using Not.Application.CRUD.Ports;
using Not.Injection;
using NoTiming.Ui.Storage.REST;
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
    readonly TenantApiRepository _tenants;
    readonly Dictionary<string, string> _names = [];

    public TenantDirectory(IAccountSession account, TenantApiRepository tenants)
    {
        _account = account;
        _tenants = tenants;
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
            var tenant = await _tenants.Read(id);
            if (tenant == null)
            {
                return _names[id] = id;
            }

            if (!string.IsNullOrEmpty(tenant.Name))
            {
                return _names[id] = tenant.Name;
            }
        }
        catch (RepositoryException)
        {
            // The Api could not be asked, or refused: the key stands in for the name until it is asked again.
        }

        return id;
    }
}
