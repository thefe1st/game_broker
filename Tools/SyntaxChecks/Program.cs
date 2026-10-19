using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Parsing only. This does NOT resolve Unity API symbols or compile a Unity build.
var root = args.Length == 1 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var files = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories);
string[][] configurations = { Array.Empty<string>(), new[] { "ENABLE_INPUT_SYSTEM" },
    new[] { "ENABLE_LEGACY_INPUT_MANAGER" }, new[] { "ENABLE_INPUT_SYSTEM", "ENABLE_LEGACY_INPUT_MANAGER" } };
int errors = 0;
foreach (var symbols in configurations)
{
    var options = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: symbols);
    foreach (string file in files)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file);
        foreach (var diagnostic in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
        { Console.Error.WriteLine(diagnostic); errors++; }
    }
}
Console.WriteLine($"C# 9 syntax: {files.Length} files x {configurations.Length} input configurations; {errors} errors. Not a Unity compilation.");
return errors == 0 ? 0 : 1;
