namespace KotoDibo.Domain.Enums;

// TariffMetered is the flagship case (progressive-band allocation via FairSplitAllocator).
// EqualSplit/WeightedSplit cover the recurring bills that have no sub-meter at all (rent, wifi,
// gas cylinder) so BillSplit stays useful beyond electricity.
//
// Naming: this identifier is intentionally technical (it names the algorithm, not the bill type) —
// don't rename it. The user-facing label is a frontend concern: display TariffMetered as
// "Electricity Bill (Postpaid)" (see MVP_FRONTEND_BLUEPRINT.md Phase 4) — "Postpaid" specifically
// distinguishes it from Bangladesh's common prepaid electricity meters, which this progressive-band,
// bill-after-usage model doesn't apply to.
public enum BillSplitMethod
{
    TariffMetered,
    EqualSplit,
    WeightedSplit,
}
