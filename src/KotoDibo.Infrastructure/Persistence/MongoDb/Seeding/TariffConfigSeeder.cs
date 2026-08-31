using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Seeding;

// Seeds the centrally-maintained tariff schedule TariffMetered bill splits look up by country.
// Bands below are illustrative reference rates only — shape mirrors the FairSplit reference repo's
// bangladesh.json — swap in real published BPDB/DPDC slabs via an ops data update before relying on
// this for real bills. Deliberately idempotent: only inserts if no config exists yet for this
// country/utility, so an ops-corrected config is never clobbered by a later app restart.
public static class TariffConfigSeeder
{
    public static async Task SeedAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var collection = context.GetCollection<UtilityTariffConfig>(nameof(UtilityTariffConfig));

        var alreadySeeded = await collection.Find(t => t.Country == "BD" && t.UtilityType == "Electricity").AnyAsync(cancellationToken);
        if (alreadySeeded)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var config = new UtilityTariffConfig
        {
            Country = "BD",
            Provider = "Residential",
            UtilityType = "Electricity",
            Currency = "BDT",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Bands =
            [
                new TariffBand { FromUnits = 0, ToUnits = 50, RatePerUnit = 4.00m },
                new TariffBand { FromUnits = 50, ToUnits = 150, RatePerUnit = 5.00m },
                new TariffBand { FromUnits = 150, ToUnits = 300, RatePerUnit = 6.00m },
                new TariffBand { FromUnits = 300, ToUnits = 400, RatePerUnit = 7.00m },
                new TariffBand { FromUnits = 400, ToUnits = 600, RatePerUnit = 9.00m },
                new TariffBand { FromUnits = 600, ToUnits = null, RatePerUnit = 11.00m },
            ],
        };

        await collection.InsertOneAsync(config, options: null, cancellationToken);
    }
}
