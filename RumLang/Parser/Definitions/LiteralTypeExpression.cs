using RumLang.Analyzer;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class LiteralTypeExpression : Expression, IFlattenable, IHasType
{
    public Literal TypeLiteral { get; }
    public Expression TypeExpression { get; }

    public LiteralTypeExpression(string str, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        var typemap = AnalyzerType.BuiltInTypes;
        if (typemap.TryGetValue(str, out var type))
        {
            TypeLiteral = type;
            TypeExpression = new IdentifierExpression(str, lineNumber, columnNumber);
        }
        else
        {
            TypeLiteral = Literal.Custom;
            TypeExpression = new IdentifierExpression(str, lineNumber, columnNumber);
        }
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}LiteralTypeExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- TypeLiteral: {TypeLiteral.ToString()}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- TypeExpression: \n{TypeExpression.GetStringRepresentation(depth + 1)}");
        return sb.ToString();
    }
    
    public string Flatten()
    {
        return TypeExpression is IFlattenable flattenable ? flattenable.Flatten() : "<unknown>";
    }
}