using QbeGenerator;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Analyzer;

public class AnalyzerType
{
    public string Name { get; set; }
    public AccessModifier AccessModifier { get; set; }
    public List<ClassMemberDeclaration> Members { get; set; }
    public List<FunctionDeclarationExpression> Functions { get; set; }

    public static readonly Dictionary<string, Literal> BuiltInTypes = new()
    {
        { "int", Literal.Int },
        { "long", Literal.Long },
        { "float", Literal.Float },
        { "string", Literal.String },
        { "double", Literal.Double },
        { "void", Literal.Void },
        { "pointer", Literal.Pointer },
        { "bool", Literal.Int }, // Bool gets cast to int in the RumLang runtime.
        { "null", Literal.Null },
    };

    public static Literal ImplicitCast(Literal type, Literal toType)
    {
        if (type == toType)
        {
            return type;
        }
        
        // Integers can be implicitly cast to long, float, and double.
        // Long can be implicitly cast to itn, float, and double.
        // Floats can be implicitly cast to double.
        // Doubles can be implicitly cast to float.
        if (type == Literal.Int && (toType == Literal.Long || toType == Literal.Float || toType == Literal.Double))
        {
            return toType;
        }
        if (type == Literal.Long && (toType == Literal.Int || toType == Literal.Float || toType == Literal.Double))
        {
            return toType;
        }
        if (type == Literal.Float && toType == Literal.Double)
        {
            return toType;
        }
        if (type == Literal.Double && toType == Literal.Float)
        {
            return toType;
        }
        // Strings can not be implicitly cast to any other type.
        if (type == Literal.String)
        {
            throw new InvalidOperationException("Cannot implicitly cast string to another type.");
        }

        return type; // If no implicit cast is possible, return the original type.
    }
    
    public static QbePrimitive ImplicitCast(IQbeTypeDefinition typeDef, IQbeTypeDefinition toTypeDef)
    {
        if (typeDef is not QbePrimitive type || toTypeDef is not QbePrimitive toType)
        {
            throw new ArgumentException("Both types must be QbePrimitive types for implicit casting.");
        }
        
        if (type == toType)
        {
            return type;
        }
        
        // Integers can be implicitly cast to long, float, and double.
        // Long can be implicitly cast to int, float, and double.
        // Floats can be implicitly cast to double.
        // Doubles can be implicitly cast to float.
        if (type == QbePrimitive.Int32 && (toType == QbePrimitive.Int64 || toType == QbePrimitive.Float || toType == QbePrimitive.Double))
        {
            return toType;
        }
        if (type == QbePrimitive.Int64 && (toType == QbePrimitive.Int32 || toType == QbePrimitive.Float || toType == QbePrimitive.Double))
        {
            return toType;
        }
        if (type == QbePrimitive.Float && toType == QbePrimitive.Double)
        {
            return toType;
        }
        if (type == QbePrimitive.Double && toType == QbePrimitive.Float)
        {
            return toType;
        }
        
        // Pointers can be both int or long, depending on the architecture. But we do not decide the architecture here, so we just return the original type.
        if (type == QbePrimitive.Pointer && (toType == QbePrimitive.Int32 || toType == QbePrimitive.Int64))
        {
            return toType; // Return the pointer type as it can be cast to either int or long.
        }

        return type; // If no implicit cast is possible, return the original type.
    }
}