# TestGenerator

Jádro nástroje TestGen jako Roslyn komponenta (netstandard2.0). Viz `AGENTS.md` v kořeni repozitáře.

- `Model/` – AnalysisModel (vstup z EA / JSON).
- `Analysis/` – čtení `*.analysis.json`.
- `Naming/` – normalizace a párování českých jmen z analýzy na identifikátory v kódu.
- `Code/` – extrakce doménových typů a pravidel ze sémantického modelu (`Volo.Abp.Check`, `CustomCheck`, vlastní guardy).
- `Linking/` – spojení analýzy a kódu do `TestIntent` se stavem Confirmed / Conflict / AnalysisOnly / CodeOnly / Ambiguous.
- `Diagnostics/` – analyzer, který intenty hlásí jako diagnostiky `TG0001`–`TG0005` při buildu projektu, který má analýzu v `AdditionalFiles`.

Použití v cílovém projektu:

```xml
<ItemGroup>
  <ProjectReference Include="..\TestGenerator\TestGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <AdditionalFiles Include="analysis\variant-A\*.analysis.json" />
</ItemGroup>
```
