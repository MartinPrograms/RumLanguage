using RumCompiler;
using RumLang;

var arguments = new ConsoleUtils(args, new List<IArgument>()
{
    new ConsoleArgument<string>("i", "Input file path", true),
    new ConsoleArgument<string>("o", "Output file path", true),
    new ConsoleFlag("h", "Show help"),
    new ConsoleFlag("d", "Show all debug information")
});

if (arguments.FlagExists("h"))
{
    Console.WriteLine("Rum Compiler");
    arguments.PrintHelp();
    return;
}

var inputFilePath = arguments.GetArgumentValue<string>("i");
var outputFilePath = arguments.GetArgumentValue<string>("o");

if (string.IsNullOrWhiteSpace(inputFilePath) || string.IsNullOrWhiteSpace(outputFilePath))
{
    Console.WriteLine("Input and output file paths must be specified.");
    return;
}

if (!File.Exists(inputFilePath))
{
    Console.WriteLine($"Input file '{inputFilePath}' does not exist.");
    return;
}

var rum = new Rum("./stdlib"); // Pass in a directory for the standard library.
// You can pass in more directories if needed, e.g. new Rum("./stdlib", "./morelibs");

var sourceCode = File.ReadAllText(inputFilePath);
var result = rum.Compile(sourceCode, arguments.FlagExists("d")); // returns a (bool Success, string? ErrorMessage, string? OutputCode) where outputCode is QBE code
if (result.Error != RumError.Success)
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
    return;
}

if (string.IsNullOrEmpty(result.OutputCode))
{
    Console.WriteLine("Compilation succeeded but no output code was generated.");
    return;
}
Console.WriteLine("Compilation succeeded. Output code generated.");

if (File.Exists(outputFilePath))
{
    Console.WriteLine($"Output file '{outputFilePath}' already exists. Overwriting.");
}

File.WriteAllText(outputFilePath, result.OutputCode ?? string.Empty);