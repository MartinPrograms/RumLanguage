using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class ThisExpression : Expression, IFlattenable
{
    public string TypeName { get; }
    public string FunctionName { get; }
    public ThisExpression(string typeNameName, string functionName, int lineNumber, int columnNumber) 
    : base(lineNumber, columnNumber)
    {
        TypeName = typeNameName;
        FunctionName = functionName;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}ThisExpression";
    }
    
    public string Flatten()
    {
        return "this";
    }
}