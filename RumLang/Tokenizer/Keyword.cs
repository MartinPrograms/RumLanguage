namespace RumLang.Tokenizer;

public enum Keyword
{
    If,
    Else,
    While,
    For,
    Function,
    Return,
    Break,
    Continue,
    Import,
    Class,
    Try,
    Catch,
    Finally,
    Switch,
    Case,
    Default,
    Do,
    End,
    Entrypoint, // Special keyword for the main entry point of the program
    Private, // Default
    Internal, // Other items within the same scope can access
    Public, // Can be accessed everywhere
    Export, // Tells the compiler to export it as a c style function.
    Namespace
}