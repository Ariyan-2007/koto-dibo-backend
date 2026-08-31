using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Application.Features.BillSplit.Interfaces;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using BillSplitEntity = KotoDibo.Domain.Entities.BillSplit;

namespace KotoDibo.Application.Features.BillSplit.Services;

public class BillSplitService : IBillSplitService
{
    private readonly IRepository<BillSplitEntity> _billSplits;
    private readonly IRepository<KotoDibo.Domain.Entities.UtilityTariffConfig> _tariffConfigs;
    private readonly IRepository<KotoDibo.Domain.Entities.HouseholdMembership> _memberships;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateBillSplitRequest> _createValidator;
    private readonly IValidator<UpdateBillSplitRequest> _updateValidator;

    public BillSplitService(
        IRepository<BillSplitEntity> billSplits,
        IRepository<KotoDibo.Domain.Entities.UtilityTariffConfig> tariffConfigs,
        IRepository<KotoDibo.Domain.Entities.HouseholdMembership> memberships,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateBillSplitRequest> createValidator,
        IValidator<UpdateBillSplitRequest> updateValidator)
    {
        _billSplits = billSplits;
        _tariffConfigs = tariffConfigs;
        _memberships = memberships;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<BillSplitDto> CreateAsync(string householdId, string callerUserId, CreateBillSplitRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddBillSplit, cancellationToken);

        var method = Enum.Parse<BillSplitMethod>(request.SplitMethod, ignoreCase: true);

        if (method == BillSplitMethod.TariffMetered)
        {
            await FindTariffConfigAsync(request.TariffCountry!, request.TariffProvider, cancellationToken);
        }

        if (request.MemberInputs.Count > 0)
        {
            await RequireActiveMembersAsync(householdId, request.MemberInputs.Select(i => i.UserId), cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow;
        var entity = new BillSplitEntity
        {
            HouseholdId = householdId,
            CreatedByUserId = callerUserId,
            Title = request.Title.Trim(),
            SplitMethod = method,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TariffCountry = method == BillSplitMethod.TariffMetered ? request.TariffCountry!.Trim().ToUpperInvariant() : null,
            TariffProvider = method == BillSplitMethod.TariffMetered ? request.TariffProvider?.Trim() : null,
            MainMeterUsage = method == BillSplitMethod.TariffMetered ? request.MainMeterUsage : null,
            TotalAmount = method == BillSplitMethod.TariffMetered ? null : request.TotalAmount,
            MemberInputs = request.MemberInputs
                .Select(i => new KotoDibo.Domain.Entities.BillSplitMemberInput { UserId = i.UserId, Value = i.Value })
                .ToList(),
            Notes = request.Notes?.Trim(),
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _billSplits.AddAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<BillSplitDto> GetByIdAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBillSplit, cancellationToken);
        var entity = await GetOwnedAsync(householdId, billSplitId, cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<BillSplitDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBillSplit, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var billSplits = await _billSplits.FindAsync(
            b => b.HouseholdId == householdId && b.PeriodTo >= effectiveFrom && b.PeriodFrom <= effectiveTo,
            cancellationToken);

        IEnumerable<BillSplitEntity> filtered = billSplits;
        if (status is not null)
        {
            var parsedStatus = Enum.Parse<FinancialEntryStatus>(status, ignoreCase: true);
            filtered = filtered.Where(b => b.Status == parsedStatus);
        }

        return filtered
            .OrderByDescending(b => b.PeriodFrom)
            .ThenByDescending(b => b.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<BillSplitDto> UpdateAsync(string householdId, string callerUserId, string billSplitId, UpdateBillSplitRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBillSplit, cancellationToken);
        var entity = await GetOwnedAsync(householdId, billSplitId, cancellationToken);

        BazarPurchaseService.RequireEditAccess(membership.Role, entity.CreatedByUserId, callerUserId, HouseholdPermission.UpdateBillSplit, "bill split");
        RequireActive(entity);

        if (request.Title is not null)
        {
            entity.Title = request.Title.Trim();
        }

        if (request.Notes is not null)
        {
            entity.Notes = request.Notes.Trim();
        }

        if (entity.SplitMethod == BillSplitMethod.TariffMetered)
        {
            var resolvedMainMeterUsage = request.MainMeterUsage ?? entity.MainMeterUsage ?? 0m;
            var resolvedMemberInputs = request.MemberInputs ?? entity.MemberInputs
                .Select(i => new BillSplitMemberInputDto { UserId = i.UserId, Value = i.Value })
                .ToList();

            if (resolvedMemberInputs.Sum(i => i.Value) > resolvedMainMeterUsage)
            {
                throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(UpdateBillSplitRequest.MemberInputs)] = ["Sum of member sub-meter usage cannot exceed MainMeterUsage."],
                });
            }

            if (request.MemberInputs is not null)
            {
                await RequireActiveMembersAsync(householdId, request.MemberInputs.Select(i => i.UserId), cancellationToken);
            }

            entity.MainMeterUsage = resolvedMainMeterUsage;
            entity.MemberInputs = resolvedMemberInputs
                .Select(i => new KotoDibo.Domain.Entities.BillSplitMemberInput { UserId = i.UserId, Value = i.Value })
                .ToList();
        }
        else
        {
            if (request.TotalAmount is not null)
            {
                entity.TotalAmount = request.TotalAmount;
            }

            if (request.MemberInputs is not null)
            {
                await RequireActiveMembersAsync(householdId, request.MemberInputs.Select(i => i.UserId), cancellationToken);
                entity.MemberInputs = request.MemberInputs
                    .Select(i => new KotoDibo.Domain.Entities.BillSplitMemberInput { UserId = i.UserId, Value = i.Value })
                    .ToList();
            }
        }

        entity.UpdatedAt = _dateTimeProvider.UtcNow;
        await _billSplits.UpdateAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<BillSplitDto> CancelAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBillSplit, cancellationToken);
        var entity = await GetOwnedAsync(householdId, billSplitId, cancellationToken);

        BazarPurchaseService.RequireEditAccess(membership.Role, entity.CreatedByUserId, callerUserId, HouseholdPermission.CancelBillSplit, "bill split");
        RequireActive(entity);

        entity.Status = FinancialEntryStatus.Cancelled;
        entity.UpdatedAt = _dateTimeProvider.UtcNow;
        await _billSplits.UpdateAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<BillSplitSettlementDto> GetSettlementAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBillSplitSettlement, cancellationToken);
        var entity = await GetOwnedAsync(householdId, billSplitId, cancellationToken);
        var activeMemberIds = await GetActiveMemberIdsAsync(householdId, cancellationToken);

        var result = entity.SplitMethod switch
        {
            BillSplitMethod.TariffMetered => await ComputeTariffMeteredAsync(entity, activeMemberIds, cancellationToken),
            BillSplitMethod.EqualSplit => FairSplitAllocator.ComputeFlatSplit(
                entity.TotalAmount ?? 0m,
                activeMemberIds.ToDictionary(id => id, _ => 1m)),
            BillSplitMethod.WeightedSplit => FairSplitAllocator.ComputeFlatSplit(
                entity.TotalAmount ?? 0m,
                entity.MemberInputs.ToDictionary(i => i.UserId, i => i.Value)),
            _ => throw new DomainException("Unsupported split method."),
        };

        return new BillSplitSettlementDto
        {
            BillSplitId = entity.Id,
            TotalAmount = result.TotalAmount,
            AttributedCost = result.AttributedCost,
            SharedCost = result.SharedCost,
            Bands = result.Bands.Select(b => new BillSplitBandDto
            {
                FromUnits = b.FromUnits,
                ToUnits = b.ToUnits,
                RatePerUnit = b.RatePerUnit,
                UnitsInBand = b.UnitsInBand,
                AttributedUnits = b.AttributedUnits,
                SharedUnits = b.SharedUnits,
                Cost = b.Cost,
            }).ToList(),
            Members = result.Members.Select(m => new BillSplitMemberSettlementDto
            {
                UserId = m.UserId,
                Usage = m.Usage,
                AttributedCost = m.AttributedCost,
                SharedCost = m.SharedCost,
                TotalOwed = m.TotalOwed,
            }).ToList(),
        };
    }

    private async Task<FairSplitResult> ComputeTariffMeteredAsync(BillSplitEntity entity, IReadOnlyList<string> activeMemberIds, CancellationToken cancellationToken)
    {
        var tariff = await FindTariffConfigAsync(entity.TariffCountry!, entity.TariffProvider, cancellationToken);
        var memberUsage = entity.MemberInputs.ToDictionary(i => i.UserId, i => i.Value);
        return FairSplitAllocator.ComputeTariffMetered(tariff.Bands, entity.MainMeterUsage ?? 0m, memberUsage, activeMemberIds);
    }

    private async Task<KotoDibo.Domain.Entities.UtilityTariffConfig> FindTariffConfigAsync(string country, string? provider, CancellationToken cancellationToken)
    {
        var normalizedCountry = country.Trim().ToUpperInvariant();
        var tariff = string.IsNullOrWhiteSpace(provider)
            ? await _tariffConfigs.FindOneAsync(t => t.Country == normalizedCountry && t.IsActive, cancellationToken)
            : await _tariffConfigs.FindOneAsync(t => t.Country == normalizedCountry && t.Provider == provider.Trim() && t.IsActive, cancellationToken);

        return tariff ?? throw FieldValidationException(nameof(CreateBillSplitRequest.TariffCountry), $"No active tariff configuration found for '{country}'.");
    }

    private async Task<IReadOnlyList<string>> GetActiveMemberIdsAsync(string householdId, CancellationToken cancellationToken)
    {
        var memberships = await _memberships.FindAsync(
            m => m.HouseholdId == householdId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);
        return memberships.Select(m => m.UserId).ToList();
    }

    private async Task RequireActiveMembersAsync(string householdId, IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var requestedIds = userIds.Distinct().ToList();
        var activeIds = (await GetActiveMemberIdsAsync(householdId, cancellationToken)).ToHashSet();
        var invalidIds = requestedIds.Where(id => !activeIds.Contains(id)).ToList();

        if (invalidIds.Count > 0)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateBillSplitRequest.MemberInputs)] = [$"The following users are not active members of this household: {string.Join(", ", invalidIds)}."],
            });
        }
    }

    private async Task<BillSplitEntity> GetOwnedAsync(string householdId, string billSplitId, CancellationToken cancellationToken)
    {
        var entity = await _billSplits.GetByIdAsync(billSplitId, cancellationToken);
        if (entity is null || entity.HouseholdId != householdId)
        {
            throw new NotFoundException("BillSplit", billSplitId);
        }

        return entity;
    }

    private static void RequireActive(BillSplitEntity entity)
    {
        if (entity.Status != FinancialEntryStatus.Active)
        {
            throw new DomainException("This bill split has been cancelled and can no longer be edited.");
        }
    }

    private static KotoDibo.Application.Common.Exceptions.ValidationException FieldValidationException(string field, string message) => new(new Dictionary<string, string[]>
    {
        [field] = [message],
    });

    private static BillSplitDto ToDto(BillSplitEntity entity) => new()
    {
        Id = entity.Id,
        HouseholdId = entity.HouseholdId,
        CreatedByUserId = entity.CreatedByUserId,
        Title = entity.Title,
        SplitMethod = entity.SplitMethod.ToString(),
        PeriodFrom = entity.PeriodFrom,
        PeriodTo = entity.PeriodTo,
        Currency = entity.Currency,
        TariffCountry = entity.TariffCountry,
        TariffProvider = entity.TariffProvider,
        MainMeterUsage = entity.MainMeterUsage,
        TotalAmount = entity.TotalAmount,
        MemberInputs = entity.MemberInputs.Select(i => new BillSplitMemberInputDto { UserId = i.UserId, Value = i.Value }).ToList(),
        Notes = entity.Notes,
        Status = entity.Status.ToString(),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
