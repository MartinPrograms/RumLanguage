using System.Text;

namespace RumLang.Parser.Definitions;

public class NamespaceDeclarationExpression : Expression
{
    public string Identifier { get; }

    public NamespaceDeclarationExpression(string identifier)
    {
        Identifier = identifier;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}Namespace Declaration");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        return sb.ToString();
    }
}