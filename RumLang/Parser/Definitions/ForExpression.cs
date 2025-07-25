namespace RumLang.Parser.Definitions;

public class ForExpression : Expression, IHasChildren
{
    public Expression Start { get; }
    public Expression End { get; }
    public Expression Step { get; }
    public List<Expression> Body { get; }

    public ForExpression(Expression start, Expression end, Expression step, List<Expression> body, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Start = start;
        End = end;
        Step = step;
        Body = body;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ForExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Start: \n{Start.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- End: \n{End.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Step: \n{Step.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Body: \n{string.Join("\n", Body.Select(expr => expr.GetStringRepresentation(depth + 1)))}");

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            new List<AstNode> { Start, End, Step },
            Body.Cast<AstNode>().ToList()
        };
    }
}