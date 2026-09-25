using Shouldly;
using TestGenerator.Naming;
using Xunit;

namespace TestGenerator.Tests.Naming;

public class NameMatcherTests
{
    [Theory]
    [InlineData("Položka objednávky", "PolozkaObjednavky")]
    [InlineData("e-mail", "Email")]
    [InlineData("IČO", "Ico")]
    [InlineData("je aktivní", "JeAktivni")]
    [InlineData("id zákazníka", "ZakaznikId")]
    [InlineData("typ dokumentu - název", "NazevTypuDokumentu")]
    [InlineData("id spisového a skartačního plánu", "IdSpisovehoASkartacnihoPlanu")]
    [InlineData("id položky SSP", "IdPolozkySSP")]
    [InlineData("Spisový a skartační plán", "SpisovyASkartacniPlan")]
    public void Matches(string analysisName, string codeName)
    {
        NameMatcher.Score(analysisName, codeName).ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("název", "Popis")]
    [InlineData("id", "ZakaznikId")]
    [InlineData("hmotnost", "Cena")]
    [InlineData("kód účetní jednotky", "KodUj")]
    public void DoesNotMatch(string analysisName, string codeName)
    {
        NameMatcher.Score(analysisName, codeName).ShouldBe(0);
    }

    [Fact]
    public void CodeTokens_SplitPascalCaseWithAcronyms()
    {
        NameMatcher.TokenizeCodeName("IdPolozkySSP").ShouldBe(new[] { "id", "polozky", "ssp" });
    }
}
