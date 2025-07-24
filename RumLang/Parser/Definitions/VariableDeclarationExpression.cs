using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class VariableDeclarationExpression : Expression, IHasType
{    
    public string Identifier { get; }
    public IHasType Type { get; }
    public Literal TypeLiteral => Type.TypeLiteral;
    public Expression TypeExpression => (Expression)Type;

    public VariableDeclarationExpression(string identifier, IHasType type, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Identifier = identifier;
        Type = type;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}VariableDeclarationExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: \n{((AstNode)Type).GetStringRepresentation(depth + 1)}");

        return sb.ToString();
    }
}