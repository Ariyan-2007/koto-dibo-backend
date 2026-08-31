namespace KotoDibo.Domain.Entities;

// One progressive-tariff schedule for a country/provider/utility combination — stored centrally so
// every household's TariffMetered bill splits reference the same up-to-date rates instead of each
// household hardcoding its own copy. Seeded server-side (see TariffConfigSeeder); no per-household
// authoring in the MVP.
public class UtilityTariffConfig
{
    public string Id { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string UtilityType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public List<TariffBand> Bands { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// A single progressive-tariff band: usage from `FromUnits` (inclusive) up to `ToUnits` (exclusive
// upper bound; null on the top band = unlimited) is billed at `RatePerUnit`.
public class TariffBand
{
    public decimal FromUnits { get; set; }
    public decimal? ToUnits { get; set; }
    public decimal RatePerUnit { get; set; }
}
