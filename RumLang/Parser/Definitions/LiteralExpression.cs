using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class LiteralExpression : Expression
{
    public string Value { get; }
    public Literal Type { get; }

    public LiteralExpression(string value, Literal type)
    {
        Value = value;
        Type = type;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}LiteralExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Value: {Value}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: {Type.ToString()}");

        return sb.ToString();
    }
}