using QbeGenerator;
using RumLang.Tokenizer;

namespace RumLang.CodeGen;

public static class CodeGenHelpers
{
    public static string QbeGetCustomTypeName(string name)
    {
        return string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
    }

    public static IQbeTypeDefinition QbeGetLiteralType(Literal fieldTypeLiteral)
    {
        return fieldTypeLiteral switch
        {
            Literal.Int => QbePrimitive.Int32,
            Literal.Long => QbePrimitive.Int64,
            Literal.Float => QbePrimitive.Float,
            Literal.Double => QbePrimitive.Double,
            Literal.Pointer => QbePrimitive.Pointer,
            Literal.String => QbePrimitive.Pointer, // TODO: Handle string type properly
            _ => throw new ArgumentException($"Unsupported literal type: {fieldTypeLiteral}")
        };
    }

    public static string QbeGetCustomFunctionName(QbeType? qbeType, string functionFunctionName)
    {
        // qbeType.Identifier__functionFunctionName
        return qbeType != null 
            ? $"{qbeType.Identifier}__{functionFunctionName}" 
            : functionFunctionName;
    }

    public static string GetIdentifierFromPotentiallMemberedString(string id)
    {
        // Remove any member access (e.g., "object.member") and return the identifier
        var parts = id.Split('.');
        return parts.Length > 0 ? parts.Last() : id;
    }
}