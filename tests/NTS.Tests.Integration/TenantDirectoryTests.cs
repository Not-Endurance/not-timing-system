using NoTiming.Ui.Features.Account;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The Tenants a person can switch between at the Ui (#645, ADR-0012): the ones they hold a Membership in, named as the Api
/// names them, and only when there is more than one, as there is nothing to choose between otherwise. Over the real Api.
/// </summary>
public sealed class TenantDirectoryTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public TenantDirectoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_person_with_a_Membership_in_one_Tenant_has_nothing_to_switch_between()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Federation of one");
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home);
        using var ui = Open(api, person);

        Assert.Empty(await ui.Tenants.OptionsOfTheAccount());
    }

    [Fact]
    public async Task A_visitor_has_nothing_to_switch_between()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var ui = Open(api, null);

        Assert.Empty(await ui.Tenants.OptionsOfTheAccount());
    }

    [Fact]
    public async Task A_person_with_Memberships_in_more_Tenants_can_switch_between_them_by_the_names_the_Api_gives()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Home Federation");
        var other = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Other Federation");
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            roles: new Dictionary<string, string[]> { [other] = [] }
        );
        using var ui = Open(api, person);

        var options = await ui.Tenants.OptionsOfTheAccount();

        Assert.Equal([(home, "Home Federation"), (other, "Other Federation")], options.Select(x => (x.Id, x.Name)));
    }

    [Fact]
    public async Task A_Tenant_whose_name_cannot_be_read_is_shown_by_its_key()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Home Federation");
        var gone = $"country-gone-{Guid.NewGuid():N}";
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            roles: new Dictionary<string, string[]> { [gone] = [] }
        );
        using var ui = Open(api, person, out var asked);

        var options = await ui.Tenants.OptionsOfTheAccount();
        await ui.Tenants.OptionsOfTheAccount();

        Assert.Equal([(home, "Home Federation"), (gone, gone)], options.Select(x => (x.Id, x.Name)));
        Assert.Equal(2, asked.Asked.Count(x => x.Contains("/tenants/"))); // each Tenant is asked about once, one that is not there too
    }

    [Fact]
    public async Task Switching_to_a_Tenant_makes_it_the_current_one_and_the_names_are_asked_for_once()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Home Federation");
        var other = await TenancySeed.TenantAsync(_mongo.ConnectionString, "Other Federation");
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            roles: new Dictionary<string, string[]> { [other] = [] }
        );
        using var ui = Open(api, person, out var asked);
        await ui.Tenants.OptionsOfTheAccount();
        var asks = asked.Asked.Count(x => x.Contains("/tenants/"));

        await ui.Account.SelectTenant(other);
        await ui.Tenants.OptionsOfTheAccount();

        Assert.Equal(other, ui.Account.Current!.CurrentTenantId);
        Assert.Equal(2, asks);
        Assert.Equal(asks, asked.Asked.Count(x => x.Contains("/tenants/")));
    }

    static Ui Open(ApiFactory api, TenancySeed.Person? person)
    {
        return Open(api, person, out _);
    }

    static Ui Open(ApiFactory api, TenancySeed.Person? person, out JsonApiClients.Requests asked)
    {
        var json = JsonApiClients.Of(api, person, out asked);
        var account = new AccountSession(json);
        return new Ui(account, new TenantDirectory(account, json));
    }

    sealed class Ui : IDisposable
    {
        public Ui(AccountSession account, TenantDirectory tenants)
        {
            Account = account;
            Tenants = tenants;
        }

        public AccountSession Account { get; }
        public TenantDirectory Tenants { get; }

        public void Dispose()
        {
            Account.Dispose();
        }
    }
}
