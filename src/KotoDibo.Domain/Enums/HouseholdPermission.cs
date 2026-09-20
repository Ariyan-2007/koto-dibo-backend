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
    TransferOwnership,
    LeaveHousehold,

    AddBazarPurchase,
    AddAnyBazarPurchase,
    ViewBazar,
    UpdateBazarPurchase,
    DeleteBazarPurchase,

    AddContribution,
    AddAnyContribution,
    ViewContributions,
    UpdateContribution,
    DeleteContribution,

    AddWithdrawal,
    AddAnyWithdrawal,
    ViewWithdrawals,
    DeleteWithdrawal,

    ViewHouseholdBalance,

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
