# Dokument – porovnání EA modelu s kódem (ruční, slouží jako gold pro Linking)

Zdroj EA: `Dokument.ea.png`. Kód: `Ulozna/src/Dynatech.Ulozna.Domain/Entities/Dokument.cs`, konstanty `DokumentConsts`, `DynatechConsts`, EF konfigurace `UloznaDbContextModelCreatingExtensions.cs`.

| EA atribut | EA typ | Vlastnost v kódu | Kód (validace) | Shoda | Poznámka pro párování |
|---|---|---|---|---|---|
| kód účetní jednotky | string(10) | `KodUj` | NotNull, max `DynatechConsts.MAX_KOD_UJ_LENGTH` = 10 | ✅ | zkratka „UJ“ → glosář |
| id | int | `Id` | – (base ctor) | ✅ | |
| datum vyřízení | date | `DatumVyrizeni` | NotNull | ✅ | `date` → `DateTime` (EF `date`) |
| datum uložení | date | `DatumUlozeni` | NotNull | ✅ | |
| id dokladu | int | `IdDokladu` | NotNull | ✅ | |
| číslo dokladu | string | `CisloDokladu` | NotNull, max `MAX_CISLO_DOKLADU_CRO_LENGTH` = 50 | ⚠️ | EA neuvádí délku → CodeOnly (max 50) |
| typ dokumentu | int | `IdTypuDokumentu` | NotNull | ⚠️ | jméno se liší (implicitní „id“) |
| typ dokumentu - název | string(255) | `NazevTypuDokumentu` | max 255 | ✅ | přeskládané pořadí slov |
| typ dokumentu - označení | string(255) | `OznaceniTypuDokumentu` | max 255 | ✅ | přeskládané pořadí slov |
| název | string(1000) | `Nazev` | max 1000 | ✅ | |
| subjekt | string(500) | `Subjekt` | max 500 | ✅ | |
| id položky SSP | int [0..1] | `IdPolozkySSP` (`int?`) | – | ✅ | `[0..1]` → nullable |
| popis | string(4000) | `Popis` | max 4000 | ✅ | |
| spisový znak | string(30) | `SpisovyZnak` | max 30 | ✅ | |
| skartační znak | string(1) | `SkartacniZnak` (enum `SkartacniZnak`) | – | ⚠️ | typ se liší: EA string(1), kód enum → testovat hodnoty enumu |
| skartační lhůta | int | `SkartacniLhuta` | NotNull (int – neúčinné) | ✅ | |
| trvalý skartační souhlas | bool | `TrvalySkartacniSouhlas` | – | ✅ | |
| uložil | string(60) | `Ulozil` | max 60 | ✅ | |

## Constrainty

| EA constraint | Kód | Stav | Test |
|---|---|---|---|
| `{if trvalý-skart-souhlas == true then skartační-znak = S}` | `SetSkartacniZnakWithTrvalySkartacniSouhlas` – throw `UserFriendlyException` | **Confirmed** | unit (D4): všechny kombinace enum × bool |
| `{UQ: kód účetní jednotky, id dokladu}` | žádná kontrola v entitě ani unikátní index v EF konfiguraci | **AnalysisOnly** | integrační (F3): vložení duplicity musí selhat → dnes selže test = nalezený rozpor |

## Co z toho plyne pro nástroj

- Jména atributů v EA jsou česká slova s mezerami a diakritikou; kód je PascalCase bez diakritiky → normalizace + tokenizace.
- Potřeba **glosáře zkratek** (`účetní jednotka` → `Uj`, `skart` → `skartační`) a pravidla „FK bez `id`“ (`typ dokumentu` → `IdTypuDokumentu`).
- Pořadí tokenů se může lišit (`typ dokumentu - název` → `NazevTypuDokumentu`) → párovat jako množinu tokenů, ne řetězec.
- Constrainty jsou pseudo-OCL: `if A == true then B = S`, `UQ: a, b`. Stačí malá gramatika (implikace, rovnost, unikátnost), zbytek → Ambiguous/LLM.
- Jména v constraintu jsou zkrácená a s pomlčkami (`trvalý-skart-souhlas`) → párovat nejdřív na atributy téže třídy v EA, teprve pak na kód.
