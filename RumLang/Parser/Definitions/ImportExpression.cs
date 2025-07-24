using System.Text;

namespace RumLang.Parser.Definitions;

public class ImportExpression : Expression
{
    public string Target { get; }

    public ImportExpression(string target, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Target = target;
    }
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();

        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ImportExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Target: {Target}");
        return sb.ToString();
    }
}