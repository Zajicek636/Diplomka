# AGENTS.md – Diplomová práce: poloautomatizované generování testů v .NET (ABP)

> Živý dokument. Upravujeme ho průběžně s tím, jak se vyjasňují rozhodnutí.
> Každá změna architektury nebo konvence se musí promítnout sem (sekce **Rozhodnutí** a **Otevřené otázky**).

---

## 1. Kontext a cíl

Diplomová práce: *Poloautomatizované generování testů v .NET aplikacích s využitím statické analýzy a doménového kontextu.*

Cílem je nástroj (Roslyn), který:

1. načte **doménovou a use-case analýzu** z Enterprise Architectu (export XMI) – případně z méně strukturovaného textového popisu,
2. provede **statickou analýzu zdrojového kódu** ABP řešení (syntaktický + sémantický model),
3. obě analýzy propojí do **modelu testovacích záměrů (Test Intents)**,
4. z testovacích záměrů **vygeneruje testy** – nejdřív unit testy doménové vrstvy, později integrační testy aplikační vrstvy,
5. vyprodukuje **report** (co bylo nalezeno, co se nepodařilo spárovat, co vyžaduje ruční doplnění).

**Fáze 1 (aktuální): doménové unit testy** – entity, agregáty, doménové služby (`*Manager`).

---

## 2. Repozitář a pracovní složky

| Cesta | Význam | Pravidla |
|---|---|---|
| `C:\Dimplomka` (tento repozitář) | Zdrojáky nástroje, dokumentace, experimenty | Sem se píše. |
| `C:\CFI - Mono\Dynatech.CFI\src\modules\Ulozna` | **Referenční cílový kód** (ABP modul Úložna) | **Pouze pro čtení.** Nic neměnit, necommitovat. |

Poznámky k Úložně:

- Úložna je modul v mono-repu CFI. Projekty referencují soubory **mimo modul** (`..\..\..\..\framework\...`, `..\..\..\SequencesManagement\...`, `common.props`). Pro plný sémantický model je nutné otevírat řešení v kontextu celého mono-repa (nebo použít zkompilované DLL z `bin/`).
- `appsettings.secrets.json` a connection stringy **nečíst, nekopírovat, necitovat**.
- Existující ruční testy (`test/Dynatech.Ulozna.Domain.Tests`, `test/Dynatech.Ulozna.Application.Tests`) jsou **baseline pro porovnání** – generátor je nesmí přepsat. Generované testy jdou do samostatné složky/projektu (viz §7).

Navržená struktura tohoto repozitáře:

```
/AGENTS.md
/README.md
/docs/
  thesis/            # text práce (kapitoly, poznámky z rešerše)
  decisions/         # ADR – jedno rozhodnutí = jeden soubor (0001-*.md)
  input-samples/     # ukázkové vstupy z EA (XMI) a textové analýzy
/src/
  TestGen.sln
  TestGen.Core/              # datový model: DomainModel, CodeModel, TestIntent
  TestGen.Input.EA/          # parser XMI exportu z Enterprise Architectu
  TestGen.Input.Text/        # parser méně strukturované analýzy (Markdown/YAML)
  TestGen.Analysis.Roslyn/   # načtení řešení, extrakce faktů z kódu
  TestGen.Linking/           # párování analýza ↔ kód, tvorba TestIntentů
  TestGen.Generation/        # generátory testů (profily konvencí), výstup C#
  TestGen.Llm/               # (volitelné) LLM obohacení nejednoznačných scénářů
  TestGen.Cli/               # konzolová aplikace – vstupní bod
/tests/
  TestGen.*.Tests/           # testy samotného nástroje (xUnit)
/experiments/
  ulozna/                    # konfigurace běhu, vygenerované výstupy, výsledky měření
```

> Současný `TestGenerator/` je šablona Roslyn **analyzeru** (netstandard2.0, `SampleSyntaxAnalyzer` apod.). Pro generátor testů je vhodnější **konzolová aplikace na .NET 8 s `MSBuildWorkspace`** (viz Rozhodnutí R1). Šablonu buď smazat, nebo ponechat jen jako budoucí rozšíření (analyzer/code-fix „vygeneruj test“ v IDE).

---

## 3. Architektura (pipeline)

```
 EA XMI ──► Input.EA ───┐
                        ├─► AnalysisModel ─┐
 Text/MD ─► Input.Text ─┘                  │
                                           ├─► Linking ─► TestIntent[] ─► Generation ─► *.cs + report
 .sln ───► Analysis.Roslyn ─► CodeModel ───┘        ▲
                                                    │
                                        Llm (volitelně, jen nejednoznačné případy)
```

Principy:

- **Modularita** – každý krok má vlastní projekt a rozhraní; vstupy i generátory jsou vyměnitelné (`IAnalysisSource`, `ICodeAnalyzer`, `IIntentBuilder`, `ITestGenerator`).
- **Deterministické jádro** – stejný vstup ⇒ stejný výstup. LLM je doplněk, ne základ (viz §8).
- **Mezivýstupy jsou serializovatelné (JSON)** – `analysis-model.json`, `code-model.json`, `test-intents.json`. Kvůli ladění, reprodukovatelnosti experimentů a přílohám práce.
- **Traceability** – každý vygenerovaný test nese odkaz na zdroj: element z EA (GUID) a/nebo symbol v kódu (`DocumentationCommentId`) + pravidlo, které ho vytvořilo.

---

## 4. Datový model (návrh, bude se upřesňovat)

### 4.1 AnalysisModel (z EA / textu)

- `DomainClass` – název, stereotyp (`Entity`, `AggregateRoot`, `ValueObject`, `DomainService`), atributy, asociace, EA GUID.
- `DomainAttribute` – název, typ, multiplicita (povinnost), tagged values (`maxLength`, `min`, `max`, `pattern`, …).
- `Constraint` – typ (`Invariant`, `Validation`, `Precondition`), text (OCL nebo přirozený jazyk), vazba na třídu/atribut.
- `StateMachine` – stavy, přechody (trigger, guard, efekt).
- `UseCase` – název, aktéři, hlavní a alternativní scénáře, pre/post-podmínky (pro fázi 2).
- `Confidence` / `SourceKind` – odkud informace pochází (strukturovaný element vs. volný text).

### 4.2 CodeModel (z Roslynu)

- `EntityInfo` – symbol, bázová třída (`Entity<T>`, `AggregateRoot<T>`, `BasicAggregateRoot<T>`), konstruktory (viditelnost!), settery, metody.
- `GuardInfo` – nalezená kontrola: `CustomCheck.NotNull(x, name, maxLength)`, `Check.*` (ABP), `if (...) throw new UserFriendlyException/BusinessException(...)`; podmínka, typ výjimky, zpráva/kód.
- `CollectionRule` – operace nad kolekcemi (`AddX` s kontrolou unikátnosti, `RemoveX` s kontrolou existence).
- `StateMutation` – metody měnící stav (co mění, jaké guardy předchází).
- `DomainServiceInfo` – `*Manager : IDomainService`, závislosti z konstruktoru (repozitáře, `ISequenceService<,>`, `IMediator`), volané metody entit.
- `ConstantRef` – vyhodnocené konstanty (`DokumentConsts.NazevMaxLength` → skutečné číslo přes sémantický model).

### 4.3 TestIntent (jádro práce)

```jsonc
{
  "id": "PolozkaSSP.TrvalySouhlas.OnlyForS",
  "kind": "Invariant",              // Validation | Invariant | StateTransition | CollectionRule | Construction | ...
  "target": { "symbol": "M:Dynatech.Ulozna.Entities.PolozkaSSP.SetSkartacniZnakWithTrvalySkartacniSouhlas(...)" },
  "sources": [
    { "type": "EA", "guid": "{...}", "text": "Trvalý souhlas lze jen u znaku S" },
    { "type": "Code", "rule": "GuardThrows", "location": "PolozkaSSP.cs:94" }
  ],
  "cases": [
    { "inputs": { "skartacniZnak": "S", "trvalySkartacniSouhlas": true },  "expect": "Ok" },
    { "inputs": { "skartacniZnak": "A", "trvalySkartacniSouhlas": true },  "expect": { "throws": "UserFriendlyException" } },
    { "inputs": { "skartacniZnak": "A", "trvalySkartacniSouhlas": false }, "expect": "Ok" }
  ],
  "status": "Confirmed",            // Confirmed (analýza i kód) | CodeOnly | AnalysisOnly | Ambiguous
  "generation": "Full"              // Full | Skeleton | Skip
}
```

Stavy párování jsou klíčové pro vyhodnocení:

- **Confirmed** – pravidlo je v analýze i v kódu → plný test.
- **AnalysisOnly** – pravidlo je v analýze, v kódu chybí → test, který **má selhat** (odhalený rozpor) nebo kostra s `Skip` a TODO.
- **CodeOnly** – v kódu je kontrola, analýza o ní mlčí → test podle kódu, v reportu označit.
- **Ambiguous** – nejasné mapování → kostra, případně LLM návrh.

---

## 5. Vstup z Enterprise Architectu

- Formát: **XMI 2.1 (UML 2.x)** export z EA (Publish → Export Package to XMI). Parser nad `System.Xml.Linq`, bez závislosti na EA API/COM.
- Mapované elementy: `uml:Class` (+ stereotypy), `ownedAttribute` (typ, multiplicita), tagged values (`xmi:Extension` / `thecustomprofile`), `ownedRule` / constraints (EA constraints typu *Invariant/Pre/Post*), `uml:StateMachine`, `uml:UseCase` (+ scénáře z EA extension), poznámky (`Note`).
- Párování s kódem: 1) tagged value `code` / `fqn`, pokud je; 2) jméno třídy/atributu (case-insensitive, **normalizace diakritiky** – EA může mít „Položka SSP“, kód `PolozkaSSP`); 3) fuzzy shoda → `Ambiguous`.
- **Dvě varianty vstupu pro experiment** (požadavek zadání):
  - **A – strukturovaná**: XMI s atributy, multiplicitami, tagged values (maxLength) a constrainty.
  - **B – málo strukturovaná**: stejný obsah jako volný text (poznámky v EA / Markdown dokument).
- Ukázkové vstupy pro Úložnu ukládat do `docs/input-samples/ulozna/` (A i B nad stejnými pravidly, aby šly porovnat).

---

## 6. Typy scénářů – fáze 1 (doménová vrstva)

Katalog pravidel = „rule“ v Linkingu. Každé pravidlo má ID, detektor v kódu, protějšek v analýze a šablonu testu.

| ID | Scénář | Detekce v kódu (Úložna) | Příklad |
|---|---|---|---|
| D1 | Povinnost a max. délka stringu | `CustomCheck.NotNull(value, nameof(X), MaxLength)` | `Dokument.SetNazev` – `DokumentConsts.NazevMaxLength` |
| D2 | Povinnost ne-string hodnoty | `CustomCheck.NotNull(value, name)` bez délky | `SetDatumVyrizeni` |
| D3 | Vztah mezi poli (cross-field) | `if (a > b) throw ...` | `SpisovyASkartacniPlan.SetPlatnost` – `PlatnyOd > PlatnyDo` |
| D4 | Podmíněné pravidlo | `if (flag && enum != X) throw ...` | `TrvalySkartacniSouhlas` jen pro `SkartacniZnak.S` (PolozkaSSP i Dokument) |
| D5 | Unikátnost v kolekci agregátu | `if (Coll.Any(x => x.K == item.K)) throw` | `SpisovyASkartacniPlan.AddPolozka` – unikátní `SpisovyZnak` |
| D6 | Odebrání neexistujícího prvku | `FirstOrDefault` + `null` → throw / `EntityNotFoundException` | `RemovePolozka`, `Dokument.GetSoubor/RemoveSoubor` |
| D7 | Konstrukce entity | konstruktor volá řetěz setterů | vytvoření validní entity + porovnání všech vlastností |
| D8 | Stavový přechod | metoda mění stav s guardem | `SpisovyASkartacniPlanManager.Ukoncit` (změna `PlatnyDo`) |
| D9 | Doménová služba | `*Manager : IDomainService` – mock závislostí | `Vytvorit` – kontrola překrytí intervalů (`CheckPrekrytiDatumu`) |

Hraniční hodnoty generovat systematicky: `null`, `""`, délka `max`, `max+1`; u intervalů `od == do`, `od > do`, `od < do`; u enumů všechny hodnoty.

Známé chyby/podezření v kódu, které mají testy odhalit (pro kapitolu vyhodnocení):

- `SetPlatnost`: `platnostDo != null` u `DateTime` (ne-nullable) je vždy `true`.
- `SpisovyASkartacniPlan.Polozky` není inicializováno v konstruktoru (`AddPolozka` na nové instanci → `NullReferenceException`), na rozdíl od `Dokument.Soubory`.
- `Dokument.AddSoubor` – zpráva obsahuje `${id}` (JS syntaxe v C# interpolaci).

---

## 7. Konvence generovaných testů (profil „Dynatech/Úložna“)

Generátor musí umět **profily konvencí**; první profil kopíruje styl existujících testů Úložny:

- xUnit (`[Fact]`, `[Theory]`, `[InlineData]`, `[MemberData]`), Shouldly, NSubstitute.
- Interní knihovna `TestHelper` (framework mono-repa): `PropertyTester.GetStringPropertyTestData`, `IFakeEntity<T>`, `TestSetProperty`, `CheckIfSame`.
- Fake entity (`FakeEntities/Fake<Entity>.cs`) + `RandomFakeEntityGenerator` + `Converter`/`Comparator` v `Dynatech.Ulozna.TestBase`.
- Konstruktory entit jsou často `internal` → testovací projekt spoléhá na `InternalsVisibleTo("Dynatech.Ulozna.Domain.Tests")`.
- Pojmenování: třída `<Entita>_Tests`, metody `Test_<Entita>_<Vlastnost>` / `<Akce>_<Entita>`; namespace `Dynatech.Ulozna`.
- Doménová jména (česky) zachovat; generované komentáře česky.

Výstup:

- Každý soubor začíná hlavičkou `// <auto-generated by TestGen>` + verze nástroje + ID intentů.
- Generuje se do `experiments/ulozna/out/<run-id>/` a pro ověření se kopíruje do **samostatného** projektu `Dynatech.Ulozna.Domain.GeneratedTests` (ne do ručních testů).
- Kromě plného profilu podporovat i **generický profil** (čistý xUnit + Shouldly bez interních knihoven) kvůli přenositelnosti.

---

## 8. Deterministický vs. hybridní (LLM) přístup

- Výchozí režim: **čistě deterministický** (pravidla + šablony). Musí fungovat bez sítě a bez LLM.
- LLM (`TestGen.Llm`, zapínat přepínačem `--llm`) jen pro:
  - převod volného textu (varianta B) na `Constraint` s vyšší jistotou,
  - `Ambiguous` párování analýza ↔ kód,
  - návrh těla testu tam, kde šablona nestačí (kostra → implementace).
- Každý LLM výstup: logovat prompt + odpověď do `experiments/.../llm-log/`, teplota 0, cache podle hashe vstupu, **validovat kompilací** (Roslyn `Compilation.GetDiagnostics()`) a označit v reportu jako `source: LLM`.
- Pro experiment porovnat tři režimy: deterministický / hybridní / „obecné AI bez doménového kontextu“ (Copilot apod.) + ruční testy.

---

## 9. Statická analýza – technické poznámky

- Načítání: `Microsoft.Build.Locator` + `MSBuildWorkspace.OpenSolutionAsync` (.NET 8). Logovat `workspace.WorkspaceFailed` – u mono-repa budou chybět reference, report je musí ukázat.
- Fallback: `CSharpCompilation` ze souborů `*.cs` + metadata reference na DLL z `bin/` (když MSBuild load selže).
- Používat **sémantický model** (`SemanticModel`, `ISymbol`) – ne jen textové hledání: rozpoznat `CustomCheck.NotNull` podle symbolu, vyhodnotit konstanty (`GetConstantValue`), bázové třídy ABP přes `INamedTypeSymbol.BaseType`.
- Detektory psát jako samostatné třídy (`IGuardDetector`), aby šel katalog pravidel rozšiřovat (ABP `Check.*`, FluentValidation, DataAnnotations, …).
- Verze Roslynu: `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.x (aktuální stabilní), sjednotit napříč projekty (Directory.Packages.props).

---

## 10. Vyhodnocení (experiment)

Metriky, které nástroj/skripty musí umět spočítat:

1. **Identifikace scénářů** – precision / recall vůči ručně sestavenému „gold“ seznamu scénářů (`experiments/ulozna/gold-scenarios.json`).
2. **Kompilovatelnost** – % vygenerovaných testovacích souborů/metod bez chyb kompilace.
3. **Spustitelnost** – % testů, které doběhnou; rozlišit pass / fail-očekávaný (odhalená chyba) / fail-chyba generátoru.
4. **Pokrytí** – coverlet (`dotnet test --collect:"XPlat Code Coverage"`), line + branch pro `Dynatech.Ulozna.Domain`.
5. **Mutation score** – Stryker.NET nad doménovým projektem.
6. **Ruční úpravy** – počet změněných řádků (diff) nutných k tomu, aby test prošel/byl akceptovatelný.

Porovnání: (a) ruční testy Úložny, (b) TestGen varianta A, (c) TestGen varianta B, (d) TestGen + LLM, (e) obecné AI bez kontextu. Výsledky do `experiments/ulozna/results/` (CSV + krátký komentář).

---

## 11. Pravidla pro agenta

- Komunikace a dokumentace **česky**; kód, identifikátory nástroje a commit zprávy **anglicky** (doménová jména z Úložny ponechat).
- Nesahat do složky Úložny (pouze čtení). Žádné tajné údaje do repozitáře.
- Před větší změnou: navrhnout postup, u architektonických rozhodnutí vytvořit ADR v `docs/decisions/`.
- Každý nový detektor / pravidlo = unit test v `tests/` nad malým vzorkem kódu (inline string → `CSharpSyntaxTree.ParseText`).
- Build a testy: `dotnet build src/TestGen.sln`, `dotnet test`. Před commitem musí projít.
- Běh nástroje (cílový tvar CLI):
  `dotnet run --project src/TestGen.Cli -- --solution <cesta.sln> --analysis <model.xmi|analyza.md> --layer domain --profile dynatech --out experiments/ulozna/out`
- Po dokončení kroku aktualizovat checklist v §12.

---

## 12. Roadmapa

- [ ] F0 – Rozhodnout osud šablony `TestGenerator/`, založit `src/TestGen.sln` se strukturou z §2
- [ ] F0 – Ověřit načtení Úložny přes `MSBuildWorkspace` (v kontextu celého mono-repa) a vypsat diagnostiku
- [ ] F1 – `TestGen.Core`: AnalysisModel, CodeModel, TestIntent + JSON serializace
- [ ] F1 – `Analysis.Roslyn`: detektory D1–D6 nad entitami Úložny
- [ ] F1 – Připravit EA model Úložny (varianta A) a textový popis (varianta B)
- [ ] F1 – `Input.EA`: parser XMI (třídy, atributy, tagged values, constrainty)
- [ ] F1 – `Linking`: párování + stavy Confirmed/CodeOnly/AnalysisOnly/Ambiguous
- [ ] F1 – `Generation`: profil „dynatech“ pro D1–D7, výstup + report
- [ ] F1 – Gold scénáře pro doménu Úložny, první měření (kompilace, pass, coverage)
- [ ] F2 – D8–D9 (stavové přechody, doménové služby s NSubstitute)
- [ ] F2 – Stryker.NET, porovnání s ručními testy a obecným AI
- [ ] F3 – Aplikační vrstva: use casy, `*AppService`, perzistence (ABP integrační testy, `UloznaApplicationTestBase`), AutoMapper profily
- [ ] F3 – `TestGen.Llm` hybridní režim

---

## 13. Rozhodnutí

- **R1** (návrh): Nástroj je konzolová aplikace .NET 8 s `MSBuildWorkspace`, ne Roslyn analyzer/source generator. Důvod: potřebujeme celé řešení, externí vstup (XMI) a zápis souborů mimo kompilaci. Analyzer může být pozdější rozšíření pro IDE.
- **R2** (návrh): Vstup z EA přes XMI 2.1 export, ne přes EA COM API (přenositelnost, běh bez EA).
- **R3** (návrh): Deterministické jádro, LLM volitelně (§8).

## 14. Otevřené otázky

- Máme k Úložně existující model v EA, nebo ho vytvoříme pro účely experimentu? Jakou verzi EA a jaký profil/stereotypy používáte?
- Jak se v EA zapisují validační pravidla – constrainty (OCL / text), tagged values, nebo poznámky?
- Je dostupný celý mono-repo `Dynatech.CFI` (kvůli referencím `framework/`, `SequencesManagement`, `TestHelper`)?
- Generovat do stylu `TestHelper`/FakeEntity, nebo začít generickým profilem a styl Úložny přidat později?
- Cílový .NET Úložny je `net8.0`, ABP 8.1.1 – nástroj držet na stejném SDK?
