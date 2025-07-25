namespace RumLang.Analyzer;

public class DependencyEntry
{
    public string FilePath { get; }
    public ExternalEntry ExternalEntry { get; }
    
    public DependencyEntry(Rum rum, string filePath, AnalyzerOptions options)
    {
        FilePath = filePath;
        
        // Load the external entry from the file
        if (System.IO.File.Exists(filePath))
        {
            // Assuming a method to deserialize the file into ExternalEntry
            ExternalEntry = LoadExternalEntryFromFile(rum, filePath, options);
        }
        else
        {
            throw new FileNotFoundException($"The file \"{filePath}\" does not exist.");
        }
    }

    public ExternalEntry LoadExternalEntryFromFile(Rum rum, string filePath, AnalyzerOptions options)
    {
        return new ExternalEntry(rum,File.ReadAllText(filePath), options);
        
    }
}