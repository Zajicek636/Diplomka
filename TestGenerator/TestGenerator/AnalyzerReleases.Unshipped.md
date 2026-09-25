; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TG0001 | TestGen | Info | Pravidlo z analýzy je v kódu (Confirmed)
TG0002 | TestGen | Warning | Analýza a kód se liší (Conflict)
TG0003 | TestGen | Warning | Pravidlo z analýzy v kódu chybí (AnalysisOnly)
TG0004 | TestGen | Info | Pravidlo je jen v kódu (CodeOnly)
TG0005 | TestGen | Info | Nejednoznačné párování nebo vlastní validace (Ambiguous)
TG0100 | TestGen | Warning | Soubor analýzy nelze načíst
