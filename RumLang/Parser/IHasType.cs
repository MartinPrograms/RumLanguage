using RumLang.Tokenizer;

namespace RumLang.Parser;

public interface IHasType
{
    public Literal TypeLiteral { get; }
    public Expression TypeExpression { get; }
}