namespace RumLang.Parser.Definitions;

// Usage in rum:
// type x = new SomeType();
public class NewExpression : Expression, IHasChildren
{
    public IHasType Type { get; }
    public List<Expression> Arguments { get; }

    public NewExpression(IHasType type, List<Expression> arguments, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Type = type;
        Arguments = arguments;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}NewExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: {Type}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Arguments: \n{string.Join("\n", Arguments.Select(arg => arg.GetStringRepresentation(depth + 1)))}");

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            Arguments.Cast<AstNode>().ToList()
        };
    }
}