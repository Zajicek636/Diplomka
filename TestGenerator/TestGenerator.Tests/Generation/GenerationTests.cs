using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using TestGenerator.Analysis;
using TestGenerator.Generation;
using TestGenerator.Tests.Infrastructure;
using Xunit;

namespace TestGenerator.Tests.Generation;

public class GenerationTests
{
    private const string Domain = """
        using System;
        using System.Text.RegularExpressions;
        using Volo.Abp;
        using Volo.Abp.Domain.Entities;
        using Volo.Abp.Domain.Values;

        namespace Demo;

        public class Adresa : ValueObject
        {
            public string Ulice { get; private set; }
            public string Psc { get; private set; }

            public Adresa(string ulice, string psc)
            {
                Ulice = Check.NotNullOrWhiteSpace(ulice, nameof(Ulice), 200);
                if (psc == null || !Regex.IsMatch(psc, @"^\d{5}$"))
                {
                    throw new BusinessException("psc");
                }
                Psc = psc;
            }
        }

        public class Zakaznik : AggregateRoot<Guid>
        {
            public string Jmeno { get; private set; } = null!;
            public string Email { get; private set; } = null!;
            public Adresa Adresa { get; private set; } = null!;
            public string? DuvodBlokace { get; private set; }

            public Zakaznik(Guid id, string jmeno, string email, Adresa adresa) : base(id)
            {
                Jmeno = Check.NotNullOrWhiteSpace(jmeno, nameof(Jmeno), 200);
                ZmenitEmail(email);
                Adresa = adresa;
            }

            public void ZmenitEmail(string email)
            {
                Check.NotNullOrWhiteSpace(email, nameof(Email), 200);
                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    throw new BusinessException("email");
                }
                Email = email;
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
            { "name": "Zákazník", "attributes": [
                { "name": "jméno", "type": "string", "length": 200 },
                { "name": "e-mail", "type": "string", "length": 256 },
                { "name": "důvod blokace", "type": "string", "length": 500, "multiplicity": "0..1" },
                { "name": "hmotnost", "type": "decimal" } ] },
            { "name": "Adresa", "attributes": [
                { "name": "ulice", "type": "string", "length": 200 },
                { "name": "PSČ", "type": "string", "length": 5 } ] }
          ]
        }
        """;

    private static GenerationResult Run(string profile)
    {
        var compilation = TestCompilation.Create(Domain);
        return TestGenerationPipeline.Run(compilation, new[] { AnalysisJsonReader.Read(Analysis) },
            new[] { ProfileRegistry.Get(profile) }, new GenerationOptions { RootNamespace = "Demo.Tests" });
    }

    [Fact]
    public void Plan_UsesPatternsForValidValues()
    {
        var plan = Run("generic").Plans.Single(p => p.Name == "Zakaznik");
        plan.StringTests.Single(t => t.PropertyName == "Email").Shape.ShouldBe(StringShape.Email);
        plan.StringTests.Single(t => t.PropertyName == "DuvodBlokace").Route.ShouldBe(TestRoute.Method);
        plan.MissingMembers.Single().ExpectedName.ShouldBe("Hmotnost");
    }

    [Fact]
    public void GenericProfile_GeneratesCompilableCode()
    {
        var result = Run("generic");
        result.Files.Select(f => f.RelativePath).ShouldBe(new[] { "Generic/AdresaTests.cs", "Generic/ZakaznikTests.cs" }, ignoreOrder: true);

        var trees = result.Files.Select(f => CSharpSyntaxTree.ParseText(f.Content, path: f.RelativePath));
        var compilation = TestCompilation.Create(Domain).AddSyntaxTrees(trees);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors) + Environment.NewLine + result.Files[0].Content);
    }

    [Fact]
    public void GenericProfile_MarksConflictAndSkipsAmbiguous()
    {
        var zakaznik = Run("generic").Files.Single(f => f.RelativePath == "Generic/ZakaznikTests.cs").Content;
        zakaznik.ShouldContain("[Trait(\"TestGenStatus\", \"Conflict\")]");
        zakaznik.ShouldContain("[InlineData(256, false)]");
        zakaznik.ShouldContain("entity.Zablokovat(value)");

        var adresa = Run("generic").Files.Single(f => f.RelativePath == "Generic/AdresaTests.cs").Content;
        adresa.ShouldContain("Skip = ");
        adresa.ShouldContain("\"11111\"");
    }

    [Fact]
    public void FakeEntityProfile_GeneratesFakesGeneratorAndTests()
    {
        var result = Run("fakeentity");
        result.Files.Select(f => f.RelativePath).ShouldBe(new[]
        {
            "FakeEntity/FakeEntities/FakeAdresa.cs",
            "FakeEntity/FakeEntities/FakeZakaznik.cs",
            "FakeEntity/Generators/RandomFakeEntityGenerator.cs",
            "FakeEntity/Adresa_Tests.cs",
            "FakeEntity/Zakaznik_Tests.cs"
        }, ignoreOrder: true);

        var fake = result.Files.Single(f => f.RelativePath.EndsWith("FakeZakaznik.cs")).Content;
        fake.ShouldContain("public class FakeZakaznik : IFakeEntity<Zakaznik>");
        fake.ShouldContain("public const int EmailMaxLength = 256;");

        var tests = result.Files.Single(f => f.RelativePath == "FakeEntity/Zakaznik_Tests.cs").Content;
        tests.ShouldContain("PropertyTester.GetStringPropertyTestData");
        tests.ShouldContain("TestSetProperty<Zakaznik, System.ArgumentException>");
    }
}
