namespace RumLang.Parser.Definitions;

public class WhileExpression : Expression, IHasChildren
{
    public Expression Condition { get; }
    public List<Expression> Body { get; }

    public WhileExpression(Expression condition, List<Expression> body, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Condition = condition;
        Body = body;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}WhileExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Condition: \n{Condition.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Body: \n{string.Join("\n", Body.Select(expr => expr.GetStringRepresentation(depth + 1)))}");

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            new List<AstNode> { Condition },
            Body.Cast<AstNode>().ToList()
        };
    }
}