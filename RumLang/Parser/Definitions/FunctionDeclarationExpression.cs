using System.Text;

namespace RumLang.Parser.Definitions;

public class FunctionDeclarationExpression : Expression
{
    public string FunctionName { get; }
    
    /// <summary>
    /// Reused type, Value is repurposed as variable name.
    /// </summary>
    public List<Expression> Arguments { get; }
    
    public string ReturnType { get; }
    
    public AccessModifier Access { get; }
    
    public bool IsEntryPoint { get; }

    public List<Expression> Expressions { get; }
    
    public bool IsVariadic { get; }
    
    public bool Export { get; }

    public FunctionDeclarationExpression(string functionName, List<Expression> arguments, string returnType,
        AccessModifier accessModifier, List<Expression> expressions, bool isVariadic, bool isEntryPoint = false, bool export = false)
    {
        FunctionName = functionName;
        Arguments = arguments;
        ReturnType = returnType;
        Access = accessModifier;
        Expressions = expressions; 
        IsVariadic = isVariadic;
        IsEntryPoint = isEntryPoint;
        Export = export;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}FunctionDeclaration");
        sb.Append($"{StringHelpers.Repeat("\t", depth)}:- Name: {FunctionName}");
        if (IsEntryPoint)
            sb.Append($" (entrypoint)");
        if (Expressions.Count <= 0)
            sb.Append(" (no expressions)");
        if (Export)
            sb.Append(" (exported)");
        sb.AppendLine();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Return Type: {ReturnType.ToString()}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Access Modifier: {Access.ToString()}");
        var targetDepth = depth + 1;
        string isVarArg = IsVariadic ? "(variadic)" : string.Empty;
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Arguments {isVarArg} \n{string.Join("\n", Arguments.Select(x => x.GetStringRepresentation(targetDepth)))}");
        
        if(Expressions.Count > 0)
            sb.Append(
                $"{StringHelpers.Repeat("\t", depth)}:- Expressions \n{string.Join("\n", Expressions.Select(x => x.GetStringRepresentation(targetDepth)))}");
        return sb.ToString();
    }
}