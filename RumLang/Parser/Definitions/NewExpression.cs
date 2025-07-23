namespace RumLang.Parser.Definitions;

// Usage in rum:
// type x = new SomeType();
public class NewExpression : Expression
{
    public string Type { get; }
    public List<Expression> Arguments { get; }

    public NewExpression(string type, List<Expression> arguments)
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
}