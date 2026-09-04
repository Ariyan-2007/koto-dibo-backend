using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Migrations;

// One-time backfill for BazarPurchase/Contribution rows recorded before CreatedByUserId (the
// "who actually submitted this" audit field, separate from the financial owner) existed. Every
// pre-existing row was, in effect, self-recorded — the "on behalf of" flows that make the two
// diverge didn't exist yet — so the safe default is CreatedByUserId = the row's own owner id.
//
// Must run before BazarContributionMirrorBackfill: that migration mirrors CreatedByUserId from the
// purchase onto the Contribution it generates, so a purchase's own CreatedByUserId needs to already
// be backfilled by the time it runs.
//
// Idempotent: only touches rows where CreatedByUserId is still empty, so it's safe on every startup.
public static class CreatedByAuditBackfill
{
    public static async Task RunAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var purchases = context.GetCollection<BazarPurchase>(nameof(BazarPurchase));
        var contributions = context.GetCollection<Contribution>(nameof(Contribution));

        var purchaseFilter = Builders<BazarPurchase>.Filter.Or(
            Builders<BazarPurchase>.Filter.Eq(p => p.CreatedByUserId, null),
            Builders<BazarPurchase>.Filter.Eq(p => p.CreatedByUserId, string.Empty));
        var purchaseCandidates = await purchases.Find(purchaseFilter).ToListAsync(cancellationToken);
        foreach (var purchase in purchaseCandidates)
        {
            var update = Builders<BazarPurchase>.Update.Set(p => p.CreatedByUserId, purchase.PurchasedByUserId);
            await purchases.UpdateOneAsync(p => p.Id == purchase.Id, update, cancellationToken: cancellationToken);
        }

        var contributionFilter = Builders<Contribution>.Filter.Or(
            Builders<Contribution>.Filter.Eq(c => c.CreatedByUserId, null),
            Builders<Contribution>.Filter.Eq(c => c.CreatedByUserId, string.Empty));
        var contributionCandidates = await contributions.Find(contributionFilter).ToListAsync(cancellationToken);
        foreach (var contribution in contributionCandidates)
        {
            var update = Builders<Contribution>.Update.Set(c => c.CreatedByUserId, contribution.ContributedByUserId);
            await contributions.UpdateOneAsync(c => c.Id == contribution.Id, update, cancellationToken: cancellationToken);
        }
    }
}
