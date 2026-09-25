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

Postup vývoje: nejdřív na **`TestGenerator.Sample`** (E-shop) (malé, veřejné NuGety, známý gold) → pak **Úložna** (reálný modul, privátní feed, `CustomCheck`) → pak další moduly CFI.

---

## 2. Repozitář a pracovní složky

| Cesta | Význam | Pravidla |
|---|---|---|
| `C:\Dimplomka` (tento repozitář) | Zdrojáky nástroje, dokumentace, experimenty | Sem se píše. |
| `C:\CFI - Mono` | Mono-repo CFI (ABP 8.1.1, .NET 8) – kontext pro reference (`framework/`, `SequencesManagement`, `TestHelper`) | **Pouze pro čtení.** Jeho `AGENTS.md`/`CLAUDE.md` neměnit. |
| `C:\CFI - Mono\Dynatech.CFI\src\modules\Ulozna` | **Testovací řešení** (`Dynatech.Ulozna.sln`) – první cíl nástroje | **Pouze pro čtení.** Nic neměnit, necommitovat. |

Poznámky k Úložně:

- Úložna je jen **první testovací řešení**. Cílem je nástroj použitelný pro **všechny ABP moduly** (v CFI je jich ~25 se stejnou strukturou vrstev) – nic nesmí být natvrdo pro Úložnu (viz §9a).
- Projekty referencují soubory **mimo modul** (`..\..\..\..\framework\...`, `..\..\..\SequencesManagement\...`, `common.props`). Proto se řešení modulu otevírá **na místě v mono-repu** (`C:\CFI - Mono\...`), ne jako kopie.
- Vygenerované testy, které mají skončit v CFI, musí dodržet pravidla z `C:\CFI - Mono\AGENTS.md` (viz §7).
- `appsettings.secrets.json` a connection stringy **nečíst, nekopírovat, necitovat**.
- Existující ruční testy (`test/Dynatech.Ulozna.Domain.Tests`, `test/Dynatech.Ulozna.Application.Tests`) jsou **baseline pro porovnání** – generátor je nesmí přepsat. Generované testy jdou do samostatné složky/projektu (viz §7).

Struktura tohoto repozitáře:

```
/AGENTS.md
/docs/
  thesis/            # text práce (kapitoly, poznámky z rešerše)
  decisions/         # ADR – jedno rozhodnutí = jeden soubor (0001-*.md)
  input-samples/     # vstupy z EA (přepisy diagramů, později XMI) – Úložna
/TestGenerator/
  TestGenerator.sln
  TestGenerator/             # jádro (netstandard2.0, Roslyn komponenta) – viz TestGenerator/Readme.md
    Model/                   #   AnalysisModel
    Analysis/                #   čtení *.analysis.json (později XMI, text)
    Naming/                  #   párování českých jmen analýzy na identifikátory
    Code/                    #   extrakce doménových typů a pravidel ze sémantického modelu
    Linking/                 #   TestIntent + stavy Confirmed/Conflict/AnalysisOnly/CodeOnly/Ambiguous
    Diagnostics/             #   analyzer → diagnostiky TG0001–TG0005 při buildu
    Generation/              #   TestPlan (plán testů) → profily (ITestProfile) → GeneratedFile
      Profiles/              #   generic (xUnit+Shouldly), fakeentity (TestHelper)
  TestGenerator.Cli/         # `testgen` – MSBuildWorkspace, zápis testů + testgen.manifest.json
  TestGenerator.Sample/      # ukázková ABP doména E-shop (3 agregáty) + analysis/ (A, B, gold)
  TestGenerator.Sample.Tests/# cíl generování (Generated/Generic, Generated/FakeEntity); TestHelper přes $(CfiRoot)
  TestGenerator.Tests/       # testy jádra (xUnit + Shouldly, in-memory kompilace se stuby ABP)
/experiments/
  ulozna/                    # konfigurace běhu, vygenerované výstupy, výsledky měření
```

Cílové rozšíření (až bude potřeba): `TestGenerator.Llm` pro hybridní režim.

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
- **AnalysisOnly** – pravidlo je v analýze, v kódu chybí → **plný test podle analýzy, který padá**, dokud se kód neopraví (ne `Skip`, ne TODO).
- **Conflict** – analýza i kód pravidlo mají, ale liší se (délka, povinnost, typ) → test podle **analýzy**, padá.
- **CodeOnly** – v kódu je kontrola, analýza o ní mlčí → test podle kódu (charakterizační, prochází), v reportu jako podnět pro analytika.

**Zdroj pravdy je analýza (EA)** – viz R7. Moduly nejsou dokončené, padající test = nedodělek/chyba v kódu, ne chyba generátoru.

Pravidla pro padající testy:

- Vygenerovaný projekt se musí **vždy zkompilovat**. Chybí-li v kódu člen z analýzy (např. `Soubor.BlobJeUlozen`), test na něj nesmí způsobit chybu kompilace → ověření přes reflexi/`dynamic` (`typeof(Soubor).GetProperty(...).ShouldNotBeNull()`), případně test přes existující API, který selže za běhu.
- Každý test nese `[Trait("TestGenStatus", "Confirmed|AnalysisOnly|Conflict|CodeOnly")]` → v reportu i v CI jde odlišit „známé mezery vůči specifikaci“ od regresí.
- Zpráva selhání odkazuje na pravidlo z analýzy (ID intentu + text constraintu), aby vývojář věděl, co opravit.
- Jakmile kód pravidlo splní, test začne procházet bez zásahu – nový běh generátoru ho nepřegeneruje jinak (stav se v manifestu přepne na `Confirmed`).
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

### 5.1 Notace EA v Dynatechu (zjištěno z diagramu *Úložna - Domain model*)

Zdroj: `docs/input-samples/ulozna/variant-A/Ulozna-DomainModel.ea.png`, přepis `Ulozna.analysis.json`, gold porovnání s kódem `Ulozna.mapping.md` (soubory `Dokument.*` jsou starší dílčí verze). XMI export zatím není k dispozici → do té doby se pracuje s ručně přepsaným JSON ve formátu AnalysisModel (parser XMI se pak jen „přišroubuje“ před něj).

- Atributy: české názvy s mezerami a diakritikou (`kód účetní jednotky`, `typ dokumentu - název`).
- Typy: `string(n)` = max. délka, `string` bez délky = délka neznámá, `date`, `int`, `bool`; multiplicita `[0..1]` = nepovinné (→ `int?`).
- Constrainty v sekci *constraints* jako pseudo-OCL ve složených závorkách:
  - implikace `{if <atr> == <hodnota> then <atr> = <hodnota>}` – jména atributů zkrácená a s pomlčkami (`trvalý-skart-souhlas`),
  - unikátní klíč `{UQ: <atr>, <atr>}`.
- Rozdíly vůči kódu, se kterými musí Linking počítat: zkratky (`účetní jednotka` → `Uj`), FK bez slova „id“ (`typ dokumentu` → `IdTypuDokumentu`), přeházené pořadí slov (`typ dokumentu - název` → `NazevTypuDokumentu`), jiný typ (`skartační znak: string(1)` × enum `SkartacniZnak`).
- Stereotypy: `«enumeration»` (literály `KÓD - popis`, např. `A - archivace`), `«conceptual»` = jen pojem, **neřešit**.
- Prefix `CRO:` (obecně `<Systém>:`) = třída externího systému → neřešit, jen jako cíl „weak ref“ (závislost s popiskem `weak ref: …`).
- Vazby: asociace s multiplicitami (`1 → 1..*`, `0..* → 0..1`), kompozice (◆ = hranice agregátu), agregace, závislost (weak ref). Šipky jsou navigační.
- Poznámky obsahují otevřené otázky analytika („metadata?“, „hash nebo p7s?“) → do reportu, ne do testů.
- Default hodnoty: `bool = false`.
- Párování jmen: normalizace diakritiky → tokenizace → porovnání **množin tokenů** + **glosář** (per organizace/modul, viz §9a).
- `UQ` constrainty patří do perzistence (integrační testy, F3) – u `Dokument` v kódu ani v EF konfiguraci unikátní index není ⇒ `AnalysisOnly`.
- **Klíčové zjištění:** doménový diagram popisuje hlavně strukturu. Ze 16 pravidel v `Ulozna.mapping.md` §3 je v EA jen 8; chování (unikátní spisový znak v plánu, od ≤ do, nepřekrývání plánů, AddSoubor/RemoveSoubor) je jen v kódu (`CodeOnly`). Varianta B (text/use casy) musí dodat hlavně pravidla chování.
- Enumy `SkartacniZnak`, `TypSouboru` jsou z privátního NuGetu `Dynatech.Shared.ULO` → sémantický model vyžaduje restore z privátního feedu (jinak fallback na literály z EA).

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
- EA `{UQ: kód účetní jednotky, id dokladu}` nemá v kódu ani v EF konfiguraci protějšek (chybí unikátní index).
- EA `číslo dokladu: string` bez délky, kód má max 50 (`MAX_CISLO_DOKLADU_CRO_LENGTH`) – nekonzistence dokumentace.

---

## 7. Konvence generovaných testů – profily

Generátor má **vyměnitelné profily** (`ITestProfile`, registrace v `ProfileRegistry`, volba `--profile a,b|all`). Všechny profily vycházejí ze stejného `EntityTestPlan` (konstruktor, parametry, pravidla, vzorové hodnoty) – liší se jen výstupem.

| Profil | Výstup | Závislosti |
|---|---|---|
| `generic` | `Generic/<Entita>Tests.cs` – vnořená třída `Data` s platnými hodnotami, `[Theory]` s délkami (`-1` = null) | xUnit, Shouldly |
| `fakeentity` | `FakeEntity/FakeEntities/Fake<Entita>.cs` (`IFakeEntity<T>`, konstanty `*MaxLength`, `CreateOriginalEntity`, `CheckIfSame`), `Generators/RandomFakeEntityGenerator.cs`, `<Entita>_Tests.cs` s `PropertyTester` + `TestSetProperty<T, TException>` | TestHelper (vlastní, v CFI `src/framework/TestHelper`), xUnit, Shouldly |
| *(další)* | např. `dynatech-ulozna` (Converter/Comparator jako partial třídy, reuse existujících Fake), NUnit, FluentAssertions | – |

Společná pravidla pro všechny profily:

- Platné hodnoty: řetězec podle tvaru z regexu v kódu (`Email`, `Digits{n}`, `[A-Z…]+`, jinak `A…`), Guid/čísla/datum konstantní (`generic`) nebo z `RandomGenerator` (`fakeentity`), enum = první hodnota, hodnotový objekt/entita = rekurzivně přes její konstruktor.
- Cesta testu: přes konstruktor (parametr se jménem vlastnosti), jinak přes jednoparametrovou metodu, ve které je kontrola (např. `Zrusit(duvod)`).
- Očekávaná výjimka: `Check.*` → `ArgumentException`, `CustomCheck.*` → `UserFriendlyException`, bez pravidla v kódu → `Exception`.
- `Ambiguous` / neznámý formát → `[Fact(Skip = "…")]` s důvodem (kandidát na LLM).
- Chybějící člen (`AnalysisOnly`) → test přes reflexi (projekt se vždy zkompiluje).

Profil `fakeentity` kopíruje styl existujících testů Úložny:

- xUnit (`[Fact]`, `[Theory]`, `[InlineData]`, `[MemberData]`), Shouldly, NSubstitute.
- Interní knihovna `TestHelper` (framework mono-repa): `PropertyTester.GetStringPropertyTestData`, `IFakeEntity<T>`, `TestSetProperty`, `CheckIfSame`.
- Fake entity (`FakeEntities/Fake<Entity>.cs`) + `RandomFakeEntityGenerator` + `Converter`/`Comparator` v `Dynatech.Ulozna.TestBase`.
- Konstruktory entit jsou často `internal` → testovací projekt spoléhá na `InternalsVisibleTo("Dynatech.Ulozna.Domain.Tests")`.
- Pojmenování: třída `<Entita>_Tests`, metody `Test_<Entita>_<Vlastnost>` / `<Akce>_<Entita>`; namespace `Dynatech.Ulozna`.
- Doménová jména (česky) zachovat; generované komentáře česky.

Výstup:

- **CFI zakazuje komentáře v kódu** → traceability se nedává do komentářů, ale do atributu `[Trait("TestGen", "<intent-id>")]` u každého testu a do sidecar souboru `testgen.manifest.json` (intent → test → zdroj v EA/kódu, verze nástroje, hash vstupů). Generický profil může mít hlavičku `// <auto-generated>`.
- Vygenerovaný projekt je oddělený od ručních testů; v CI se padající testy `AnalysisOnly`/`Conflict` odfiltrují traitem (R8), takže neblokují PR.
- Commity do CFI dělá člověk podle pravidel CFI (`#NNNN feat: ...`, jen předmět, žádné AI trailery). Nástroj do CFI necommituje.
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

## 9a. Nasazení do všech ABP modulů (přenositelnost)

Cíl: generátor jde „zapnout“ v libovolném ABP modulu bez úprav kódu nástroje.

- **Distribuce**: `dotnet tool` (balíček `Dynatech.TestGen`, příkaz `dotnet testgen`), instalovatelný jako lokální nástroj do `.config/dotnet-tools.json` modulu (Úložna už tento soubor má) nebo globálně. Vnitřní feed Azure DevOps.
- **Autodetekce vrstev ABP** podle konvence a sémantiky, ne podle cest: projekty `*.Domain`, `*.Domain.Shared`, `*.Application(.Contracts)`, `*.EntityFrameworkCore`, testovací `*.Domain.Tests`, `*.Application.Tests`, `*.TestBase`; potvrzení přes třídu `: AbpModule` a `[DependsOn]`.
- **Konfigurace per modul**: volitelný `testgen.json` vedle `.sln` (přepisuje výchozí hodnoty):
  ```jsonc
  {
    "solution": "Dynatech.Ulozna.sln",
    "analysis": ["docs/ea/Ulozna.xmi"],
    "profile": "dynatech",
    "layers": ["domain"],
    "glossary": "../../../testgen.glossary.json",
    "output": { "mode": "project", "project": "test/Dynatech.Ulozna.Domain.GeneratedTests" }
  }
  ```
- **Organizační profil** (`dynatech`) sdílený pro všechny moduly: znalost `CustomCheck`, `UserFriendlyException`, `ISequenceService`, `TestHelper`/FakeEntity, glosář zkratek. Profily jsou data + pluginy, ne `if (modul == "Ulozna")`.
- **Režimy běhu**: `generate` (zapíše testy), `check` (nic nezapisuje, vrací nenulový exit kód při rozdílu analýza ↔ kód nebo zastaralých vygenerovaných testech – pro CI), `report` (jen `test-intents.json` + HTML/MD report).
- **Idempotence**: opakovaný běh bez změny vstupů nemění soubory; ručně upravené testy se nepřepisují (hash v manifestu).
- Ověření přenositelnosti v práci: po Úložně spustit na 1–2 dalších modulech CFI bez změny kódu nástroje (jen `testgen.json`).

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
- Každý nový detektor / pravidlo = unit test v `TestGenerator.Tests` nad malým vzorkem kódu (`TestCompilation.Create(source)` se stuby ABP).
- Jádro (`TestGenerator/TestGenerator`) je **netstandard2.0** (podmínka pro Roslyn komponenty): žádné `record`/`init`, `ToHashSet`, `string.Contains(char)` apod.; Roslyn `Microsoft.CodeAnalysis.CSharp` 4.3.0; JSON přes `System.Text.Json` (PrivateAssets).
- Build a testy: `dotnet build TestGenerator/TestGenerator.sln`, `dotnet test TestGenerator/TestGenerator.sln`. Před commitem musí projít.
- Zapojení do cílového projektu: `ProjectReference` na `TestGenerator.csproj` s `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` + `<AdditionalFiles Include="…/*.analysis.json" />`.
- Generování (z adresáře repozitáře): `dotnet run --project TestGenerator/TestGenerator.Cli -- --project <domain.csproj> --analysis <x.analysis.json> --profile generic,fakeentity --namespace <Ns>.Tests --out <testovací projekt>/Generated` (podrobně `TestGenerator.Sample.Tests/README.md`). `Generated/` je celé generované – ručně neupravovat.
- CLI (`TestGenerator.Cli`) je net10 s Roslyn 4.14 (`MSBuildWorkspace`); jádro zůstává na Roslyn 4.3 (kvůli analyzeru) – API jádra držet kompatibilní s 4.3.
- `TestGenerator/Directory.Build.rsp` přepíná restore na nuget.org (`RestoreSources`). TestHelper leží v CFI, jehož `NuGet.Config` obsahuje privátní feedy s ověřením (`%PAT_TOKEN%`); TestHelper sám potřebuje jen veřejné balíčky. Rider/VS `.rsp` nečtou → v IDE nastavit `PAT_TOKEN` nebo jednou `dotnet restore`.
- Ladění analyzeru: profil `DebugRoslynAnalyzers` (`TestGenerator/Properties/launchSettings.json`) cílí na `TestGenerator.Sample`.
- Po dokončení kroku aktualizovat checklist v §12.

---

## 12. Roadmapa

- [x] F0 – Ukázkový vstup: celý EA doménový model Úložny + ruční přepis do AnalysisModel + gold mapování (`docs/input-samples/ulozna/variant-A/Ulozna.*`)
- [x] F0 – Ukázková doména E-shop v `TestGenerator.Sample` (Zákazník+Adresa, Produkt+Manager, Objednávka+Položky) s analýzou A/B a gold (31 pravidel, 4 záměrně padající)
- [x] F0 – Šablona analyzeru vyčištěna, jádro v `TestGenerator` (R1)
- [x] F1 – Model analýzy + čtení JSON, párování jmen, extrakce textových pravidel (`Check.*`, `CustomCheck.*`, vlastní guardy), linker, analyzer TG0001–TG0005, testy jádra
- [x] F1 – Build ověřen: Sample hlásí TG0002 (Zakaznik.Email 256×200) a TG0003 (Produkt.hmotnost) dle očekávání
- [ ] F1 – Ověřit `dotnet test TestGenerator/TestGenerator.sln` (NameMatcherTests, LinkerTests)
- [ ] F0 – Ověřit načtení Úložny přes `MSBuildWorkspace` (v kontextu celého mono-repa) a vypsat diagnostiku
- [ ] F1 – Serializace TestIntentů do JSON (report)
- [ ] F1 – Detektory: číselné rozsahy, regex/formát, kolekce (unikátnost, max. počet), odebrání neexistujícího, stavové přechody (matice stav × akce), doménové služby
- [x] F1 – Generování testů: `EntityTestPlan`, profily `generic` a `fakeentity`, CLI `testgen`, projekt `TestGenerator.Sample.Tests`, testy generování (kompilace vygenerovaného kódu)
- [x] F1 – Ověřeno 27. 9. 2026: jádro 27/27; CLI nad Sample → 12 záměrů, 16 souborů; `TestGenerator.Sample.Tests` 70 testů = 62 ✓, 4 skip (Ambiguous IČO/PSČ), 4 ✗ očekávaně (E02, E15 v obou profilech)
- [ ] F1 – Profil `fakeentity` nad Úložnou: znovupoužít existující `Fake*`/`Converter`/`Comparator` místo generování
- [ ] F1 – `Analysis.Roslyn`: detektory D1–D6 nad entitami Úložny
- [ ] F1 – Připravit EA model Úložny (varianta A) a textový popis (varianta B)
- [ ] F1 – `Input.EA`: nejdřív loader AnalysisModel JSON, pak parser XMI (třídy, atributy, constrainty)
- [ ] F1 – Parser pseudo-OCL constraintů (`if … then …`, `UQ: …`) + glosář a párování jmen
- [ ] F1 – Připravit variantu B (textový popis pravidel P1–P16) do `docs/input-samples/ulozna/variant-B/`
- [ ] F1 – `Linking`: párování + stavy Confirmed/CodeOnly/AnalysisOnly/Ambiguous
- [ ] F1 – `Generation`: profil „dynatech“ pro D1–D7, výstup + report
- [ ] F1 – Gold scénáře pro doménu Úložny, první měření (kompilace, pass, coverage)
- [ ] F2 – D8–D9 (stavové přechody, doménové služby s NSubstitute)
- [ ] F2 – Stryker.NET, porovnání s ručními testy a obecným AI
- [ ] F3 – Aplikační vrstva: use casy, `*AppService`, perzistence (ABP integrační testy, `UloznaApplicationTestBase`), AutoMapper profily
- [ ] F3 – `TestGen.Llm` hybridní režim
- [ ] F4 – Balíček `dotnet tool`, `testgen.json`, režim `check` do CI, ověření na dalších modulech CFI

---

## 13. Rozhodnutí

- **R1** (revidováno): Jádro je Roslyn komponenta v projektu `TestGenerator` (netstandard2.0) s API nad `Compilation` + `AnalysisModel` → `TestIntent`. První hostitel je **analyzer** (diagnostiky shody analýzy a kódu při každém buildu, v IDE i v CI). Zápis testových souborů analyzer neumí → druhý hostitel (CLI s `MSBuildWorkspace` nebo code fix) nad stejným jádrem.
- **R2** (návrh): Vstup z EA přes XMI 2.1 export, ne přes EA COM API (přenositelnost, běh bez EA).
- **R3** (návrh): Deterministické jádro, LLM volitelně (§8).
- **R4** (návrh): Distribuce jako `dotnet tool` + `testgen.json` per modul, organizační profil sdílený (§9a).
- **R5**: Traceability generovaných testů přes `[Trait]` + `testgen.manifest.json`, ne komentáři (CFI zakazuje komentáře v kódu).
- **R7**: **Analýza (EA) je specifikace.** Při rozporu analýza × kód se test generuje podle analýzy a má padat, dokud se kód neopraví (moduly nejsou dokončené). Kód je zdroj pravdy jen tam, kde analýza mlčí (`CodeOnly`).
- **R8**: **CI filtruje podle traitu.** Vygenerované testy jsou v PR pipeline CFI, ale blokující běh vylučuje známé mezery vůči specifikaci:
  `dotnet test ... --filter "TestGenStatus!=AnalysisOnly&TestGenStatus!=Conflict"`.
  Volitelně druhý, neblokující krok (`continueOnError: true`) spustí jen `TestGenStatus=AnalysisOnly|TestGenStatus=Conflict` a publikuje je jako přehled nedodělků. Úpravu `build/pipeline-pr-test.yml` dělá tým CFI, ne nástroj. Název traitu bez tečky (`TestGenStatus`), aby šel použít ve filtru `dotnet test`.
- **R6**: Dokud není XMI export, vstupem je ručně přepsaný AnalysisModel JSON; parser XMI bude jen další `IAnalysisSource`.

## 14. Otevřené otázky

- ~~Existuje model Úložny v EA?~~ Ano (diagram `Dokument`); XMI export zatím nejde – kdy bude? Verze EA?
- ~~Jak se zapisují pravidla?~~ Pseudo-OCL constrainty ve třídě (§5.1). Používají se i tagged values / stereotypy (Entity, AggregateRoot) nebo stavové diagramy?
- Kde leží zdroj varianty B (málo strukturovaná analýza) – Word/Confluence/DevOps wiki?
- ~~P4 / „blob je uložen“ – chyba, nebo záměr?~~ Analýza platí (R7) → padající testy.
- ~~Padající vygenerované testy v CI?~~ Filtrovat podle traitu (R8).
- Existuje firemní glosář zkratek (UJ, SSP, CRO, …), který můžeme použít jako vstup?
- Generovat do stylu `TestHelper`/FakeEntity, nebo začít generickým profilem a styl Úložny přidat později?
- Cílový .NET Úložny je `net8.0`, ABP 8.1.1 – nástroj držet na stejném SDK?
