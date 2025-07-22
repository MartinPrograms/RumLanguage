using System.Text;

namespace RumLang.Parser.Definitions;

public class IdentifierExpression :Expression
{
    public string Name { get; }

    public IdentifierExpression(string name)
    {
        Name = name;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();

        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}IdentifierExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Name: {Name}");

        return sb.ToString();
    }
}