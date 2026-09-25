using Microsoft.Build.Locator;

namespace TestGenerator.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options == null)
        {
            CliOptions.PrintUsage();
            return 1;
        }

        if (!MSBuildLocator.IsRegistered)
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"[msbuild] {ex.Message}");
            }
        }

        return await Runner.RunAsync(options);
    }
}
