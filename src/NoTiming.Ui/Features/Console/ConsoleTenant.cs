using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console;
using static NoTiming.Ui.Features.Console.ApiAnswers;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Tenant of the Console (#646): what the Api tells of the Tenant the account acts in and of what the person may do about
/// it, and the saving of its rules. The Ui keeps no rule of its own about who may edit. It follows the account.
/// </summary>
public class ConsoleTenant : AccountAwareContext, IConsoleTenant, IScoped
{
    readonly JsonApiClient _api;

    public ConsoleTenant(IAccountSession account, JsonApiClient api)
        : base(account)
    {
        _api = api;
    }

    public string? TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool CanEditRules { get; private set; }
    public bool CanEditBranding { get; private set; }
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

        var tenant = await _api.Send(HttpMethod.Get, Route(tenantId));
        var capabilities = await _api.Send(HttpMethod.Get, $"{Route(tenantId)}/capabilities");
        if (tenant is not { IsSuccess: true, Document: { } document })
        {
            Clear();
            return true;
        }

        var attributes = document.GetProperty("data").GetProperty("attributes");
        TenantId = tenantId;
        Name = Text(attributes, "name") ?? tenantId;
        var rules = attributes.GetProperty("regionalRules");
        OnlyAverageLoopSpeed = Flag(rules, "onlyAverageLoopSpeed");
        RankerCode = Text(rules, "rankerCode");
        var may = capabilities is { IsSuccess: true, Document: { } granted }
            ? granted.GetProperty("data").GetProperty("attributes")
            : default;
        CanEditRules = may.ValueKind == JsonValueKind.Object && Flag(may, "canEditRules");
        CanEditBranding = may.ValueKind == JsonValueKind.Object && Flag(may, "canEditBranding");
        return true;
    }

    public async Task SaveRules(bool onlyAverageLoopSpeed, string? rankerCode)
    {
        var tenantId = TenantId ?? throw new DomainException(Federation_not_yours_string);
        // a ranker that is left empty is sent as null, which clears it: a member that is not named stays as it is
        var rules = new JsonObject
        {
            ["onlyAverageLoopSpeed"] = onlyAverageLoopSpeed,
            ["rankerCode"] = string.IsNullOrWhiteSpace(rankerCode) ? null : rankerCode.Trim(),
        };
        var document = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "tenants",
                ["id"] = tenantId,
                ["attributes"] = new JsonObject { ["regionalRules"] = rules },
            },
        };
        Accepted(await _api.Send(HttpMethod.Patch, Route(tenantId), JsonSerializer.SerializeToElement(document)));
        await ReloadState();
    }

    void Clear()
    {
        TenantId = null;
        Name = string.Empty;
        CanEditRules = false;
        CanEditBranding = false;
        OnlyAverageLoopSpeed = false;
        RankerCode = null;
    }

    static string Route(string tenantId)
    {
        return $"tenants/{Uri.EscapeDataString(tenantId)}";
    }
}
