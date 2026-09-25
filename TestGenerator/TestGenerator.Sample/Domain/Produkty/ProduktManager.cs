using TestGenerator.Sample.Enums;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace TestGenerator.Sample.Produkty;

public class ProduktManager(IProduktRepository produktRepository, IGuidGenerator guidGenerator) : IDomainService
{
    public async Task<Produkt> VytvoritAsync(string kod, string nazev, decimal cena, SazbaDph sazbaDph,
        CancellationToken cancellationToken = default)
    {
        await OveritUnikatniKodAsync(kod, cancellationToken);
        return new Produkt(guidGenerator.Create(), kod, nazev, cena, sazbaDph);
    }

    public async Task<Produkt> ZmenitKodAsync(Produkt produkt, string novyKod, CancellationToken cancellationToken = default)
    {
        Check.NotNull(produkt, nameof(produkt));
        if (produkt.Kod == novyKod)
        {
            return produkt;
        }
        await OveritUnikatniKodAsync(novyKod, cancellationToken);
        return produkt.SetKod(novyKod);
    }

    private async Task OveritUnikatniKodAsync(string kod, CancellationToken cancellationToken)
    {
        var existujici = await produktRepository.FindByKodAsync(kod, cancellationToken);
        if (existujici != null)
        {
            throw new BusinessException(EshopErrorCodes.DuplicitniKodProduktu).WithData("kod", kod);
        }
    }
}
