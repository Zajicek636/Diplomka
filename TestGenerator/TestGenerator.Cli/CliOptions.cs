using TestGenerator.Generation;

namespace TestGenerator.Cli;

public sealed class CliOptions
{
    public string Project { get; private set; } = "";
    public List<string> Analyses { get; } = new();
    public List<string> Profiles { get; } = new();
    public string Output { get; private set; } = "";
    public string? Namespace { get; private set; }

    public static CliOptions? Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--project" when value != null: options.Project = Path.GetFullPath(value); i++; break;
                case "--analysis" when value != null: options.Analyses.Add(Path.GetFullPath(value)); i++; break;
                case "--profile" when value != null:
                    options.Profiles.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    i++;
                    break;
                case "--out" when value != null: options.Output = Path.GetFullPath(value); i++; break;
                case "--namespace" when value != null: options.Namespace = value; i++; break;
                default:
                    Console.Error.WriteLine($"Neznámý nebo neúplný argument: {args[i]}");
                    return null;
            }
        }

        if (options.Profiles.Count == 0)
        {
            options.Profiles.Add("generic");
        }
        if (options.Profiles.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase)))
        {
            options.Profiles.Clear();
            options.Profiles.AddRange(ProfileRegistry.Names);
        }

        return options.Project.Length > 0 && options.Analyses.Count > 0 && options.Output.Length > 0 ? options : null;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("testgen --project <domain.csproj> --analysis <model.analysis.json> [--analysis ...]");
        Console.WriteLine("        --out <adresář> [--profile generic,fakeentity|all] [--namespace <root namespace testů>]");
        Console.WriteLine();
        Console.WriteLine("Profily:");
        foreach (var profile in ProfileRegistry.All.OrderBy(p => p.Name))
        {
            Console.WriteLine($"  {profile.Name,-12} {profile.Description}");
        }
    }
}
