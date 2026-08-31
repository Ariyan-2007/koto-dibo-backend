namespace KotoDibo.Domain.Enums;

// TariffMetered is the flagship case (progressive-band allocation via FairSplitAllocator).
// EqualSplit/WeightedSplit cover the recurring bills that have no sub-meter at all (rent, wifi,
// gas cylinder) so BillSplit stays useful beyond electricity.
public enum BillSplitMethod
{
    TariffMetered,
    EqualSplit,
    WeightedSplit,
}
