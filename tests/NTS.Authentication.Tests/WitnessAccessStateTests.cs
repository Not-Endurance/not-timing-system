using System.Linq.Expressions;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.CRUD.Ports;
using Not.Application.RPC.SignalR;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Witness.Blazor;
using NTS.Witness.Blazor.Features;
using NTS.Witness.Features.Access;

namespace NTS.Authentication.Tests;

public class WitnessAccessStateTests
{
    [Fact]
    public async Task Access_state_returns_unknown_when_no_event_is_connected()
    {
        var service = CreateService(userId: 7, officials: []);

        await service.Load();

        Assert.Equal(WitnessAccessLevel.Unknown, service.AccessLevel);
        Assert.False(WitnessAccessPolicy.CanViewSnapshots(service.AccessLevel));
    }

    [Fact]
    public async Task Access_state_returns_official_when_current_user_matches_event_official()
    {
        var service = CreateService(
            userId: 7,
            officials:
            [
                new Official(new Person(["Judge", "Official"]), NTS.Domain.Enums.OfficialRole.GroundJuryPresident, 11, userId: 7),
            ],
            eventId: 11
        );

        await service.Load();

        Assert.Equal(WitnessAccessLevel.Official, service.AccessLevel);
        Assert.True(WitnessAccessPolicy.CanViewSnapshots(service.AccessLevel));
    }

    [Fact]
    public async Task Access_state_returns_participant_when_current_user_is_not_an_event_official()
    {
        var service = CreateService(
            userId: 7,
            officials:
            [
                new Official(new Person(["Judge", "Other"]), NTS.Domain.Enums.OfficialRole.GroundJuryPresident, 11, userId: 99),
            ],
            eventId: 11
        );

        await service.Load();

        Assert.Equal(WitnessAccessLevel.Participant, service.AccessLevel);
        Assert.False(WitnessAccessPolicy.CanViewSnapshots(service.AccessLevel));
    }

    [Fact]
    public void Access_policy_routes_officials_to_snapshots_and_participants_to_performance()
    {
        Assert.Equal(Routes.SNAPSHOT_PAGE, WitnessAccessPolicy.ResolveHomeRoute(WitnessAccessLevel.Official));
        Assert.Equal(Routes.PERFORMANCE_PAGE, WitnessAccessPolicy.ResolveHomeRoute(WitnessAccessLevel.Participant));
    }

    [Fact]
    public void Access_policy_only_redirects_snapshot_page_for_participants()
    {
        Assert.True(WitnessAccessPolicy.ShouldRedirectFromSnapshots(WitnessAccessLevel.Participant));
        Assert.False(WitnessAccessPolicy.ShouldRedirectFromSnapshots(WitnessAccessLevel.Official));
        Assert.False(WitnessAccessPolicy.ShouldRedirectFromSnapshots(WitnessAccessLevel.Unknown));
    }

    static WitnessAccessContext CreateService(int userId, IEnumerable<Official> officials, int? eventId = null)
    {
        return new WitnessAccessContext(
            new TestSocketContext(eventId),
            new TestUserSession(userId),
            new TestOfficialReader(officials)
        );
    }

    sealed class TestUserSession : INUserSession
    {
        readonly int _userId;

        public TestUserSession(int userId)
        {
            _userId = userId;
        }

        public Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
            where TSessionState : class
        {
            INUserSessionModel<TSessionState> session = new NUserSessionModel<TSessionState>(
                "entra-1",
                new NUserModel("user@example.com", id: _userId)
            );
            return Task.FromResult<INUserSessionModel<TSessionState>?>(session);
        }
    }

    sealed class TestOfficialReader : IReadMany<Official>
    {
        readonly IReadOnlyList<Official> _officials;

        public TestOfficialReader(IEnumerable<Official> officials)
        {
            _officials = officials.ToList();
        }

        public Task<IEnumerable<Official>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Official>>(_officials);
        }

        public Task<IEnumerable<Official>> ReadMany(Expression<Func<Official, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Official>>(_officials.Where(predicate).ToList());
        }
    }

    sealed class TestSocketContext : INtsSocketContext
    {
        public TestSocketContext(int? eventId)
        {
            Event = eventId == null ? null : CreateEvent(eventId.Value);
        }

        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; }

        static EnduranceEvent CreateEvent(int eventId)
        {
            var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
            return new EnduranceEvent(
                new PopulatedPlace(country, "Sofia", "Ring"),
                new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
                null,
                null,
                null,
                eventId
            );
        }
    }
}
