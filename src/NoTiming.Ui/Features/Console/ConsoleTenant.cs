using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Tenancy.Models;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Tenant of the Console (#646): what the Api tells of the Tenant the account acts in and of what the person may do about
/// it, and the saving of its rules. The Ui keeps no rule of its own about who may edit. It follows the account.
/// </summary>
public class ConsoleTenant : AccountAwareContext, IConsoleTenant, IScoped
{
    readonly TenantApiRepository _tenants;

    public ConsoleTenant(IAccountSession account, TenantApiRepository tenants)
        : base(account)
    {
        _tenants = tenants;
    }

    public string? TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool CanEdit { get; private set; }
    public bool OnlyAverageLoopSpeed { get; private set; }
    public string? RankerCode { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        await Account.Load();
        if (!Account.IsKnown)
        {
            return false; // the host has not said who is signed in: ask again
        }

        if (Account.Current is not { CurrentTenantId: { Length: > 0 } tenantId })
        {
            Clear();
            return true;
        }

        TenantModel? tenant;
        try
        {
            tenant = await _tenants.Read(tenantId);
        }
        catch (JsonApiException)
        {
            tenant = null; // a Tenant the Api does not give is none
        }

        if (tenant == null)
        {
            Clear();
            return true;
        }

        TenantId = tenantId;
        Name = string.IsNullOrEmpty(tenant.Name) ? tenantId : tenant.Name;
        OnlyAverageLoopSpeed = tenant.RegionalRules.OnlyAverageLoopSpeed;
        RankerCode = tenant.RegionalRules.RankerCode;
        CanEdit = await CanEditOf(tenantId);
        return true;
    }

    public async Task SaveRules(bool onlyAverageLoopSpeed, string? rankerCode)
    {
        var tenantId = TenantId ?? throw new DomainException(Federation_not_yours_string);
        // a ranker that is left empty is sent as null, which clears it: a member that is not named stays as it is
        var rules = new RegionalRulesModel
        {
            OnlyAverageLoopSpeed = onlyAverageLoopSpeed,
            RankerCode = string.IsNullOrWhiteSpace(rankerCode) ? null : rankerCode.Trim(),
        };
        await ApiRefusals.Answered(() => _tenants.SaveRules(tenantId, rules));
        await ReloadState();
    }

    async Task<bool> CanEditOf(string tenantId)
    {
        try
        {
            return (await _tenants.CapabilitiesOf(tenantId)).CanEditTenant;
        }
        catch (JsonApiException)
        {
            return false; // what the Api does not say, the person may not do
        }
    }

    void Clear()
    {
        TenantId = null;
        Name = string.Empty;
        CanEdit = false;
        OnlyAverageLoopSpeed = false;
        RankerCode = null;
    }
}
