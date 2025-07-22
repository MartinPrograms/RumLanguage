using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class VariableDeclarationExpression : Expression
{    
    public string Identifier { get; }
    public string Type { get; }

    public VariableDeclarationExpression(string identifier, string type)
    {
        Identifier = identifier;
        Type = type;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}VariableDeclarationExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Type: {Type}");

        return sb.ToString();
    }
}