using System.Net;
using System.Text.Json;
using Not.Application.HTTP;
using Not.Injection;
using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

/// <summary>A Tenant a person can switch to, named as the Api names it.</summary>
public sealed class TenantOption
{
    public TenantOption(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }
}

/// <summary>The Tenants a person can switch between (#645, ADR-0012).</summary>
public interface ITenantDirectory
{
    /// <summary>
    /// The Tenants the person holds a Membership in, named, in the order of the Memberships; none when they hold fewer than
    /// two, as there is nothing to choose between.
    /// </summary>
    Task<IReadOnlyList<TenantOption>> OptionsOfTheAccount();
}

public class TenantDirectory : ITenantDirectory, IScoped
{
    readonly IAccountSession _account;
    readonly JsonApiClient _api;
    readonly Dictionary<string, string> _names = [];

    public TenantDirectory(IAccountSession account, JsonApiClient api)
    {
        _account = account;
        _api = api;
    }

    public async Task<IReadOnlyList<TenantOption>> OptionsOfTheAccount()
    {
        await _account.Load();
        if (_account.Current is not { Memberships.Count: > 1 } person)
        {
            return [];
        }

        var options = new List<TenantOption>();
        foreach (var membership in person.Memberships)
        {
            options.Add(new TenantOption(membership.TenantId, await NameOf(membership.TenantId)));
        }

        return options;
    }

    /// <summary>
    /// The name the Api gives the Tenant, asked for once. A Tenant that is not there is shown by its key, and that answer is
    /// kept; one the Api could not be asked about is shown by its key now and asked about again.
    /// </summary>
    async Task<string> NameOf(string id)
    {
        if (_names.TryGetValue(id, out var known))
        {
            return known;
        }

        try
        {
            var response = await _api.Send(HttpMethod.Get, $"tenants/{Uri.EscapeDataString(id)}");
            if (response is { IsSuccess: true, Document: { } document })
            {
                var attributes = document.GetProperty("data").GetProperty("attributes");
                if (attributes.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    return _names[id] = name.GetString()!;
                }
            }

            if (response.Status == HttpStatusCode.NotFound)
            {
                return _names[id] = id;
            }
        }
        catch (HttpRequestException)
        {
            // The Api could not be reached: the key stands in for the name until it is asked again.
        }

        return id;
    }
}
