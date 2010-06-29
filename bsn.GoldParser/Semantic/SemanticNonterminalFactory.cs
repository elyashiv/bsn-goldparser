using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using bsn.GoldParser.Grammar;

namespace bsn.GoldParser.Semantic {
	public abstract class SemanticNonterminalFactory: SemanticTokenFactory {
		internal protected abstract IEnumerable<Symbol> GetInputSymbols(Rule rule);

		public abstract ReadOnlyCollection<Type> InputTypes {
			get;
		}

		internal abstract SemanticToken CreateInternal(Rule rule, ReadOnlyCollection<SemanticToken> tokens);
	}

	public abstract class SemanticNonterminalFactory<T>: SemanticNonterminalFactory where T: SemanticToken {
		public override sealed Type OutputType {
			get {
				return typeof(T);
			}
		}

		protected internal override IEnumerable<Symbol> GetInputSymbols(Rule rule) {
			if (rule == null) {
				throw new ArgumentNullException("rule");
			}
			return rule;
		}

		public abstract T Create(Rule rule, ReadOnlyCollection<SemanticToken> tokens);

		internal override sealed SemanticToken CreateInternal(Rule rule, ReadOnlyCollection<SemanticToken> tokens) {
			return Create(rule, tokens);
		}
	}
}