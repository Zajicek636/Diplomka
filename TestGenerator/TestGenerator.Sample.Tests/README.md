# TestGenerator.Sample.Tests

Cíl generování pro ukázkovou doménu. Obsah `Generated/` je celý generovaný – ručně neupravovat, při dalším běhu se přepíše.

Z adresáře `TestGenerator/`:

```powershell
dotnet run --project TestGenerator.Cli -- --project TestGenerator.Sample/TestGenerator.Sample.csproj --analysis TestGenerator.Sample/analysis/variant-A/Eshop.analysis.json --profile all --namespace TestGenerator.Sample.Tests --out TestGenerator.Sample.Tests/Generated

dotnet test TestGenerator.Sample.Tests --filter "TestGenStatus!=AnalysisOnly&TestGenStatus!=Conflict"
dotnet test TestGenerator.Sample.Tests
```

Výsledek 1. běhu (27. 9. 2026, varianta A): 12 záměrů (Confirmed 8, Conflict 1, AnalysisOnly 1, Ambiguous 2), 16 souborů, 70 testů – 62 prošlo, 4 přeskočeny (IČO, PSČ × 2 profily), 4 selhaly očekávaně (E02 e-mail 256 × 200, E15 hmotnost × 2 profily).

- `Generated/Generic/` – profil `generic` (xUnit + Shouldly).
- `Generated/FakeEntity/` – profil `fakeentity` (TestHelper z CFI, cesta přes MSBuild vlastnost `CfiRoot`, výchozí `..\..\..\CFI - Mono\`).
- `Generated/testgen.manifest.json` – testovací záměry a jejich stav.

Očekávaně padají testy se stavem `Conflict`/`AnalysisOnly` (gold E02 e-mail 256 × 200, E15 chybějící hmotnost).
