using TestGenerator.Sample.Consts;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace TestGenerator.Sample.Objednavky;

public class PolozkaObjednavky : Entity<Guid>
{
    public Guid ObjednavkaId { get; private set; }
    public Guid ProduktId { get; private set; }
    public string NazevProduktu { get; private set; } = null!;
    public decimal JednotkovaCena { get; private set; }
    public int Mnozstvi { get; private set; }

    public decimal Cena => JednotkovaCena * Mnozstvi;

    protected PolozkaObjednavky()
    {
    }

    internal PolozkaObjednavky(Guid id, Guid objednavkaId, Guid produktId, string nazevProduktu, decimal jednotkovaCena, int mnozstvi)
        : base(id)
    {
        ObjednavkaId = objednavkaId;
        ProduktId = produktId;
        NazevProduktu = Check.NotNullOrWhiteSpace(nazevProduktu, nameof(NazevProduktu), ObjednavkaConsts.NazevProduktuMaxLength);
        if (jednotkovaCena <= 0)
        {
            throw new BusinessException(EshopErrorCodes.NeplatnaCena).WithData("cena", jednotkovaCena);
        }
        JednotkovaCena = jednotkovaCena;
        SetMnozstvi(mnozstvi);
    }

    internal void SetMnozstvi(int mnozstvi)
    {
        Mnozstvi = Check.Range(mnozstvi, nameof(Mnozstvi), ObjednavkaConsts.MnozstviMin, ObjednavkaConsts.MnozstviMax);
    }
}
