using System.Collections.Generic;

namespace TestGenerator.Generation;

public interface ITestProfile
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<GeneratedFile> Generate(IReadOnlyList<EntityTestPlan> plans, GenerationOptions options);
}

public sealed class GeneratedFile
{
    public GeneratedFile(string relativePath, string content)
    {
        RelativePath = relativePath;
        Content = content;
    }

    public string RelativePath { get; }
    public string Content { get; }
}

public sealed class GenerationOptions
{
    public string RootNamespace { get; set; } = "Generated.Tests";
    public string ToolVersion { get; set; } = "0.1";
}
