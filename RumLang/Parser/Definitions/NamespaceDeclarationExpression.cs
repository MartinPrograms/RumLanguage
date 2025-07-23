using System.Text;

namespace RumLang.Parser.Definitions;

public class NamespaceDeclarationExpression : Expression
{
    public string Identifier { get; }
    public List<AstNode> Nodes { get; }
    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    public NamespaceDeclarationExpression(string identifier, List<AstNode> nodes, AccessModifier accessModifier = AccessModifier.Public)
    {
        Identifier = identifier;
        Nodes = nodes;
        AccessModifier = accessModifier;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}Namespace Declaration");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        sb.AppendLine(
            $"{StringHelpers.Repeat("\t", depth)}:- Nodes: \n{StringHelpers.Repeat("\t", depth)}{string.Join($"\n", Nodes.Select(x => x.GetStringRepresentation(depth + 1)))}");
        return sb.ToString();
    }
}