using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using Not.Events;
using Not.Notify;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NTS.Contracts.Branding;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The logos at the head of what an Event prints, as the Console has them (#646, ADR-0012): the Tenant's, which a Tenant Root edits,
/// an Event's own in place of them, which its Console Operator edits, and what the pages print with, which is what the Api says an
/// Event is printed with. Over the real Api in this process, with the services of the Ui built as the app builds them.
/// </summary>
public sealed class ConsoleBrandingTests : IClassFixture<SharedApiFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;
    static readonly byte[] PNG = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    static readonly byte[] OTHER_PNG = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9, 9];
    static readonly byte[] JPEG = [0xFF, 0xD8, 0xFF, 0xE0, 4, 5, 6];

    readonly SharedApiFixture _shared;

    public ConsoleBrandingTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_Tenant_Root_sets_the_logos_of_its_Tenant_reads_them_again_and_clears_one_of_them()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        await using var ui = ConsoleUi.Open(api, root);
        var branding = ui.Get<IConsoleBranding>();

        await branding.SetOfTenant(LogoPosition.Left, "image/png", PNG);
        await branding.SetOfTenant(LogoPosition.Right, "IMAGE/JPEG", JPEG);
        var both = await branding.OfTenant();
        await branding.ClearOfTenant(LogoPosition.Left);
        var one = await branding.OfTenant();

        Assert.Equal("image/png", both.Left!.ContentType);
        Assert.Equal(PNG, both.Left.Content);
        Assert.Null(both.Left.Source); // what a Tenant keeps is not an Event's or another's
        Assert.Equal("image/jpeg", both.Right!.ContentType);
        Assert.Equal(JPEG, both.Right.Content);
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(PNG)}", both.Left.DataUrl);
        Assert.Equal(PNG.Length, both.Left.Size);
        Assert.Null(one.Left);
        Assert.Equal(JPEG, one.Right!.Content);
        Assert.Equal(tenant, (await StoredAsync($"tenant:{tenant}"))["TenantId"].AsString);
    }

    [Fact]
    public async Task The_Console_Operator_puts_a_logo_of_its_Event_in_place_of_the_Tenants_and_the_pages_print_with_what_the_Api_says()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, NOW);
        await using var rootUi = ConsoleUi.Open(api, root);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Left, "image/png", PNG);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Right, "image/jpeg", JPEG);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var branding = ui.Get<IConsoleBranding>();
        var view = new StubViewedEvent(new NoParticipations(), IntegrationPayloadFactory.EventInformation(eventId));
        var changed = 0;
        branding.ObservableEvent.Subscribe(() => changed++);

        await branding.Show(view);
        var inherited = (branding.LeftLogo, branding.RightLogo);
        await branding.SetOfEvent(eventId, LogoPosition.Left, "image/png", OTHER_PNG);
        var own = (branding.LeftLogo, branding.RightLogo);
        var effective = await branding.OfEvent(eventId);
        await branding.ClearOfEvent(eventId, LogoPosition.Left);

        Assert.Equal((DataUrl("image/png", PNG), DataUrl("image/jpeg", JPEG)), inherited);
        Assert.Equal((DataUrl("image/png", OTHER_PNG), DataUrl("image/jpeg", JPEG)), own);
        Assert.Equal(
            (LogoSource.Event, LogoSource.Tenant),
            (effective.Left!.Source!.Value, effective.Right!.Source!.Value)
        );
        Assert.Equal(DataUrl("image/png", PNG), branding.LeftLogo); // taking its own away gives back the Tenant's
        Assert.True(changed >= 3); // the pages that print are told
    }

    [Fact]
    public async Task The_right_logo_of_an_Event_is_its_own_to_change_and_the_left_one_stays_the_Tenants()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, NOW);
        await using var rootUi = ConsoleUi.Open(api, root);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Left, "image/png", PNG);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Right, "image/jpeg", JPEG);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var branding = ui.Get<IConsoleBranding>();

        await branding.SetOfEvent(eventId, LogoPosition.Right, "image/png", OTHER_PNG);
        var own = await branding.OfEvent(eventId);
        await branding.ClearOfEvent(eventId, LogoPosition.Right);
        var given = await branding.OfEvent(eventId);

        Assert.Equal((LogoSource.Tenant, LogoSource.Event), (own.Left!.Source!.Value, own.Right!.Source!.Value));
        Assert.Equal(OTHER_PNG, own.Right.Content);
        Assert.Equal(PNG, own.Left.Content);
        Assert.Equal((LogoSource.Tenant, LogoSource.Tenant), (given.Left!.Source!.Value, given.Right!.Source!.Value));
        Assert.Equal(JPEG, given.Right.Content); // taking its own away gives back the Tenant's
    }

    [Fact]
    public async Task A_file_of_the_largest_size_is_taken_and_so_is_every_kind_the_Api_takes()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        await using var ui = ConsoleUi.Open(api, root);
        var branding = ui.Get<IConsoleBranding>();
        var largest = new byte[LogoLimits.MAX_BYTES];
        PNG.CopyTo(largest, 0);
        var kinds = new Dictionary<string, byte[]>
        {
            ["image/png"] = PNG,
            ["image/jpeg"] = JPEG,
            ["image/gif"] = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 2],
            ["image/webp"] = [0x52, 0x49, 0x46, 0x46, 0x10, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 1, 2],
        };

        await branding.SetOfTenant(LogoPosition.Left, "image/png", largest); // exactly what a logo may weigh
        var taken = (await branding.OfTenant()).Left!.Size;
        var made = new Dictionary<string, string>();
        foreach (var (kind, image) in kinds)
        {
            await branding.SetOfTenant(LogoPosition.Right, kind, image);
            made[kind] = (await branding.OfTenant()).Right!.ContentType;
        }

        Assert.Equal(LogoLimits.MAX_BYTES, taken);
        Assert.Equal(kinds.Keys, made.Values);
        Assert.Equal(kinds.Keys.Order(), LogoLimits.CONTENT_TYPES.Order()); // the Console tells the person what the Api takes, no more and no less
    }

    [Fact]
    public async Task What_the_Api_does_not_take_from_a_person_is_said_in_its_words_and_nothing_is_kept()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var plain = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, NOW);
        await using var ui = ConsoleUi.Open(api, plain);
        var branding = ui.Get<IConsoleBranding>();

        var tenantRefused = await Assert.ThrowsAsync<DomainException>(
            () => branding.SetOfTenant(LogoPosition.Left, "image/png", PNG)
        );
        var tenantRead = await Assert.ThrowsAsync<DomainException>(() => branding.OfTenant());
        var eventRefused = await Assert.ThrowsAsync<DomainException>(
            () => branding.SetOfEvent(eventId, LogoPosition.Left, "image/png", PNG)
        );
        var missing = await Assert.ThrowsAsync<DomainException>(() => branding.OfEvent(Guid.NewGuid()));

        Assert.All([tenantRefused, tenantRead, eventRefused, missing], x => Assert.NotEmpty(x.Message));
        Assert.Null(await FindAsync($"tenant:{tenant}"));
        Assert.Null(await FindAsync($"event:{eventId}"));
    }

    [Fact]
    public async Task A_file_that_no_server_would_take_is_refused_before_it_is_sent()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var json = JsonApiClients.Of(api, root, out var requests);
        var account = new AccountSession(json);
        var branding = new ConsoleBranding(json, new ConsoleTenant(account, json), new Notifier());

        var notAnImage = await Assert.ThrowsAsync<DomainException>(
            () => branding.SetOfTenant(LogoPosition.Left, "image/svg+xml", PNG)
        );
        var empty = await Assert.ThrowsAsync<DomainException>(
            () => branding.SetOfTenant(LogoPosition.Left, "image/png", [])
        );
        var tooLarge = await Assert.ThrowsAsync<DomainException>(
            () => branding.SetOfTenant(LogoPosition.Left, "image/png", new byte[LogoLimits.MAX_BYTES + 1])
        );

        Assert.Equal(Logo_not_an_image_string, notAnImage.Message);
        Assert.Equal(Logo_not_an_image_string, empty.Message);
        Assert.Equal(string.Format(Logo_is_too_large_string, 512), tooLarge.Message);
        Assert.Empty(requests.Asked.Where(x => x.StartsWith("PATCH", StringComparison.Ordinal)));
        Assert.Null(await FindAsync($"tenant:{tenant}"));
    }

    [Fact]
    public async Task A_page_that_cannot_read_the_logos_prints_without_them_and_says_so()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, NOW);
        await using var rootUi = ConsoleUi.Open(api, root);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Left, "image/png", PNG);
        await rootUi.Get<IConsoleBranding>().SetOfTenant(LogoPosition.Right, "image/jpeg", JPEG);
        var json = JsonApiClients.Of(api, consoleOperator, out _);
        var notifier = new Notifier();
        var warned = new List<string>();
        notifier.Warned.Subscribe(warned.Add);
        var branding = new ConsoleBranding(json, new ConsoleTenant(new AccountSession(json), json), notifier);
        await branding.Show(
            new StubViewedEvent(new NoParticipations(), IntegrationPayloadFactory.EventInformation(eventId))
        );
        var before = (branding.LeftLogo, branding.RightLogo);

        await branding.Show(
            new StubViewedEvent(new NoParticipations(), IntegrationPayloadFactory.EventInformation(Guid.NewGuid()))
        ); // an Event that is not there

        Assert.Equal((DataUrl("image/png", PNG), DataUrl("image/jpeg", JPEG)), before);
        Assert.Null(branding.LeftLogo); // not the logos of the Event that was shown before
        Assert.Null(branding.RightLogo);
        Assert.Single(warned);
    }

    [Fact]
    public async Task The_branding_of_the_Console_is_in_the_container_the_app_builds()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var person = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        await using var ui = ConsoleUi.Open(api, person);

        var branding = ui.Get<IConsoleBranding>();

        Assert.IsType<ConsoleBranding>(branding);
        Assert.Same(branding, ui.Get<IConsoleBranding>());
    }

    static string DataUrl(string contentType, byte[] image)
    {
        return $"data:{contentType};base64,{Convert.ToBase64String(image)}";
    }

    async Task<BsonDocument> StoredAsync(string key)
    {
        return await FindAsync(key) ?? throw new InvalidOperationException($"No branding '{key}' is stored.");
    }

    async Task<BsonDocument?> FindAsync(string key)
    {
        return await RegistrySeed
            .Collection(_shared.MongoConnectionString, "brandings")
            .Find(new BsonDocument("_id", key))
            .FirstOrDefaultAsync();
    }

    sealed class NoParticipations : IParticipationStore
    {
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public IReadOnlyList<Participation> Participations => [];

        public Task Load()
        {
            return Task.CompletedTask;
        }

        public Participation? Find(Guid id)
        {
            return null;
        }

        public Task Refresh(Guid id)
        {
            return Task.CompletedTask;
        }
    }
}
