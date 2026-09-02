namespace KotoDibo.Domain.Enums;

// Manual: a member deposited cash directly. AutoFromBazar: generated automatically because a
// member paid for a Bazar purchase out of their own pocket (BazarFundingSource.Personal) — it
// mirrors that spend as money the household received. Auto-generated rows can only be changed by
// editing/cancelling the Bazar purchase that created them (see ContributionService), so the two
// records never drift apart.
public enum ContributionSourceType
{
    Manual,
    AutoFromBazar,
}
