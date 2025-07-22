namespace RumLang.Parser.Definitions;

public class ReturnExpression : Expression
{
    public Expression? Value { get; }

    public ReturnExpression(Expression? value)
    {
        Value = value;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ReturnExpression");
        if (Value != null)
        {
            sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Value: \n{Value.GetStringRepresentation(depth + 1)}");
        }
        else
        {
            sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Value: null");
        }

        return sb.ToString();
    }
}