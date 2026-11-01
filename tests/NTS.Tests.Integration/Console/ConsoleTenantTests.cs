using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The federation the person acts in, as the Console's page of it shows it (#646, ADR-0012): its name and the rules of its Regional
/// competitions, what the person may do about them, which is what the Api says, and the saving of the rules. Over the real Api in
/// this process.
/// </summary>
public sealed class ConsoleTenantTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public ConsoleTenantTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_Tenant_Root_reads_the_rules_of_its_Tenant_and_saves_them_and_the_Api_keeps_them()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        using var console = Open(api, root);

        await console.Tenant.Load();
        var before = (console.Tenant.OnlyAverageLoopSpeed, console.Tenant.RankerCode);
        await console.Tenant.SaveRules(true, "  BG-2026 ");

        Assert.Equal(tenant, console.Tenant.TenantId);
        Assert.NotEmpty(console.Tenant.Name);
        Assert.True(console.Tenant.CanEdit);
        Assert.Equal((false, null), before);
        Assert.Equal((true, "BG-2026"), (console.Tenant.OnlyAverageLoopSpeed, console.Tenant.RankerCode));
        Assert.Equal((true, "BG-2026"), await StoredRulesAsync(tenant));
    }

    [Fact]
    public async Task A_ranker_that_is_left_empty_is_the_ranker_of_the_FEI()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        using var console = Open(api, root);
        await console.Tenant.Load();
        await console.Tenant.SaveRules(false, "BG-2026");

        await console.Tenant.SaveRules(false, "  ");

        Assert.Null(console.Tenant.RankerCode);
        Assert.Equal((false, null), await StoredRulesAsync(tenant));
    }

    [Fact]
    public async Task Rules_that_the_Api_does_not_take_are_said_in_its_words_and_nothing_is_saved()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        using var console = Open(api, root);
        await console.Tenant.Load();

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => console.Tenant.SaveRules(true, new string('x', 41))
        );

        Assert.NotEmpty(refused.Message);
        Assert.False(console.Tenant.OnlyAverageLoopSpeed);
        Assert.Equal((false, null), await StoredRulesAsync(tenant));
    }

    [Fact]
    public async Task A_person_who_is_not_a_Tenant_Root_may_edit_nothing_and_the_Api_refuses_what_it_tries()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var plain = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        using var console = Open(api, plain);

        await console.Tenant.Load();
        var refused = await Assert.ThrowsAsync<DomainException>(() => console.Tenant.SaveRules(true, "BG-2026"));

        Assert.Equal(tenant, console.Tenant.TenantId);
        Assert.False(console.Tenant.CanEdit);
        Assert.NotEmpty(refused.Message);
        Assert.Equal((false, null), await StoredRulesAsync(tenant));
    }

    [Fact]
    public async Task What_the_Api_cannot_say_of_the_Tenant_is_not_what_was_known_of_it_before()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var json = JsonApiClients.Of(api, root, out var requests);
        var account = new AccountSession(json);
        using var console = new Opened(account, new ConsoleTenant(account, json));
        await console.Tenant.Load();
        var before = (console.Tenant.TenantId, console.Tenant.CanEdit);
        requests.Answer = request =>
            request.RequestUri!.AbsolutePath.StartsWith("/api/tenants/", StringComparison.Ordinal)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                : null;

        await account.Refresh(); // the account is asked again, and the Tenant with it

        Assert.Equal((tenant, true), before);
        await UiBrowser.Eventually(async () => console.Tenant.TenantId == null);
        Assert.False(console.Tenant.CanEdit);
        Assert.Empty(console.Tenant.Name);
    }

    [Fact]
    public async Task A_person_who_acts_in_no_Tenant_has_no_federation()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var nobody = await SignedInAsync(api, client, _shared.MongoConnectionString, null);
        using var console = Open(api, nobody);

        await console.Tenant.Load();

        Assert.Null(console.Tenant.TenantId);
        Assert.False(console.Tenant.CanEdit);
        await Assert.ThrowsAsync<DomainException>(() => console.Tenant.SaveRules(true, null));
    }

    async Task<(bool OnlyAverageLoopSpeed, string? RankerCode)> StoredRulesAsync(string tenant)
    {
        var stored = await Tenants(_shared.MongoConnectionString).Find(new BsonDocument("_id", tenant)).FirstAsync();
        if (!stored.TryGetValue("RegionalRules", out var value) || value is not BsonDocument rules)
        {
            return (false, null);
        }

        return (
            rules.GetValue("OnlyAverageLoopSpeed", false).AsBoolean,
            rules.TryGetValue("RankerCode", out var code) && code.IsString ? code.AsString : null
        );
    }

    static Opened Open(ApiFactory api, Person person)
    {
        var json = JsonApiClients.Of(api, person, out _);
        var account = new AccountSession(json);
        return new Opened(account, new ConsoleTenant(account, json));
    }

    sealed class Opened : IDisposable
    {
        public Opened(AccountSession account, ConsoleTenant tenant)
        {
            Account = account;
            Tenant = tenant;
        }

        public AccountSession Account { get; }
        public ConsoleTenant Tenant { get; }

        public void Dispose()
        {
            Tenant.Dispose();
            Account.Dispose();
        }
    }
}
