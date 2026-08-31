namespace KotoDibo.Domain.Enums;

public enum HouseholdPermission
{
    ViewHousehold,
    UpdateHousehold,
    ArchiveHousehold,
    RestoreHousehold,
    ViewMembers,
    AddMember,
    RemoveMember,
    UpdateMemberRole,
    LeaveHousehold,

    AddBazarPurchase,
    ViewBazar,
    UpdateBazarPurchase,
    CancelBazarPurchase,

    AddContribution,
    ViewContributions,
    UpdateContribution,
    CancelContribution,

    RecordOwnMealCount,
    RecordAnyMealCount,
    ViewMeals,

    ViewMealCalculation,

    AddBillSplit,
    ViewBillSplit,
    UpdateBillSplit,
    CancelBillSplit,
    ViewBillSplitSettlement,

    ViewSettlement,
}
