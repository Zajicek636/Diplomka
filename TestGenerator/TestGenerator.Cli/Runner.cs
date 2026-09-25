using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using TestGenerator.Analysis;
using TestGenerator.Generation;
using TestGenerator.Linking;

namespace TestGenerator.Cli;

public static class Runner
{
    public static async Task<int> RunAsync(CliOptions options)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
        {
            if (!e.Diagnostic.Message.Contains("known") || !e.Diagnostic.Message.Contains("vulnerability"))
            {
                Console.Error.WriteLine($"[workspace] {e.Diagnostic.Kind}: {e.Diagnostic.Message}");
            }
        };

        Console.WriteLine($"Načítám {options.Project}");
        var project = await workspace.OpenProjectAsync(options.Project);
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            Console.Error.WriteLine("Kompilaci projektu se nepodařilo vytvořit.");
            return 2;
        }

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"Pozor: projekt má {errors.Count} chyb kompilace, sémantický model může být neúplný. První: {errors[0]}");
        }

        var analyses = options.Analyses.Select(p => AnalysisJsonReader.Read(File.ReadAllText(p))).ToList();
        var profiles = options.Profiles.Select(ProfileRegistry.Get).ToList();
        var generationOptions = new GenerationOptions
        {
            RootNamespace = options.Namespace ?? (project.AssemblyName + ".Tests"),
            ToolVersion = typeof(TestGenerationPipeline).Assembly.GetName().Version?.ToString() ?? "0.1"
        };

        var result = TestGenerationPipeline.Run(compilation, analyses, profiles, generationOptions);
        Write(options.Output, result);
        PrintSummary(result);
        return 0;
    }

    private static void Write(string output, GenerationResult result)
    {
        foreach (var root in result.Files.Select(f => f.RelativePath.Split('/')[0]).Distinct())
        {
            var directory = Path.Combine(output, root);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        foreach (var file in result.Files)
        {
            var path = Path.Combine(output, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }

        var manifest = new
        {
            files = result.Files.Select(f => f.RelativePath).OrderBy(f => f),
            intents = result.Intents.Select(i => new
            {
                i.Id,
                Status = i.Status.ToString(),
                Kind = i.Kind.ToString(),
                i.Message,
                i.AnalysisClass,
                i.AnalysisAttribute,
                i.CodeType,
                i.CodeProperty,
                i.Required,
                i.MaxLength
            })
        };
        File.WriteAllText(Path.Combine(output, "testgen.manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
    }

    private static void PrintSummary(GenerationResult result)
    {
        Console.WriteLine();
        Console.WriteLine("Testovací záměry:");
        foreach (var group in result.Intents.GroupBy(i => i.Status).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {group.Key,-13} {group.Count()}");
        }
        foreach (var intent in result.Intents.Where(i => i.Status != MatchStatus.Confirmed).OrderBy(i => i.Status))
        {
            Console.WriteLine($"    [{intent.Status}] {intent.Message}");
        }

        var skipped = result.Plans.SelectMany(p => p.StringTests).Count(t => t.SkipReason != null);
        Console.WriteLine();
        Console.WriteLine($"Soubory: {result.Files.Count}, typy: {result.Plans.Count}, přeskočené testy (Skip): {skipped}");
    }
}
