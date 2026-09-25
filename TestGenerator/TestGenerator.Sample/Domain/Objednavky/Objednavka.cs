using TestGenerator.Sample.Consts;
using TestGenerator.Sample.Enums;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace TestGenerator.Sample.Objednavky;

public class Objednavka : AggregateRoot<Guid>
{
    private readonly List<PolozkaObjednavky> _polozky = new();

    public string Cislo { get; private set; } = null!;
    public Guid ZakaznikId { get; private set; }
    public DateTime DatumVytvoreni { get; private set; }
    public StavObjednavky Stav { get; private set; }
    public string? DuvodZruseni { get; private set; }

    public IReadOnlyCollection<PolozkaObjednavky> Polozky => _polozky.AsReadOnly();
    public decimal CelkovaCena => _polozky.Sum(x => x.Cena);

    protected Objednavka()
    {
    }

    public Objednavka(Guid id, string cislo, Guid zakaznikId, DateTime datumVytvoreni) : base(id)
    {
        Cislo = Check.NotNullOrWhiteSpace(cislo, nameof(Cislo), ObjednavkaConsts.CisloMaxLength);
        ZakaznikId = zakaznikId;
        DatumVytvoreni = datumVytvoreni;
        Stav = StavObjednavky.Nova;
    }

    public PolozkaObjednavky PridatPolozku(Guid polozkaId, Guid produktId, string nazevProduktu, decimal jednotkovaCena, int mnozstvi)
    {
        OveritUpravitelnost();

        var existujici = _polozky.FirstOrDefault(x => x.ProduktId == produktId);
        if (existujici != null)
        {
            existujici.SetMnozstvi(existujici.Mnozstvi + mnozstvi);
            return existujici;
        }

        var polozka = new PolozkaObjednavky(polozkaId, Id, produktId, nazevProduktu, jednotkovaCena, mnozstvi);
        _polozky.Add(polozka);
        return polozka;
    }

    public void OdebratPolozku(Guid produktId)
    {
        OveritUpravitelnost();

        var polozka = _polozky.FirstOrDefault(x => x.ProduktId == produktId)
                      ?? throw new BusinessException(EshopErrorCodes.PolozkaNenalezena).WithData("produktId", produktId);
        _polozky.Remove(polozka);
    }

    public void Potvrdit()
    {
        OveritStav(StavObjednavky.Nova, StavObjednavky.Potvrzena);
        if (_polozky.Count == 0)
        {
            throw new BusinessException(EshopErrorCodes.ObjednavkaBezPolozek).WithData("cislo", Cislo);
        }
        Stav = StavObjednavky.Potvrzena;
    }

    public void Odeslat()
    {
        OveritStav(StavObjednavky.Potvrzena, StavObjednavky.Odeslana);
        Stav = StavObjednavky.Odeslana;
    }

    public void Dorucit()
    {
        OveritStav(StavObjednavky.Odeslana, StavObjednavky.Dorucena);
        Stav = StavObjednavky.Dorucena;
    }

    public void Zrusit(string duvod)
    {
        if (Stav is StavObjednavky.Dorucena or StavObjednavky.Zrusena)
        {
            throw new BusinessException(EshopErrorCodes.NeplatnyPrechodStavu)
                .WithData("z", Stav)
                .WithData("do", StavObjednavky.Zrusena);
        }
        DuvodZruseni = Check.NotNullOrWhiteSpace(duvod, nameof(duvod), ObjednavkaConsts.DuvodZruseniMaxLength);
        Stav = StavObjednavky.Zrusena;
    }

    private void OveritUpravitelnost()
    {
        if (Stav != StavObjednavky.Nova)
        {
            throw new BusinessException(EshopErrorCodes.ObjednavkaNeniUpravitelna).WithData("stav", Stav);
        }
    }

    private void OveritStav(StavObjednavky ocekavany, StavObjednavky cilovy)
    {
        if (Stav != ocekavany)
        {
            throw new BusinessException(EshopErrorCodes.NeplatnyPrechodStavu)
                .WithData("z", Stav)
                .WithData("do", cilovy);
        }
    }
}
