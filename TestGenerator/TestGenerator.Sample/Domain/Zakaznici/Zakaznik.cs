using System.Text.RegularExpressions;
using TestGenerator.Sample.Consts;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace TestGenerator.Sample.Zakaznici;

public class Zakaznik : AggregateRoot<Guid>
{
    public string Jmeno { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Ico { get; private set; }
    public decimal UverovyLimit { get; private set; }
    public Adresa Adresa { get; private set; } = null!;
    public bool JeAktivni { get; private set; }

    protected Zakaznik()
    {
    }

    public Zakaznik(Guid id, string jmeno, string email, string? ico, Adresa adresa) : base(id)
    {
        SetJmeno(jmeno);
        ZmenitEmail(email);
        SetIco(ico);
        ZmenitAdresu(adresa);
        UverovyLimit = 0;
        JeAktivni = true;
    }

    public Zakaznik SetJmeno(string jmeno)
    {
        Jmeno = Check.NotNullOrWhiteSpace(jmeno, nameof(Jmeno), ZakaznikConsts.JmenoMaxLength);
        return this;
    }

    public Zakaznik ZmenitEmail(string email)
    {
        Check.NotNullOrWhiteSpace(email, nameof(Email), ZakaznikConsts.EmailMaxLength);
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new BusinessException(EshopErrorCodes.NeplatnyEmail).WithData("email", email);
        }
        Email = email;
        return this;
    }

    public Zakaznik SetIco(string? ico)
    {
        if (ico != null && !Regex.IsMatch(ico, @"^\d{8}$"))
        {
            throw new BusinessException(EshopErrorCodes.NeplatneIco).WithData("ico", ico);
        }
        Ico = ico;
        return this;
    }

    public Zakaznik ZmenitAdresu(Adresa adresa)
    {
        Adresa = Check.NotNull(adresa, nameof(Adresa));
        return this;
    }

    public Zakaznik NastavitUverovyLimit(decimal limit)
    {
        if (limit < 0)
        {
            throw new BusinessException(EshopErrorCodes.ZapornyUverovyLimit).WithData("limit", limit);
        }
        UverovyLimit = limit;
        return this;
    }

    public void Deaktivovat()
    {
        JeAktivni = false;
        UverovyLimit = 0;
    }

    public void Aktivovat()
    {
        JeAktivni = true;
    }
}
