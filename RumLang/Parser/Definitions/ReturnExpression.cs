namespace RumLang.Parser.Definitions;

public class ReturnExpression : Expression, IHasChildren
{
    public Expression? Value { get; }

    public ReturnExpression(Expression? value, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
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

    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            Value != null ? new List<AstNode> { Value } : new List<AstNode>()
        };
    }
}