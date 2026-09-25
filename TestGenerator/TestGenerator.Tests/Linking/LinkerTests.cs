using Shouldly;
using TestGenerator.Analysis;
using TestGenerator.Code;
using TestGenerator.Linking;
using TestGenerator.Tests.Infrastructure;
using Xunit;

namespace TestGenerator.Tests.Linking;

public class LinkerTests
{
    private const string Domain = """
        using System;
        using System.Text.RegularExpressions;
        using Volo.Abp;
        using Volo.Abp.Domain.Entities;

        namespace Demo;

        public static class ZakaznikConsts
        {
            public const int JmenoMaxLength = 200;
            public const int EmailMaxLength = 200;
        }

        public class Zakaznik : AggregateRoot<Guid>
        {
            public string Jmeno { get; private set; } = null!;
            public string Email { get; private set; } = null!;
            public string? Ico { get; private set; }
            public string? Poznamka { get; private set; }
            public string? DuvodBlokace { get; private set; }

            public Zakaznik(Guid id, string jmeno, string email) : base(id)
            {
                Jmeno = Check.NotNullOrWhiteSpace(jmeno, nameof(Jmeno), ZakaznikConsts.JmenoMaxLength);
                ZmenitEmail(email);
            }

            public void ZmenitEmail(string email)
            {
                Check.NotNullOrWhiteSpace(email, nameof(Email), ZakaznikConsts.EmailMaxLength);
                Email = email;
            }

            public void SetIco(string? ico)
            {
                if (ico != null && !Regex.IsMatch(ico, "^[0-9]{8}$"))
                {
                    throw new BusinessException("ico");
                }
                Ico = ico;
            }

            public void SetPoznamka(string? poznamka)
            {
                Poznamka = Check.Length(poznamka, nameof(Poznamka), 1000);
            }

            public void Zablokovat(string duvod)
            {
                DuvodBlokace = Check.NotNullOrWhiteSpace(duvod, nameof(duvod), 500);
            }
        }
        """;

    private const string Analysis = """
        {
          "classes": [
            {
              "name": "Zákazník",
              "attributes": [
                { "name": "id", "type": "guid" },
                { "name": "jméno", "type": "string", "length": 200 },
                { "name": "e-mail", "type": "string", "length": 256 },
                { "name": "IČO", "type": "string", "length": 8, "multiplicity": "0..1" },
                { "name": "důvod blokace", "type": "string", "length": 500, "multiplicity": "0..1" },
                { "name": "hmotnost", "type": "decimal" }
              ]
            },
            { "name": "Faktura", "attributes": [] },
            { "name": "CRO:Doklad", "external": true }
          ]
        }
        """;

    private static IReadOnlyList<TestIntent> Link()
    {
        var code = DomainCodeExtractor.Extract(TestCompilation.Create(Domain));
        return Linker.Link(AnalysisJsonReader.Read(Analysis), code);
    }

    private static TestIntent Intent(IReadOnlyList<TestIntent> intents, string id) =>
        intents.Single(i => i.Id == id);

    [Fact]
    public void Extractor_FindsStringRules()
    {
        var zakaznik = DomainCodeExtractor.Extract(TestCompilation.Create(Domain)).Single();
        zakaznik.Kind.ShouldBe(DomainKind.AggregateRoot);
        zakaznik.StringRules.Single(r => r.PropertyName == "Jmeno").MaxLength.ShouldBe(200);
        zakaznik.StringRules.Single(r => r.PropertyName == "Email").MaxLength.ShouldBe(200);
        zakaznik.StringRules.Single(r => r.PropertyName == "Poznamka").Required.ShouldBeFalse();
        zakaznik.StringRules.Single(r => r.PropertyName == "DuvodBlokace").Required.ShouldBeTrue();
        zakaznik.CustomGuardedProperties.ShouldContain("Ico");
    }

    [Fact]
    public void SameLength_IsConfirmed()
    {
        Intent(Link(), "Zakaznik.Jmeno.String").Status.ShouldBe(MatchStatus.Confirmed);
    }

    [Fact]
    public void DifferentLength_IsConflict()
    {
        var intent = Intent(Link(), "Zakaznik.EMail.String");
        intent.Status.ShouldBe(MatchStatus.Conflict);
        intent.Message.ShouldContain("256");
    }

    [Fact]
    public void CustomValidation_IsAmbiguous()
    {
        Intent(Link(), "Zakaznik.Ico.String").Status.ShouldBe(MatchStatus.Ambiguous);
    }

    [Fact]
    public void RequiredOnlyForOperation_IsConfirmed()
    {
        Intent(Link(), "Zakaznik.DuvodBlokace.String").Status.ShouldBe(MatchStatus.Confirmed);
    }

    [Fact]
    public void MissingMember_IsAnalysisOnly()
    {
        Intent(Link(), "Zakaznik.Hmotnost.Member").Status.ShouldBe(MatchStatus.AnalysisOnly);
    }

    [Fact]
    public void MissingType_IsAnalysisOnly()
    {
        Intent(Link(), "Faktura.Type").Status.ShouldBe(MatchStatus.AnalysisOnly);
    }

    [Fact]
    public void RuleNotInAnalysis_IsCodeOnly()
    {
        Intent(Link(), "Zakaznik.Poznamka.String").Status.ShouldBe(MatchStatus.CodeOnly);
    }

    [Fact]
    public void ExternalClasses_AreIgnored()
    {
        Link().ShouldNotContain(i => i.AnalysisClass == "CRO:Doklad");
    }
}
