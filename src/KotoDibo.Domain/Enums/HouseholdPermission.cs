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
    AddAnyBazarPurchase,
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
