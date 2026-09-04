using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Migrations;

// One-time backfill for data recorded before the Bazar funding-source split shipped. Every
// BazarPurchase before this feature was, in effect, paid personally (there was no HouseholdFund
// option), and MealCalculationService.GetMealRateAsync used to credit that spend directly by
// summing BazarPurchase and Contribution independently. Now that GiveTake is computed purely from
// Contribution rows (a Personal purchase's credit flows through its mirrored Contribution
// instead — see BazarPurchaseService), any pre-existing purchase missing that mirror needs one
// created retroactively. Without this, re-querying a past period after this ships would silently
// show less credit than it used to for members who bought Bazar out of pocket.
//
// Idempotent: only touches purchases that don't already have a LinkedContributionId, so it's safe
// to run on every startup — after the first run it finds nothing to do.
public static class BazarContributionMirrorBackfill
{
    public static async Task RunAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var purchases = context.GetCollection<BazarPurchase>(nameof(BazarPurchase));
        var contributions = context.GetCollection<Contribution>(nameof(Contribution));

        var filter = Builders<BazarPurchase>.Filter.And(
            Builders<BazarPurchase>.Filter.Eq(p => p.FundingSource, BazarFundingSource.Personal),
            Builders<BazarPurchase>.Filter.Eq(p => p.LinkedContributionId, null),
            Builders<BazarPurchase>.Filter.Gt(p => p.Amount, 0m));

        var candidates = await purchases.Find(filter).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var purchase in candidates)
        {
            // Mirror the purchase's own status: a purchase that was already cancelled before this
            // migration ran must not inject phantom active money into the balance/meal calc.
            var contribution = new Contribution
            {
                HouseholdId = purchase.HouseholdId,
                ContributedByUserId = purchase.PurchasedByUserId,
                CreatedByUserId = purchase.CreatedByUserId,
                Date = purchase.Date,
                Amount = purchase.Amount,
                Currency = purchase.Currency,
                Notes = "Backfilled: mirrors a Bazar purchase recorded before funding-source tracking existed.",
                SourceType = ContributionSourceType.AutoFromBazar,
                SourceBazarPurchaseId = purchase.Id,
                Status = purchase.Status,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await contributions.InsertOneAsync(contribution, options: null, cancellationToken);

            var update = Builders<BazarPurchase>.Update.Set(p => p.LinkedContributionId, contribution.Id);
            await purchases.UpdateOneAsync(p => p.Id == purchase.Id, update, cancellationToken: cancellationToken);
        }
    }
}
