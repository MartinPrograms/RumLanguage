using System.Text;

namespace RumLang.Parser.Definitions;

public class IdentifierExpression : Expression, IFlattenable
{
    public string Identifier { get; }

    public IdentifierExpression(string identifier, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Identifier = identifier;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();

        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}IdentifierExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Name: {Identifier}");

        return sb.ToString();
    }
    
    public string Flatten()
    {
        return Identifier;
    }
}