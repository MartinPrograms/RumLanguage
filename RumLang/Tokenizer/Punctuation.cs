namespace RumLang.Tokenizer;

public enum Punctuation
{
    // Punctuation marks used in the Rum language
    Semicolon, // ; end of statement
    Comma,     // , separates items in a list or parameters in a function call
    LeftParenthesis, // ( function call or grouping
    RightParenthesis, // ) ^^
    LeftBrace, // { Defines a block of code, such as in a function or control structure
    RightBrace, // } ^^
    LeftBracket, // [ Used for array indexing
    RightBracket, // ] ^^
}