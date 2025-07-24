using System.Text;

namespace RumLang.Parser.Definitions;

public class NamespaceDeclarationExpression : Expression, IHasChildren
{
    public string Identifier { get; }
    public List<AstNode> Nodes { get; }
    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    public NamespaceDeclarationExpression(string identifier, List<AstNode> nodes, int lineNumber, int columnNumber,AccessModifier accessModifier = AccessModifier.Public) 
        : base(lineNumber, columnNumber)
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
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Access Modifier: {AccessModifier}");
        sb.AppendLine(
            $"{StringHelpers.Repeat("\t", depth)}:- Nodes: \n{StringHelpers.Repeat("\t", depth)}{string.Join($"\n", Nodes.Select(x => x.GetStringRepresentation(depth + 1)))}");
        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            Nodes.Cast<AstNode>().ToList()
        };
    }
}