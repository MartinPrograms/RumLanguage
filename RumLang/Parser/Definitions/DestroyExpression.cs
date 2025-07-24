namespace RumLang.Parser.Definitions;

// Usage:
// destroy x; Does NOT return anything, this removes it from the heap.
public class DestroyExpression : Expression
{
    public string Identifier { get; }

    public DestroyExpression(string identifier, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        Identifier = identifier;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}DestroyExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Identifier: {Identifier}");
        return sb.ToString();
    }
}