
namespace RumLang.Parser;

public interface IHasChildren
{
    public List<List<AstNode>> GetChildren();
}