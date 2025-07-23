namespace RumLang.Parser.Definitions;

// Represents a class member declaration in the RumLang parser.
public class ClassMemberDeclaration : Expression
{
    public string Identifier { get; }
    public string Type { get; }
    public AccessModifier AccessModifier { get; }
    
    public ClassMemberDeclaration(string identifier, string type, AccessModifier accessModifier)
    {
        Identifier = identifier;
        Type = type;
        AccessModifier = accessModifier;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ClassMemberDeclaration");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: {Type}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- AccessModifier: {AccessModifier.ToString()}");

        return sb.ToString();
    }
}