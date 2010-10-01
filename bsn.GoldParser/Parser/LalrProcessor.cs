// bsn GoldParser .NET Engine
// --------------------------
// 
// Copyright 2009, 2010 by Ars�ne von Wyss - avw@gmx.ch
// 
// Development has been supported by Sirius Technologies AG, Basel
// 
// Source:
// 
// https://bsn-goldparser.googlecode.com/hg/
// 
// License:
// 
// The library is distributed under the GNU Lesser General Public License:
// http://www.gnu.org/licenses/lgpl.html
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using bsn.GoldParser.Grammar;

namespace bsn.GoldParser.Parser {
	internal class LalrStack<T> where T: class, IToken {
		private class RangePop: IList<T> {
			private readonly int bottomIndex;
			private readonly KeyValuePair<T, LalrState>[] items;
			private readonly int topIndex;

			public RangePop(KeyValuePair<T, LalrState>[] items, int topIndex, int bottomIndex) {
				Debug.Assert(items != null);
				Debug.Assert((topIndex < items.Length) && (topIndex >= bottomIndex) && (bottomIndex >= 0));
				this.items = items;
				this.topIndex = topIndex;
				this.bottomIndex = bottomIndex;
			}

			public IEnumerator<T> GetEnumerator() {
				for (int i = bottomIndex+1; i <= topIndex; i++) {
					yield return items[i].Key;
				}
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}

			public void Add(T item) {
				throw new NotSupportedException();
			}

			public void Clear() {
				throw new NotSupportedException();
			}

			public bool Contains(T item) {
				return IndexOf(item) >= 0;
			}

			public void CopyTo(T[] array, int arrayIndex) {
				for (int i = bottomIndex+1; i <= topIndex; i++) {
					array[arrayIndex++] = items[i].Key;
				}
			}

			public bool Remove(T item) {
				throw new NotSupportedException();
			}

			public int Count {
				get {
					return topIndex-bottomIndex;
				}
			}

			public bool IsReadOnly {
				get {
					return true;
				}
			}

			public int IndexOf(T item) {
				if (item != null) {
					for (int i = bottomIndex+1; i <= topIndex; i++) {
						if (item == items[i].Key) {
							return i-(bottomIndex+1);
						}
					}
				}
				return -1;
			}

			public void Insert(int index, T item) {
				throw new NotSupportedException();
			}

			public void RemoveAt(int index) {
				throw new NotSupportedException();
			}

			public T this[int index] {
				get {
					if ((index < 0) || (index >= Count)) {
						throw new ArgumentOutOfRangeException("index");
					}
					return items[index+bottomIndex+1].Key;
				}
				set {
					throw new NotSupportedException();
				}
			}
		}

		private KeyValuePair<T, LalrState>[] items = new KeyValuePair<T, LalrState>[128];
		private int topIndex;

		public LalrStack(LalrState initialState) {
			if (initialState == null) {
				throw new ArgumentNullException("initialState");
			}
			items[0] = new KeyValuePair<T, LalrState>(default(T), initialState);
		}

		public LalrState GetTopState() {
			return items[topIndex].Value;
		}

		public T Peek() {
			return items[topIndex].Key;
		}

		public T Pop() {
			Debug.Assert(topIndex >= 0);
			return items[topIndex--].Key;
		}

		public IList<T> PopRange(int count) {
			Debug.Assert(count >= 0);
			int oldTopIndex = topIndex;
			topIndex -= count;
			return new RangePop(items, oldTopIndex, topIndex);
		}

		public void Push(T token, LalrState state) {
			if ((topIndex-2) == items.Length) {
				Array.Resize(ref items, items.Length*2);
			}
			items[++topIndex] = new KeyValuePair<T, LalrState>(token, state);
		}
	}

	/// <summary>
	/// Pull parser which uses Grammar table to parse input stream.
	/// </summary>
	public abstract class LalrProcessor<T>: IParser<T> where T: class, IToken {
		private readonly LalrStack<T> tokenStack; // Stack of LR states used for LR parsing.
		private readonly ITokenizer<T> tokenizer;
		private LalrState currentState;
		private T currentToken;

		/// <summary>
		/// Initializes new instance of Parser class.
		/// </summary>
		/// <param name="tokenizer">The tokenizer.</param>
		/// <param name="trim">if set to <c>true</c> [trim].</param>
		protected LalrProcessor(ITokenizer<T> tokenizer) {
			if (tokenizer == null) {
				throw new ArgumentNullException("tokenizer");
			}
			this.tokenizer = tokenizer;
			currentState = tokenizer.Grammar.InitialLRState;
			tokenStack = new LalrStack<T>(currentState);
		}

		/// <summary>
		/// Gets the current currentToken.
		/// </summary>
		/// <value>The current currentToken.</value>
		public T CurrentToken {
			get {
				if (currentToken != null) {
					return currentToken;
				}
				return tokenStack.Peek();
			}
		}

		/// <summary>
		/// Gets array of expected currentToken symbols.
		/// </summary>
		public ReadOnlyCollection<Symbol> GetExpectedTokens() {
			List<Symbol> expectedTokens = new List<Symbol>(currentState.ActionCount);
#warning Do we need to recurse somehow on non-terminals?
			for (int i = 0; i < currentState.ActionCount; i++) {
				switch (currentState.GetAction(i).Symbol.Kind) {
				case SymbolKind.Terminal:
				case SymbolKind.End:
					expectedTokens.Add(currentState.GetAction(i).Symbol);
					break;
				}
			}
			return expectedTokens.AsReadOnly();
		}

		/// <summary>
		/// Executes next step of parser and returns parser currentState.
		/// </summary>
		/// <returns>Parser current currentState.</returns>
		public virtual ParseMessage Parse() {
			while (true) {
				T inputToken;
				if (currentToken == null) {
					//We must read a currentToken
					T textInputToken;
					ParseMessage message = tokenizer.NextToken(out textInputToken);
					if (textInputToken == null) {
						return ParseMessage.InternalError;
					}
					//					Debug.WriteLine(string.Format("State: {0} Line: {1}, Column: {2}, Parse Value: {3}, Token Type: {4}", currentState.Index, inputToken.Line, inputToken.LinePosition, inputToken.Text, inputToken.symbol.Name), "Token Read");
					if (textInputToken.Symbol.Kind != SymbolKind.End) {
						currentToken = textInputToken;
						return message;
					}
					inputToken = textInputToken;
				} else {
					inputToken = currentToken;
				}
				switch (inputToken.Symbol.Kind) {
				case SymbolKind.WhiteSpace:
				case SymbolKind.CommentStart:
				case SymbolKind.CommentLine:
					ClearCurrentToken();
					break;
				case SymbolKind.Error:
					return ParseMessage.LexicalError;
				default:
					LalrAction action = currentState.GetActionBySymbol(inputToken.Symbol);
					if (action == null) {
						if (RetrySyntaxError(ref inputToken)) {
							currentToken = inputToken;
							continue;
						}
						return ParseMessage.SyntaxError;
					}
					// the Execute() is the ParseToken() equivalent
					switch (action.Execute(this, inputToken)) {
					case TokenParseResult.Accept:
						return ParseMessage.Accept;
					case TokenParseResult.Shift:
						ClearCurrentToken();
						break;
					case TokenParseResult.SyntaxError:
						return ParseMessage.SyntaxError;
					case TokenParseResult.ReduceNormal:
						return ParseMessage.Reduction;
					case TokenParseResult.InternalError:
						return ParseMessage.InternalError;
					}
					break;
				}
			}
		}

		protected virtual bool RetrySyntaxError(ref T currentToken) {
			return false;
		}

		public ParseMessage ParseAll() {
			ParseMessage result;
			do {
				result = Parse();
			} while (CompiledGrammar.CanContinueParsing(result));
			return result;
		}

		protected abstract bool CanTrim(Rule rule);

		protected abstract T CreateReduction(Rule rule, IList<T> children);

		private void ClearCurrentToken() {
			currentToken = default(T);
		}

		bool IParser<T>.CanTrim(Rule rule) {
			return CanTrim(rule);
		}

		T IParser<T>.PopToken() {
			return tokenStack.Pop();
		}

		void IParser<T>.PushTokenAndState(T token, LalrState state) {
			Debug.Assert(state != null);
			tokenStack.Push(token, state);
			currentState = state;
		}

		T IParser<T>.CreateReduction(Rule rule) {
			Debug.Assert(rule != null);
			return CreateReduction(rule, tokenStack.PopRange(rule.SymbolCount));
		}

		LalrState IParser<T>.TopState {
			get {
				return tokenStack.GetTopState();
			}
		}
	}

	///<summary>
	/// A concrete implementation of the <see cref="LalrProcessor{T}"/> using <see cref="Token"/> as token types
	///</summary>
	public class LalrProcessor: LalrProcessor<Token> {
		private readonly bool trim;

		/// <summary>
		/// Initializes a new instance of the <see cref="LalrProcessor"/> class.
		/// </summary>
		/// <param name="tokenizer">The tokenizer.</param>
		public LalrProcessor(ITokenizer<Token> tokenizer): this(tokenizer, false) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="LalrProcessor"/> class.
		/// </summary>
		/// <param name="tokenizer">The tokenizer.</param>
		/// <param name="trim">Trim the rules withn only one nonterminal away if set to <c>true</c>.</param>
		public LalrProcessor(ITokenizer<Token> tokenizer, bool trim): base(tokenizer) {
			this.trim = trim;
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="LalrProcessor"/> does automatically trim the tokens with a single terminal.
		/// </summary>
		/// <value><c>true</c> if automatic trimming is enabled; otherwise, <c>false</c>.</value>
		public bool Trim {
			get {
				return trim;
			}
		}

		protected override bool CanTrim(Rule rule) {
			return trim;
		}

		/// <summary>
		/// Creates the reduction.
		/// </summary>
		/// <param name="rule">The rule.</param>
		/// <param name="children">The children.</param>
		/// <returns></returns>
		protected override Token CreateReduction(Rule rule, IList<Token> children) {
			return new Reduction(rule, children);
		}
	}
}