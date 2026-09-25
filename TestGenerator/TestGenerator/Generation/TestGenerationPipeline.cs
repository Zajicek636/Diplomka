using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using TestGenerator.Code;
using TestGenerator.Linking;
using TestGenerator.Model;

namespace TestGenerator.Generation;

public sealed class GenerationResult
{
    public GenerationResult(IReadOnlyList<TestIntent> intents, IReadOnlyList<EntityTestPlan> plans, IReadOnlyList<GeneratedFile> files)
    {
        Intents = intents;
        Plans = plans;
        Files = files;
    }

    public IReadOnlyList<TestIntent> Intents { get; }
    public IReadOnlyList<EntityTestPlan> Plans { get; }
    public IReadOnlyList<GeneratedFile> Files { get; }
}

public static class TestGenerationPipeline
{
    public static GenerationResult Run(Compilation compilation, IEnumerable<AnalysisModel> analyses, IEnumerable<ITestProfile> profiles,
        GenerationOptions options, CancellationToken cancellationToken = default)
    {
        var code = DomainCodeExtractor.Extract(compilation, cancellationToken);
        var intents = analyses.SelectMany(a => Linker.Link(a, code)).ToList();
        var plans = TestPlanBuilder.Build(code, intents);
        var files = profiles.SelectMany(p => p.Generate(plans, options)).ToList();
        return new GenerationResult(intents, plans, files);
    }
}
