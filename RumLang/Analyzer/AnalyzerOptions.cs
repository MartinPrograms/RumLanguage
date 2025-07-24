namespace RumLang.Analyzer;

public class AnalyzerOptions
{
    // The default class for string literals. Must have the following constructor!
    // public string(long length, pointer data)
    public string DefaultStringClass { get; set; } = "rum.text.string";
    public string CPrefix { get; set; } = "c.";
    public string ThisKeyword { get; set; } = "this";
}