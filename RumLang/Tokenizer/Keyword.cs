namespace RumLang.Tokenizer;

public enum Keyword
{
    If,
    Else,
    While,
    For,
    Return,
    Break,
    Continue,
    Import,
    New, // Related to object instantiation
    Destroy, // Related to object destruction
    Class,
    Do,
    Entrypoint, // Special keyword for the main entry point of the program
    Private, // Default
    Internal, // Other items within the same scope can access
    Public, // Can be accessed everywhere
    Export, // Tells the compiler to export it as a c style function.
    Namespace,
    Null,
    True,
    False,
    This,
}