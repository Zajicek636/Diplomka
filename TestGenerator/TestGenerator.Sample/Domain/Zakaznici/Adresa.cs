using System.Text.RegularExpressions;
using TestGenerator.Sample.Consts;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace TestGenerator.Sample.Zakaznici;

public class Adresa : ValueObject
{
    public string Ulice { get; private set; } = null!;
    public string Mesto { get; private set; } = null!;
    public string Psc { get; private set; } = null!;

    private Adresa()
    {
    }

    public Adresa(string ulice, string mesto, string psc)
    {
        Ulice = Check.NotNullOrWhiteSpace(ulice, nameof(Ulice), AdresaConsts.UliceMaxLength);
        Mesto = Check.NotNullOrWhiteSpace(mesto, nameof(Mesto), AdresaConsts.MestoMaxLength);

        if (psc == null || !Regex.IsMatch(psc, @"^\d{5}$"))
        {
            throw new BusinessException(EshopErrorCodes.NeplatnePsc).WithData("psc", psc ?? string.Empty);
        }
        Psc = psc;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Ulice;
        yield return Mesto;
        yield return Psc;
    }
}
