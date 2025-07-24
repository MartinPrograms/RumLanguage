namespace RumLang.Tokenizer;

public enum Literal
{
    Int,
    Long,
    Float,
    Double,
    String, // This is defined in std.rum but is a special case to handle string literals. This will be statically compiled into an instance of the String class with their respective value.
    Pointer, // This gets converted to either int or long depending on the architecture. 
    Void,
    Null,
    Custom // This is used for custom types that are not defined in the standard library.
}