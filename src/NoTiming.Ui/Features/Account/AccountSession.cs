using System.Net;
using System.Text.Json;
using Not.Application.Behinds.Adapters;
using Not.Application.HTTP;
using Not.Injection;
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

    readonly JsonApiClient _api;

    public AccountSession(JsonApiClient api)
    {
        _api = api;
    }

    public CurrentAccount? Current { get; private set; }

    public bool IsSignedIn => Current != null;

    public bool IsKnown { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        var response = await _api.Send(HttpMethod.Get, "me");
        if (response.Status == HttpStatusCode.Unauthorized)
        {
            Current = null;
            IsKnown = true;
            return true;
        }

        if (!response.IsSuccess)
        {
            throw response.ToException(); // not a visitor: the host could not say, so ask again
        }

        Current = AccountOf(response.Document!.Value);
        IsKnown = true;
        return true;
    }

    public Task Refresh()
    {
        return ReloadState();
    }

    public async Task SignOut()
    {
        var response = await _api.Send(HttpMethod.Delete, "sessions/current");
        if (!response.IsSuccess)
        {
            throw response.ToException();
        }

        Current = null;
        IsKnown = true;
        EmitChanged();
    }

    public async Task SelectTenant(string? tenantId)
    {
        var account = Current ?? throw new InvalidOperationException("Nobody is signed in.");
        var document = JsonSerializer.SerializeToElement(
            new
            {
                data = new
                {
                    type = "accounts",
                    id = account.Id.ToString(),
                    attributes = new { selectedTenantId = tenantId },
                },
            },
            JsonApiClient.WriteOptions
        );
        var response = await _api.Send(HttpMethod.Patch, "me", document);
        if (!response.IsSuccess)
        {
            throw response.ToException();
        }

        Current = AccountOf(response.Document!.Value);
        EmitChanged();
    }

    static CurrentAccount AccountOf(JsonElement document)
    {
        var data = document.GetProperty("data");
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
