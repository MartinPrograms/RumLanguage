using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class LiteralExpression : Expression, IHasType
{
    public string Value { get; }
    public Literal TypeLiteral { get; }
    public Expression TypeExpression { get; }
    
    public LiteralExpression(string value, Literal type, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Value = value;
        TypeLiteral = type;
        TypeExpression = this;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}LiteralExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Value: {Value}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: {TypeLiteral.ToString()}");

        return sb.ToString();
    }
}