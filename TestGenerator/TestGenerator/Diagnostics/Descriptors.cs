using Microsoft.CodeAnalysis;
using TestGenerator.Linking;

namespace TestGenerator.Diagnostics;

public static class Descriptors
{
    private const string Category = "TestGen";

    public static readonly DiagnosticDescriptor Confirmed = new("TG0001", "Pravidlo potvrzeno",
        "[{0}] {1}", Category, DiagnosticSeverity.Info, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor Conflict = new("TG0002", "Analýza a kód se liší",
        "[{0}] {1}", Category, DiagnosticSeverity.Warning, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor AnalysisOnly = new("TG0003", "Pravidlo z analýzy v kódu chybí",
        "[{0}] {1}", Category, DiagnosticSeverity.Warning, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor CodeOnly = new("TG0004", "Pravidlo je jen v kódu",
        "[{0}] {1}", Category, DiagnosticSeverity.Info, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor Ambiguous = new("TG0005", "Nejednoznačné pravidlo",
        "[{0}] {1}", Category, DiagnosticSeverity.Info, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor InvalidAnalysis = new("TG0100", "Soubor analýzy nelze načíst",
        "Soubor analýzy '{0}' nelze načíst: {1}", Category, DiagnosticSeverity.Warning, true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static DiagnosticDescriptor For(MatchStatus status) => status switch
    {
        MatchStatus.Confirmed => Confirmed,
        MatchStatus.Conflict => Conflict,
        MatchStatus.AnalysisOnly => AnalysisOnly,
        MatchStatus.CodeOnly => CodeOnly,
        _ => Ambiguous
    };
}
