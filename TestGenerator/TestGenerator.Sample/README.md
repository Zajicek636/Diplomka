# Ukázka: E-shop (3 DDD agregáty)

Projekt TestGenerator.Sample – malá ABP doména (Volo.Abp.Ddd.Domain 8.1.1, jen veřejné NuGety) pro první pokusy s TestGen – nezávislé na CFI.

| Agregát | Obsah |
|---|---|
| `Zakaznik` + VO `Adresa` | validace (délky, e-mail, IČO, PSČ), úvěrový limit, aktivace |
| `Produkt` + `ProduktManager` | kód (formát, unikátnost přes repozitář), cena, DPH, deaktivace |
| `Objednavka` + `PolozkaObjednavky` | kolekce položek, výpočet ceny, stavový automat Nová → Potvrzená → Odeslaná → Doručená / Zrušená |

- Analýza: `analysis/variant-A` (strukturovaná JSON, jako z EA), `analysis/variant-B` (volný text).
- Gold: `analysis/Eshop.gold.md` – 31 pravidel, z toho 4 záměrné rozpory (E02, E15, E23, E29) → testy mají padat.
- Analyzer `TestGenerator` čte `analysis/variant-A/*.analysis.json` (`AdditionalFiles`) a při buildu hlásí shodu s kódem.

```bash
dotnet build TestGenerator/TestGenerator.sln
dotnet test  TestGenerator/TestGenerator.sln
```

Očekávané diagnostiky po buildu (zatím jen pravidla textových atributů a existence členů):

| Diagnostika | Pravidlo |
|---|---|
| TG0002 Conflict | `Zakaznik.Email` – analýza max 256, kód 200 (gold E02) |
| TG0003 AnalysisOnly | `Produkt` – atribut `hmotnost` v kódu chybí (gold E15) |
| TG0005 Ambiguous | `Zakaznik.Ico`, `Adresa.Psc` – validace regexem, nelze deterministicky porovnat |
| TG0001 Confirmed (Info) | jméno, ulice, město, kód, název produktu, číslo, důvod zrušení, název položky |

Gold E23 (max 50 položek) a E29 (zrušení odeslané objednávky) čekají na detektory kolekcí a stavových přechodů.
