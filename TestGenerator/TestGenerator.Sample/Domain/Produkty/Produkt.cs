using System.Text.RegularExpressions;
using TestGenerator.Sample.Consts;
using TestGenerator.Sample.Enums;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace TestGenerator.Sample.Produkty;

public class Produkt : AggregateRoot<Guid>
{
    public string Kod { get; private set; } = null!;
    public string Nazev { get; private set; } = null!;
    public decimal Cena { get; private set; }
    public SazbaDph SazbaDph { get; private set; }
    public bool JeAktivni { get; private set; }

    protected Produkt()
    {
    }

    internal Produkt(Guid id, string kod, string nazev, decimal cena, SazbaDph sazbaDph) : base(id)
    {
        SetKod(kod);
        SetNazev(nazev);
        JeAktivni = true;
        ZmenitCenu(cena);
        SazbaDph = sazbaDph;
    }

    internal Produkt SetKod(string kod)
    {
        Check.NotNullOrWhiteSpace(kod, nameof(Kod), ProduktConsts.KodMaxLength);
        if (!Regex.IsMatch(kod, @"^[A-Z0-9-]+$"))
        {
            throw new BusinessException(EshopErrorCodes.NeplatnyKodProduktu).WithData("kod", kod);
        }
        Kod = kod;
        return this;
    }

    public Produkt SetNazev(string nazev)
    {
        Nazev = Check.NotNullOrWhiteSpace(nazev, nameof(Nazev), ProduktConsts.NazevMaxLength);
        return this;
    }

    public Produkt ZmenitCenu(decimal cena)
    {
        if (!JeAktivni)
        {
            throw new BusinessException(EshopErrorCodes.ProduktNeniAktivni).WithData("kod", Kod);
        }
        if (cena <= 0)
        {
            throw new BusinessException(EshopErrorCodes.NeplatnaCena).WithData("cena", cena);
        }
        Cena = cena;
        return this;
    }

    public Produkt ZmenitSazbuDph(SazbaDph sazbaDph)
    {
        SazbaDph = sazbaDph;
        return this;
    }

    public void Deaktivovat()
    {
        JeAktivni = false;
    }

    public void Aktivovat()
    {
        JeAktivni = true;
    }
}
