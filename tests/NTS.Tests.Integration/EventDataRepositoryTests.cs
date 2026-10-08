using System.Net;
using System.Text;
using NoTiming.Ui.Features.Access;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What an Event keeps as the Ui reaches it (#604, ADR-0006, ADR-0013): the repositories of the Participations, Rankings,
/// Officials and Handouts over the real Api, on the JSON:API variant of the client's repository, and the access context of
/// the Witness, which asks the Api what the person may do and does not guess it from the lists of Officials and Operators.
/// A list names the Event it is of, a Participation is read with its version and changed against it, and what the Api refuses
/// comes back with its code.
/// </summary>
public sealed class EventDataRepositoryTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EventDataRepositoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_scoped_repository_lists_the_rows_of_the_selected_Event_with_the_Event_at_the_head_of_every_filter()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var other = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var first = Ridden(eventId, 1);
        var second = Ridden(eventId, 2);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, first, version: 3);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, second);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, Ridden(other, 9));
        var participations = new ParticipationEventScopedApiRepository(
            JsonApiClients.Of(api, null, out var requests),
            new EventScopeFactory<Participation>(new SelectedEvent(eventId))
        );

        var all = (await participations.ReadMany()).ToList();
        var narrowed = (await participations.ReadMany(x => x.Combination.Number > 1)).ToList();
        var one = await participations.Read(first.Id);

        Assert.Equal(new[] { first.Id, second.Id }.Order(), all.Select(x => x.Id).Order());
        Assert.Equal(3, all.Single(x => x.Id == first.Id).Version);
        Assert.Equal(0, all.Single(x => x.Id == second.Id).Version);
        Assert.Equal([second.Id], narrowed.Select(x => x.Id));
        Assert.Equal(3, one!.Version);
        Assert.Equal(START.AddHours(1), one.Phases[0].Events.Single().Time);
        var lists = requests
            .Asked.Select(Uri.UnescapeDataString)
            .Where(x => x.StartsWith("GET /api/participations?", StringComparison.Ordinal));
        Assert.All(lists, x => Assert.Contains($"eventId eq {eventId}", x));
        Assert.Contains(requests.Asked, x => x == $"GET /api/participations/{first.Id}");
        Assert.Null(participations.LastError);
    }

    [Fact]
    public async Task A_repository_that_is_not_scoped_reads_what_the_filter_of_the_call_names_and_nothing_without_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var other = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var mine = IntegrationPayloadFactory.Official(eventId, null, Guid.NewGuid());
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, mine);
        await EventSeed.OfficialAsync(
            _mongo.ConnectionString,
            tenant,
            IntegrationPayloadFactory.Official(other, null, Guid.NewGuid())
        );
        var officials = new OfficialApiRepository(JsonApiClients.Of(api, null, out _));

        var ofTheEvent = (await officials.ReadMany(x => x.EventId == eventId)).ToList();
        var error = officials.LastError;
        var unnamed = (await officials.ReadMany()).ToList();

        Assert.Equal([mine.Id], ofTheEvent.Select(x => x.Id));
        Assert.Null(error);
        Assert.Empty(unnamed); // the Api answers no list that does not say whose
        Assert.Equal("event-required", officials.LastError?.Code);
    }

    [Fact]
    public async Task A_Participation_is_changed_against_the_version_it_was_read_at_and_the_Api_refuses_one_that_was_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 2);
        var participations = new ParticipationEventScopedApiRepository(
            JsonApiClients.Of(api, mainOperator, out var requests),
            new EventScopeFactory<Participation>(new SelectedEvent(eventId))
        );
        var read = (await participations.Read(participation.Id))!;
        var staleRead = (await participations.Read(participation.Id))!;

        read.Withdraw();
        await participations.Update(read);
        var taken = (
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        )!;
        var error = participations.LastError;
        staleRead.Retire();
        await participations.Update(staleRead);

        Assert.Null(error);
        Assert.Equal(3, taken["Version"].AsInt32);
        Assert.Equal("WD", taken["Eliminated"]["Code"].AsString);
        Assert.Equal(eventId, taken["EventId"].AsGuid);
        Assert.Equal("participation-changed", participations.LastError?.Code);
        var stored = (
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        )!;
        Assert.Equal(3, stored["Version"].AsInt32);
        Assert.Equal("WD", stored["Eliminated"]["Code"].AsString);
        Assert.Equal(2, requests.Asked.Count(x => x == $"PATCH /api/participations/{participation.Id}"));
    }

    [Fact]
    public async Task What_the_Api_refuses_of_a_Participation_comes_back_with_its_code()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        var scope = new EventScopeFactory<Participation>(new SelectedEvent(eventId));
        var asMember = new ParticipationEventScopedApiRepository(JsonApiClients.Of(api, member, out _), scope);
        var read = (await asMember.Read(participation.Id))!;

        read.Withdraw();
        await asMember.Update(read);

        Assert.Equal("not-main-operator", asMember.LastError?.Code);
        Assert.False(
            (
                await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
            )!.Contains("Eliminated")
        );
    }

    [Fact]
    public async Task A_Ranking_is_made_changed_and_removed_through_the_repository_by_the_Main_Operator()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var rankings = new RankingEventScopedApiRepository(
            JsonApiClients.Of(api, mainOperator, out var requests),
            new EventScopeFactory<Ranking>(new SelectedEvent(eventId))
        );
        var ranking = IntegrationPayloadFactory.Ranking(eventId, [participation], Guid.NewGuid(), "CEI 1*");

        await rankings.Create(ranking);
        var made = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_rankings", ranking.Id))!;
        await rankings.Update(IntegrationPayloadFactory.Ranking(eventId, [participation], ranking.Id, "CEI 2*"));
        var changed = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_rankings", ranking.Id))!;
        var read = await rankings.Read(ranking.Id);
        await rankings.Delete(ranking.Id);

        Assert.Equal("CEI 1*", made["Name"].AsString);
        Assert.Equal(tenant, made["TenantId"].AsString);
        Assert.Equal(eventId, made["EventId"].AsGuid);
        Assert.Equal("CEI 2*", changed["Name"].AsString);
        Assert.Equal("CEI 2*", read!.Name);
        Assert.Equal([participation.Id], read.Entries.Select(x => x.ParticipationId));
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_rankings", ranking.Id));
        Assert.Null(rankings.LastError);
        Assert.Equal(
            [
                "POST /api/rankings",
                $"PATCH /api/rankings/{ranking.Id}",
                $"GET /api/rankings/{ranking.Id}",
                $"DELETE /api/rankings/{ranking.Id}",
            ],
            requests.Asked
        );
    }

    [Fact]
    public async Task The_account_an_Official_is_linked_to_is_not_sent_and_the_Official_is_made_all_the_same()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var officials = new OfficialEventScopedApiRepository(
            JsonApiClients.Of(api, mainOperator, out _),
            new EventScopeFactory<Official>(new SelectedEvent(eventId))
        );
        var linked = IntegrationPayloadFactory.Official(eventId, TestId.Of(77), Guid.NewGuid());

        await officials.Create(linked);

        Assert.Null(officials.LastError);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_officials", linked.Id))!;
        Assert.Equal("Integration Official", stored["Name"].AsString);
        Assert.False(stored.Contains("UserId"));
        Assert.Null((await officials.Read(linked.Id))!.UserId);
    }

    [Fact]
    public async Task A_Handout_is_found_by_the_Participation_it_is_of_within_the_Event()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var first = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 2, Guid.NewGuid());
        var handout = IntegrationPayloadFactory.Handout(first, Guid.NewGuid());
        await EventSeed.HandoutAsync(_mongo.ConnectionString, tenant, handout);
        await EventSeed.HandoutAsync(
            _mongo.ConnectionString,
            tenant,
            IntegrationPayloadFactory.Handout(second, Guid.NewGuid())
        );
        var handouts = new HandoutEventScopedApiRepository(
            JsonApiClients.Of(api, null, out var requests),
            new EventScopeFactory<Handout>(new SelectedEvent(eventId))
        );

        var found = (await handouts.ReadMany(x => x.ParticipationId == first.Id)).ToList();

        Assert.Equal([handout.Id], found.Select(x => x.Id));
        Assert.Contains(
            requests.Asked.Select(Uri.UnescapeDataString),
            x => x.Contains($"eventId eq {eventId}") && x.Contains($"participationId eq {first.Id}")
        );
    }

    [Fact]
    public async Task What_the_Api_answers_is_not_taken_for_a_list_when_it_does_not_arrive_whole()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, Ridden(eventId, 1));
        var participations = new ParticipationEventScopedApiRepository(
            JsonApiClients.Of(api, null, out var requests),
            new EventScopeFactory<Participation>(new SelectedEvent(eventId))
        );
        requests.Answer = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                """{"errors":[{"code":"unexpected"}]}""",
                Encoding.UTF8,
                "application/vnd.api+json"
            ),
        };

        var list = (await participations.ReadMany()).ToList();

        Assert.Empty(list);
        Assert.Equal("unexpected", participations.LastError?.Code);
    }

    [Fact]
    public async Task Whether_a_person_may_send_Snapshots_is_asked_of_the_Api_and_not_worked_out_from_the_Officials_and_Operators()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var steward = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var veterinary = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var visitor = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            steward.Email,
            steward.Id
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "VeterinaryCommissionMember",
            veterinary.Email,
            veterinary.Id
        );
        // What the legacy lists said is not what counts: an Official that only the documents of the Event name is nobody's grant.
        await EventSeed.OfficialAsync(
            _mongo.ConnectionString,
            tenant,
            IntegrationPayloadFactory.Official(eventId, visitor.Id, Guid.NewGuid())
        );

        var anonymous = await AccessLevelOfAsync(api, null, eventId);
        var withoutAnEvent = await AccessLevelOfAsync(api, visitor, null);
        var asVisitor = await AccessLevelOfAsync(api, visitor, eventId);
        var asVeterinary = await AccessLevelOfAsync(api, veterinary, eventId);
        var asSteward = await AccessLevelOfAsync(api, steward, eventId);
        var asMainOperator = await AccessLevelOfAsync(api, mainOperator, eventId);

        Assert.Equal(WitnessAccessLevel.Anonymous, anonymous);
        Assert.Equal(WitnessAccessLevel.Registered, withoutAnEvent);
        Assert.Equal(WitnessAccessLevel.Registered, asVisitor);
        Assert.Equal(WitnessAccessLevel.Registered, asVeterinary);
        Assert.Equal(WitnessAccessLevel.Official, asSteward);
        Assert.Equal(WitnessAccessLevel.Official, asMainOperator);
    }

    [Fact]
    public async Task A_person_who_is_not_signed_in_to_the_Api_is_a_visitor_and_may_not_send_Snapshots()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);

        var level = await AccessLevelOfAsync(api, null, eventId);

        Assert.Equal(WitnessAccessLevel.Anonymous, level);
    }

    [Fact]
    public async Task A_filter_the_Api_cannot_be_asked_for_is_applied_to_what_the_Event_has_and_not_to_what_the_Tenant_has()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var other = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var steward = new Official("Steward Of The Event", null, OfficialRole.Steward, eventId, Guid.NewGuid(), null);
        var judge = new Official("Judge Of The Event", null, OfficialRole.GroundJury, eventId, Guid.NewGuid(), null);
        var elsewhere = new Official("Steward Of Another", null, OfficialRole.Steward, other, Guid.NewGuid(), null);
        foreach (var official in new[] { steward, judge, elsewhere })
        {
            await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, official);
        }

        var officials = new OfficialEventScopedApiRepository(
            JsonApiClients.Of(api, null, out var requests),
            new EventScopeFactory<Official>(new SelectedEvent(eventId))
        );

        var stewards = (await officials.ReadMany(x => x.Role == OfficialRole.Steward)).ToList();

        Assert.Equal([steward.Id], stewards.Select(x => x.Id)); // an enum is not written for the Api: the repository filters
        var lists = requests.Asked.Select(Uri.UnescapeDataString).ToList();
        Assert.All(lists, x => Assert.Contains($"eventId eq {eventId}", x));
        Assert.DoesNotContain(lists, x => x.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.Null(officials.LastError);
    }

    [Fact]
    public async Task An_Api_that_cannot_be_reached_leaves_the_person_registered_and_the_state_to_be_loaded_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var json = JsonApiClients.Of(api, mainOperator, out var requests);
        using var account = new AccountSession(json);
        await account.Load();
        using var context = new WitnessAccessContext(new SelectedEvent(eventId), account, json);
        requests.Answer = _ => throw new HttpRequestException("The Api is down.");

        await context.Load();
        var whileItIsDown = context.AccessLevel;
        requests.Answer = null;
        await context.Load();

        Assert.Equal(WitnessAccessLevel.Registered, whileItIsDown);
        Assert.Equal(WitnessAccessLevel.Official, context.AccessLevel);
    }

    async Task<WitnessAccessLevel> AccessLevelOfAsync(ApiFactory api, Person? person, Guid? eventId)
    {
        var json = JsonApiClients.Of(api, person, out _);
        using var account = new AccountSession(json);
        using var context = new WitnessAccessContext(new SelectedEvent(eventId), account, json);

        await context.Load();

        return context.AccessLevel;
    }

    /// <summary>A Participation of two Phases with an arrival recorded in the first.</summary>
    static Participation Ridden(Guid eventId, int number)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
            eventId,
            number,
            Guid.NewGuid(),
            startTime: START
        );
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(number, START.AddHours(1)),
            TestId.Of(9),
            START.AddHours(2)
        );
        return participation;
    }
}
