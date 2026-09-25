# E-shop – gold seznam pravidel (analýza × kód)

Referenční „správná odpověď“ pro vyhodnocení nástroje na ukázce. Kód: `src/TestGenerator.Sample.Domain`. Analýza: `variant-A/Eshop.analysis.json`, `variant-B/Eshop.analysis.md`.
Stav podle AGENTS.md §4.3; **analýza je specifikace (R7)** → `AnalysisOnly` a `Conflict` = test podle analýzy, který padá.

| ID | Pravidlo | Kód | Stav | Detektor | Test prochází? |
|---|---|---|---|---|---|
| E01 | Zákazník.jméno povinné, max 200 | `Check.NotNullOrWhiteSpace(…, 200)` | Confirmed | D1 | ano |
| E02 | Zákazník.e-mail povinný, **max 256** | max **200** (`ZakaznikConsts.EmailMaxLength`) | **Conflict** | D1 | **ne** (délka 201–256 musí projít) |
| E03 | E-mail ve formátu x@y.z | regex + `NeplatnyEmail` | Confirmed | D3/pattern | ano |
| E04 | IČO nepovinné, 8 číslic | regex + `NeplatneIco` | Confirmed | D4/pattern | ano |
| E05 | Úvěrový limit ≥ 0 | `NastavitUverovyLimit` + `ZapornyUverovyLimit` | Confirmed | D3/range | ano |
| E06 | Nový zákazník aktivní, limit 0 | ctor | Confirmed | D7 | ano |
| E07 | Deaktivace vynuluje úvěrový limit | `Deaktivovat` | **CodeOnly** | D8 | ano |
| E08 | Adresa: ulice max 200, město max 100 (povinné) | `Check.NotNullOrWhiteSpace` | Confirmed | D1 | ano |
| E09 | PSČ = 5 číslic | regex + `NeplatnePsc` | Confirmed | pattern | ano |
| E10 | Adresa je hodnotový objekt (rovnost podle hodnot) | `ValueObject.GetAtomicValues` | Confirmed | VO equality | ano |
| E11 | Produkt.kód max 20, `[A-Z0-9-]+` | `SetKod` | Confirmed | D1 + pattern | ano |
| E12 | Kód produktu unikátní (vytvoření i změna) | `ProduktManager` + `IProduktRepository.FindByKodAsync` | Confirmed | D9 (mock repo) | ano |
| E13 | Produkt.název povinný, max 200 | `SetNazev` | Confirmed | D1 | ano |
| E14 | Cena produktu > 0 | `ZmenitCenu` + `NeplatnaCena` | Confirmed | range | ano |
| E15 | Hmotnost produktu 0..1, ≥ 0 | **chybí** | **AnalysisOnly** | reflexe | **ne** |
| E16 | Neaktivnímu produktu nelze měnit cenu | `ZmenitCenu` + `ProduktNeniAktivni` | **CodeOnly** | D4 | ano |
| E17 | Sazba DPH ∈ {0, 12, 21} | enum `SazbaDph` | Confirmed | enum | ano |
| E18 | Objednávka.číslo povinné, max 20 | ctor | Confirmed | D1 | ano |
| E19 | Nová objednávka ve stavu Nová | ctor | Confirmed | D7 | ano |
| E20 | Položka: jednotková cena > 0 | ctor `PolozkaObjednavky` | Confirmed | range | ano |
| E21 | Položka: množství 1..999 | `Check.Range` | Confirmed | range | ano |
| E22 | Cena položky = jednotková cena × množství; celková = součet | vypočtené vlastnosti | Confirmed | derived | ano |
| E23 | Max 50 položek v objednávce | **chybí** | **AnalysisOnly** | D5 (kolekce) | **ne** |
| E24 | Stejný produkt podruhé → sečíst množství | `PridatPolozku` | **CodeOnly** | D5 | ano |
| E25 | Položky měnit jen ve stavu Nová | `OveritUpravitelnost` | Confirmed | D8 | ano |
| E26 | Odebrání neexistující položky → chyba | `PolozkaNenalezena` | Confirmed | D6 | ano |
| E27 | Potvrdit jen z Nová a s ≥ 1 položkou | `Potvrdit` | Confirmed | D8 | ano |
| E28 | Potvrzená → Odeslaná → Doručená, jiné přechody zakázané | `Odeslat`, `Dorucit` | Confirmed | D8 (matice stav × akce) | ano |
| E29 | Zrušit jen z Nová/Potvrzená | povoleno i z **Odeslaná** | **Conflict** | D8 | **ne** (zrušení odeslané musí selhat) |
| E30 | Zrušení vyžaduje důvod, max 500 | `Check.NotNullOrWhiteSpace(duvod, …, 500)` | Confirmed | D1 | ano |
| E31 | Z Doručená/Zrušená žádný přechod | `Zrusit`, `OveritStav` | Confirmed | D8 | ano |

**Souhrn:** 31 pravidel – Confirmed 24, CodeOnly 3 (E07, E16, E24), AnalysisOnly 2 (E15, E23), Conflict 2 (E02, E29).
Očekávaně padající testy: **E02, E15, E23, E29**.

## Co ukázka testuje oproti Úložně

- ABP `Check.*` a `BusinessException` s kódem chyby (místo `CustomCheck` + `UserFriendlyException`) → přenositelnost detektorů.
- Hodnotový objekt (`Adresa`), `Guid` id, `internal` konstruktor vytvářený přes doménovou službu.
- Stavový automat (varianta A má explicitní `stateMachines`, varianta B jen text).
- Kolekce agregátu přes privátní `List` + `IReadOnlyCollection`.
- Doménová služba s primárním konstruktorem a mockovaným repozitářem.
