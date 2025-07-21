using RumCompiler;
using RumLang;

var arguments = new ConsoleUtils(args, new List<IArgument>()
{
    new ConsoleArgument<string>("i", "Input file path", true),
    new ConsoleArgument<string>("o", "Output file path", true),
    new ConsoleFlag("h", "Show help"),
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

var rum = new Rum();
var sourceCode = File.ReadAllText(inputFilePath);
var result = rum.Compile(sourceCode); // returns a (bool Success, string? ErrorMessage, string? OutputCode) where outputCode is QBE code
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