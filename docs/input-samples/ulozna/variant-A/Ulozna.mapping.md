# Úložna – porovnání EA doménového modelu s kódem (gold pro Linking)

Zdroj EA: `Ulozna-DomainModel.ea.png` (diagram *class Úložna - Domain model*), přepis `Ulozna.analysis.json`.
Kód: `Ulozna/src/Dynatech.Ulozna.Domain/Entities/*.cs`, `Domain.Shared/Conts/*Consts.cs`, `DynatechConsts` (framework), EF `UloznaDbContextModelCreatingExtensions.cs`.
Enumy `SkartacniZnak`, `TypSouboru` jsou z privátního NuGetu `Dynatech.Shared.ULO` → literály ověřit až sémantickým modelem po restore.

Legenda: ✅ shoda · ⚠️ rozdíl (typ/jméno/povinnost) · ❌ chybí na jedné straně

**Analýza je specifikace (AGENTS.md R7):** `AnalysisOnly` a `Conflict` → test podle EA, který padá, dokud se kód neopraví. `CodeOnly` → charakterizační test podle kódu (prochází).

## 1. Třídy a rozsah

| EA prvek | Kód | Zpracovat? |
|---|---|---|
| Dokument | `Dokument : BasicAggregateRoot<int>` | ano |
| Soubor | `Soubor : Entity<int>` (v agregátu Dokument) | ano |
| Položka SSP | `PolozkaSSP : Entity<int>` (v agregátu SSP) | ano |
| Spisový a skartační plán | `SpisovyASkartacniPlan : BasicAggregateRoot<int>` | ano |
| Typ dokumentu | `TypDokumentu : Entity<int>` | ano |
| «enumeration» Skartační znak (A/S/V) | `Dynatech.Shared.ULO.Enum.SkartacniZnak` | ano (hodnoty pro testy) |
| «enumeration» Typ souboru (HD/AS/PR) | `Dynatech.Shared.ULO.Enum.TypSouboru` | ano |
| `CRO:Doklad`, `CRO:Priloha`, `CRO:TypDokumentu`, `CRO:Způsob přesunu…` | – (externí systém CRO) | **ne** – prefix `CRO:` = cizí systém |
| Blob Storage | – (infrastruktura) | ne |
| «conceptual» Spisovna, Archiv, Úložna, … | – | ne (glosář pojmů) |
| poznámky („metadata?“, „hash nebo p7s?“, propočet kapacity) | – | ne → report jako otevřené otázky analýzy |

Pravidla, která z toho plynou pro `Input.EA`: ignorovat stereotyp `«conceptual»`, prvky s prefixem `<Systém>:` brát jako externí (jen pro weak ref), poznámky nepřevádět na scénáře.

## 2. Atributy

### Dokument

| EA atribut | EA typ | Kód | Validace v kódu | Stav |
|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max 10 (`DynatechConsts.MAX_KOD_UJ_LENGTH`) | ✅ |
| id | int | `Id` | – | ✅ |
| datum vyřízení / uložení | date | `DatumVyrizeni` / `DatumUlozeni` | NotNull | ✅ |
| id dokladu | int | `IdDokladu` | NotNull | ✅ |
| číslo dokladu | string | `CisloDokladu` | NotNull, max 50 (`MAX_CISLO_DOKLADU_CRO_LENGTH`) | ⚠️ EA bez délky → CodeOnly |
| typ dokumentu | int | `IdTypuDokumentu` | NotNull | ⚠️ jméno (FK bez „id“) |
| typ dokumentu - název / - označení | string(255) | `NazevTypuDokumentu` / `OznaceniTypuDokumentu` | max 255 | ✅ (přeházená slova) |
| název | string(1000) | `Nazev` | max 1000 | ✅ |
| subjekt | string(500) | `Subjekt` | max 500 | ✅ |
| id položky SSP | int [0..1] | `IdPolozkySSP` (`int?`) | – | ✅ |
| popis | string(4000) | `Popis` | max 4000 | ✅ |
| spisový znak | string(30) | `SpisovyZnak` | max 30 | ✅ |
| skartační znak | string(1) | `SkartacniZnak` (enum) | – | ⚠️ typ; literály z EA enumu |
| skartační lhůta | int | `SkartacniLhuta` | NotNull (u int bez efektu) | ✅ |
| trvalý skartační souhlas | bool | `TrvalySkartacniSouhlas` | – | ✅ |
| uložil | string(60) | `Ulozil` | max 60 | ✅ |

### Soubor

| EA atribut | EA typ | Kód | Validace v kódu | Stav |
|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max 10 (setter `protected`) | ✅ testovat jen přes ctor |
| id | int | `Id` | – | ✅ |
| typ | string(2) | `Typ` (enum `TypSouboru`) | – | ⚠️ typ; EA enum Typ souboru HD/AS/PR |
| id dokumentu | int | `IdDokumentu` | – (setter `protected`) | ✅ |
| BlobKey | string(256) | `BlobKey` | NotNull, max 256 | ✅ |
| název | string(255) | `Nazev` | NotNull, max 255 | ✅ |
| název souboru | string(255) | `NazevSouboru` (`string?`) | jen délka (`CustomCheck.Length`) | ⚠️ **Conflict** – EA povinné, kód nullable → test `null` musí vyhodit, padá |
| blob je uložen | bool = false | – | – | ❌ **AnalysisOnly** – test přes reflexi (existence + default `false`), padá |

### Položka SSP

| EA atribut | EA typ | Kód | Validace v kódu | Stav |
|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max 10 | ✅ |
| id | int | `Id` | – | ✅ |
| id spisového a skartačního plánu | int | `IdSpisovehoASkartacnihoPlanu` | – | ✅ |
| spisový znak | string(30) | `SpisovyZnak` | NotNull, max 30 | ✅ |
| název | string(300) | `Nazev` | NotNull, max 300 | ✅ |
| skartační znak | string(1) | `SkartacniZnak` (enum) | – | ⚠️ typ |
| skartační lhůta | int | `SkartacniLhuta` | NotNull | ✅ |
| trvalý skartační souhlas | bool | `TrvalySkartacniSouhlas` | – | ✅ |

### Spisový a skartační plán

| EA atribut | EA typ | Kód | Validace v kódu | Stav |
|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max 10 | ✅ |
| id | int | `Id` | – | ✅ |
| platný od / platný do | date | `PlatnyOd` / `PlatnyDo` | NotNull; od ≤ do | ✅ (pravidlo viz §3) |
| vytvořil | string(60) | `Vytvoril` | NotNull, max 60 | ✅ |

### Typ dokumentu

| EA atribut | EA typ | Kód | Validace v kódu | Stav |
|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max 10 | ✅ |
| id | int | `Id` | – | ✅ |
| id položky ssp | int | `IdPolozkySSP` | – | ✅ |
| id typu dokumentu CRO | int | `IdTypuDokumentu` | – | ⚠️ jméno (token „CRO“ v kódu chybí) |
| název / označení | string(255) | `Nazev` / `Oznaceni` | NotNull, max 255 | ✅ |

## 3. Pravidla (constrainty, vazby, pravidla jen v kódu)

| # | Pravidlo | EA | Kód | Stav | Vrstva testu |
|---|---|---|---|---|---|
| P1 | Trvalý souhlas jen pro skartační znak S – **Dokument** | constraint `{if … then skartační-znak = S}` | `Dokument.SetSkartacniZnakWithTrvalySkartacniSouhlas` | **Confirmed** | unit |
| P2 | Trvalý souhlas jen pro S – **Položka SSP** | ❌ (constraint je jen u Dokument a CRO:Doklad) | `PolozkaSSP.SetSkartacniZnakWithTrvalySkartacniSouhlas` | **CodeOnly** | unit |
| P3 | Unikátní (kód UJ, id dokladu) u Dokument | `{UQ: …}` | ❌ ani entita, ani EF index | **AnalysisOnly** | integrační |
| P4 | Dokument má 1..* Souborů | multiplicita `1 → 1..*` | ❌ ctor vytvoří prázdnou kolekci, `RemoveSoubor` smí odebrat poslední | **AnalysisOnly** – invariant (potvrzeno), testy: odebrání posledního souboru musí selhat; padá | unit |
| P5 | Soubor s daným id jen jednou v Dokumentu | ❌ | `Dokument.AddSoubor` | **CodeOnly** | unit |
| P6 | Odebrání/získání neexistujícího Souboru | ❌ | `GetSoubor` → `EntityNotFoundException` | **CodeOnly** | unit |
| P7 | Plán vlastní položky (kompozice 1 ◆→ 0..*) | kompozice | `SpisovyASkartacniPlan.Polozky`, `AddPolozka`/`RemovePolozka` | **Confirmed** (struktura) | unit |
| P8 | Unikátní spisový znak v rámci plánu | ❌ | `AddPolozka` | **CodeOnly** | unit |
| P9 | Odebrání neexistující položky | ❌ | `RemovePolozka` | **CodeOnly** | unit |
| P10 | Platný od ≤ platný do | ❌ | `SetPlatnost` | **CodeOnly** | unit |
| P11 | Plány stejné UJ se nesmí časově překrývat | ❌ | `SpisovyASkartacniPlanManager.CheckPrekrytiDatumu` | **CodeOnly** | unit (Manager + mock repo) |
| P12 | Ukončení plánu (změna `PlatnyDo`) | ❌ | `SpisovyASkartacniPlanManager.Ukoncit` | **CodeOnly** | unit (Manager) |
| P13 | Dokument → Položka SSP 0..1 | `0..* → 0..1` | `IdPolozkySSP int?` | **Confirmed** | unit (null i hodnota) |
| P14 | Položka SSP → Typ dokumentu 1 → 0..* | asociace | `TypDokumentu.IdPolozkySSP` (int, povinné) | **Confirmed** | – (strukturální) |
| P15 | Soubor má příznak „blob je uložen“ (default false) | atribut | ❌ | **AnalysisOnly** – padá | unit (reflexe) |
| P16 | Povinnost/délky atributů (všechny `string(n)`) | typy | `CustomCheck.NotNull/Length` | **Confirmed** (viz §2) | unit (D1/D2) |

Souhrn: z EA varianty A je přímo odvoditelných jen **P1, P3, P4, P7, P13–P16**; většina chování (P2, P5, P6, P8–P12) je jen v kódu. To je argument do práce: doménový model v EA popisuje hlavně **strukturu**, ne chování → varianta B (textová analýza / use casy) musí dodat pravidla typu „plány se nepřekrývají“.

## 4. Chyby / podezření v kódu (pro vyhodnocení)

- `SpisovyASkartacniPlan.Polozky` se v konstruktoru neinicializuje → `AddPolozka` na nové instanci = `NullReferenceException`.
- `SetPlatnost`: `platnostDo != null` u ne-nullable `DateTime` je vždy `true`.
- `Dokument.AddSoubor`: `${id}` ve zprávě (JS syntaxe).
- P3 – chybí unikátní index (kód UJ, id dokladu).
- P4 – dokument bez souborů je v kódu možný (potvrzeno jako chyba – analýza platí).
- P15 – chybí `Soubor.BlobJeUlozen`.
- `Soubor.NazevSouboru` je nullable, EA ho má povinný.

## 5. Závěry pro nástroj (párování jmen)

- Normalizace: odstranit diakritiku, rozdělit na tokeny (mezery, pomlčky, PascalCase), malá písmena.
- Porovnávat **množiny tokenů** (`typ dokumentu - název` ~ `NazevTypuDokumentu`).
- Skloňování: `typ dokumentu` ↔ `TypuDokumentu`, `plánu` ↔ `Planu` → porovnávat **kmeny** (jednoduchý stemmer / prefix ≥ 4 znaky).
- Glosář (per organizace): `kód účetní jednotky` → `KodUj`, `SSP` → `spisový a skartační plán`, zkratky v constraintech (`skart` → `skartační`).
- Token označující externí systém (`CRO`) smí v kódu chybět.
- FK: EA `typ dokumentu: int` ↔ kód `IdTypuDokumentu` (implicitní prefix `Id`).
- Typ `string(n)` v EA + enumerace se shodným jménem v modelu ↔ enum v kódu → testovat všechny literály.
