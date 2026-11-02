using System.Text.Json;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

public class AccountSession : NStatefulService, IAccountSession, IScoped
{
    /// <summary>
    /// The server page that signs a person in and returns to <paramref name="back"/>, a page of the app: the page of the
    /// address the person was on. A way back that is not a path of this site (another site, a scheme, a double slash) is
    /// not passed on, as the page would be a way to send a person who just signed in somewhere else.
    /// </summary>
    public static string SignInUrl(string? back)
    {
        var isPath = back is { Length: > 1 } && back[0] == '/' && back[1] != '/' && back[1] != '\\';
        return isPath ? $"/sign-in?returnUrl={Uri.EscapeDataString(back!)}" : "/sign-in";
    }

    readonly AccountApiRepository _accounts;

    public AccountSession(AccountApiRepository accounts)
    {
        _accounts = accounts;
    }

    public CurrentAccount? Current { get; private set; }

    public bool IsSignedIn => Current != null;

    public bool IsKnown { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        // A visitor is none; a host that could not say is an exception, so that it is asked again.
        var me = await _accounts.Me();
        Current = me is { } account ? AccountOf(account) : null;
        IsKnown = true;
        return true;
    }

    public Task Refresh()
    {
        return ReloadState();
    }

    public async Task SignOut()
    {
        await _accounts.SignOut();
        Current = null;
        IsKnown = true;
        EmitChanged();
    }

    public async Task SelectTenant(string? tenantId)
    {
        var account = Current ?? throw new InvalidOperationException("Nobody is signed in.");
        Current = AccountOf(await _accounts.SelectTenant(account.Id, tenantId));
        EmitChanged();
    }

    static CurrentAccount AccountOf(JsonElement data)
    {
        var attributes = data.GetProperty("attributes");
        return new CurrentAccount
        {
            Id = Guid.Parse(data.GetProperty("id").GetString()!),
            Email = attributes.GetProperty("email").GetString()!,
            EmailConfirmed = Flag(attributes, "emailConfirmed"),
            Name = Text(attributes, "name"),
            Passkeys = attributes.TryGetProperty("passkeys", out var passkeys) ? passkeys.GetInt32() : 0,
            ProfileComplete = Flag(attributes, "profileComplete"),
            HomeTenantId = Text(attributes, "homeTenantId"),
            SelectedTenantId = Text(attributes, "selectedTenantId"),
            CurrentTenantId = Text(attributes, "currentTenantId"),
            Memberships = MembershipsOf(attributes),
            IsDeveloper = Flag(attributes, "isDeveloper"),
        };
    }

    static IReadOnlyList<AccountMembership> MembershipsOf(JsonElement attributes)
    {
        if (
            !attributes.TryGetProperty("memberships", out var memberships)
            || memberships.ValueKind != JsonValueKind.Array
        )
        {
            return [];
        }

        return
        [
            .. memberships
                .EnumerateArray()
                .Select(x => new AccountMembership
                {
                    TenantId = x.GetProperty("tenantId").GetString()!,
                    Roles =
                        x.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array
                            ? [.. roles.EnumerateArray().Select(role => role.GetString()!)]
                            : [],
                }),
        ];
    }

    static bool Flag(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.True;
    }

    static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
