using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TestGenerator.Analysis;
using TestGenerator.Code;
using TestGenerator.Linking;

namespace TestGenerator.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AnalysisConformanceAnalyzer : DiagnosticAnalyzer
{
    public const string AnalysisFileSuffix = ".analysis.json";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Descriptors.Confirmed,
        Descriptors.Conflict,
        Descriptors.AnalysisOnly,
        Descriptors.CodeOnly,
        Descriptors.Ambiguous,
        Descriptors.InvalidAnalysis);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var files = context.Options.AdditionalFiles
            .Where(f => f.Path.EndsWith(AnalysisFileSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (files.Count == 0)
        {
            return;
        }

        var code = DomainCodeExtractor.Extract(context.Compilation, context.CancellationToken);

        foreach (var file in files)
        {
            var text = file.GetText(context.CancellationToken)?.ToString();
            if (text == null)
            {
                continue;
            }

            Model.AnalysisModel analysis;
            try
            {
                analysis = AnalysisJsonReader.Read(text);
            }
            catch (JsonException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.InvalidAnalysis, Location.None, file.Path, ex.Message));
                continue;
            }

            foreach (var intent in Linker.Link(analysis, code))
            {
                var location = intent.Location.IsInSource ? intent.Location : Location.None;
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.For(intent.Status), location, intent.Id, intent.Message));
            }
        }
    }
}
