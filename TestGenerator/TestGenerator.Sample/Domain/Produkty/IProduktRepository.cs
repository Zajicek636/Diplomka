using Volo.Abp.Domain.Repositories;

namespace TestGenerator.Sample.Produkty;

public interface IProduktRepository : IRepository<Produkt, Guid>
{
    Task<Produkt?> FindByKodAsync(string kod, CancellationToken cancellationToken = default);
}
