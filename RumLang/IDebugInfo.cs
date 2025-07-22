namespace RumLang;

public enum DebugLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3 // Fatal.
}

public interface IDebugInfo
{
    public string GetDebugInfo(DebugLevel level = DebugLevel.Debug);
}