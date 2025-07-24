using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

// Represents a class member declaration in the RumLang parser.
public class ClassMemberDeclaration : Expression, IHasType, IHasChildren
{
    public string Identifier { get; }
    public IHasType Expression { get; }
    public Expression TypeExpression => (Expression)Expression;
    public Literal TypeLiteral => Expression.TypeLiteral;
    public AccessModifier AccessModifier { get; }
    
    public ClassMemberDeclaration(string identifier, IHasType type, AccessModifier accessModifier, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Identifier = identifier;
        Expression = type;
        AccessModifier = accessModifier;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ClassMemberDeclaration");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: \n{TypeExpression.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- AccessModifier: {AccessModifier.ToString()}");

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        return new List<List<AstNode>>
        {
            new List<AstNode> { TypeExpression }
        };
    }
}