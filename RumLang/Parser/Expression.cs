namespace RumLang.Parser;

public abstract class AstNode
{
    public abstract string GetStringRepresentation(int depth = 0);
}

public abstract class Expression : AstNode { }