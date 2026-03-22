using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Events;
using Not.Structures;
using NTS.Application.Socket;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Domain.Aggregates;
using NTS.Domain.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Blazor.Features.Sessions;
using NTS.Witness.Features.Sessions;

namespace NTS.Authentication.Tests;

public class WitnessSessionIdentityTests
{
    [Fact]
    public void Resolve_user_identifier_prefers_oid_claim()
    {
        var principal = CreatePrincipal(
            new Claim("oid", "entra-oid"),
            new Claim("sub", "entra-sub"),
            new Claim(ClaimTypes.NameIdentifier, "name-id")
        );

        var result = NUserClaimsHelper.ResolveUserIdentifier(principal);

        Assert.Equal("entra-oid", result);
    }

    [Fact]
    public void Resolve_user_identifier_falls_back_to_schema_objectidentifier()
    {
        var principal = CreatePrincipal(
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "schema-oid"),
            new Claim("sub", "entra-sub")
        );

        var result = NUserClaimsHelper.ResolveUserIdentifier(principal);

        Assert.Equal("schema-oid", result);
    }

    [Fact]
    public void Resolve_user_identifier_falls_back_to_sub_and_nameidentifier()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "entra-sub"),
            new Claim(ClaimTypes.NameIdentifier, "name-id")
        );

        var result = NUserClaimsHelper.ResolveUserIdentifier(principal);

        Assert.Equal("entra-sub", result);
    }

    [Fact]
    public async Task Witness_user_session_persists_user_identifier_on_new_session()
    {
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"), new Claim("oid", "entra-1"))
        );
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("user@example.com", id: 7)),
        };
        var sessions = new RecordingUserSessionRepository();
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        await service.SetEventId(17);

        var created = Assert.Single(sessions.Created);
        Assert.Equal(7, created.Id);
        Assert.Equal("entra-1", created.UserIdentifier);
        Assert.Equal(17, created.EventId);
    }

    [Fact]
    public async Task Witness_user_session_reads_by_user_identifier_before_local_user_id()
    {
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"), new Claim("oid", "entra-1"))
        );
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("user@example.com", id: 7)),
        };
        var expected = new UserSessionModel { Id = 99, UserIdentifier = "entra-1", EventId = 23 };
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = expected };
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        var current = await service.GetCurrent();

        Assert.Same(expected, current);
        Assert.Equal(1, sessions.ReadByUserIdentifierCalls);
        Assert.Equal(0, sessions.ReadIdCalls);
    }

    [Fact]
    public async Task Witness_user_session_backfills_legacy_session_with_user_identifier()
    {
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"), new Claim("oid", "entra-1"))
        );
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("user@example.com", id: 7)),
        };
        var legacySession = new UserSessionModel { Id = 7, EventId = 5 };
        var sessions = new RecordingUserSessionRepository { ReadByIdResult = legacySession };
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        var current = await service.GetCurrent();

        Assert.Same(legacySession, current);
        var updated = Assert.Single(sessions.Updated);
        Assert.Same(legacySession, updated);
        Assert.Equal("entra-1", updated.UserIdentifier);
    }

    [Fact]
    public async Task Witness_user_session_deletes_session_resolved_by_user_identifier()
    {
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"), new Claim("oid", "entra-1"))
        );
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("user@example.com", id: 7)),
        };
        var expected = new UserSessionModel { Id = 99, UserIdentifier = "entra-1", EventId = 23 };
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = expected };
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        await service.DeleteCurrent();

        var deleted = Assert.Single(sessions.Deleted);
        Assert.Same(expected, deleted);
        Assert.Equal(0, sessions.ReadIdCalls);
    }

    [Fact]
    public async Task Witness_user_session_does_not_create_session_when_identifier_is_missing()
    {
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"))
        );
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("user@example.com", id: 7)),
        };
        var sessions = new RecordingUserSessionRepository();
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        await service.SetEventId(17);

        Assert.Empty(sessions.Created);
        Assert.Equal(0, users.GetCalls);
    }

    [Fact]
    public async Task Witness_user_session_initialize_disconnects_when_current_user_has_no_session()
    {
        var sessionService = new SequenceUserSessionService(
            new UserSessionModel { EventId = 1 },
            null
        );
        var events = new RecordingEnduranceEventRepository(CreateEnduranceEvent(1));
        var socket = new RecordingSocketService();
        var session = new WitnessUserSession(sessionService, events, socket);

        await session.Initialize();
        await session.Initialize();

        Assert.Equal([1], socket.ConnectCalls);
        Assert.Equal(1, socket.DisconnectCalls);
        Assert.Null(socket.Event);
    }

    [Fact]
    public async Task Witness_user_session_initialize_reconnects_when_session_event_changes()
    {
        var sessionService = new SequenceUserSessionService(
            new UserSessionModel { EventId = 1 },
            new UserSessionModel { EventId = 2 }
        );
        var events = new RecordingEnduranceEventRepository(CreateEnduranceEvent(1), CreateEnduranceEvent(2));
        var socket = new RecordingSocketService();
        var session = new WitnessUserSession(sessionService, events, socket);

        await session.Initialize();
        await session.Initialize();

        Assert.Equal([1, 2], socket.ConnectCalls);
        Assert.Equal(1, socket.DisconnectCalls);
        Assert.Equal(2, socket.Event?.Id);
    }

    [Fact]
    public async Task Witness_user_session_initialize_deletes_missing_event_session_and_disconnects()
    {
        var sessionService = new SequenceUserSessionService(new UserSessionModel { EventId = 42 });
        var events = new RecordingEnduranceEventRepository();
        var socket = new RecordingSocketService
        {
            Event = CreateEnduranceEvent(7),
            IsConnected = true,
        };
        var session = new WitnessUserSession(sessionService, events, socket);

        await session.Initialize();

        Assert.Equal(1, sessionService.DeleteCurrentCalls);
        Assert.Equal(1, socket.DisconnectCalls);
        Assert.Null(socket.Event);
    }

    static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    static EnduranceEvent CreateEnduranceEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            new PopulatedPlace(country, "Sofia", "Sofia"),
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class StaticAuthenticationStateProvider : AuthenticationStateProvider
    {
        readonly AuthenticationState _state;

        public StaticAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_state);
        }
    }

    sealed class RecordingUserRegister : IUserRegister
    {
        public Result<NUserModel>? GetResult { get; init; }
        public Result<NUserModel>? RegisterResult { get; init; }
        public int GetCalls { get; private set; }

        public Task<Result<NUserModel>> Get(string email)
        {
            GetCalls++;
            return Task.FromResult(GetResult ?? Result.Success<NUserModel>(null!));
        }

        public Task<Result<NUserModel>> Register(NUserRegistration registration)
        {
            return Task.FromResult(
                RegisterResult
                    ?? Result.Success(new NUserModel(registration.Email, id: 7))
            );
        }
    }

    sealed class RecordingUserSessionRepository : IUserSessionRepository
    {
        public UserSessionModel? ReadByUserIdentifierResult { get; init; }
        public UserSessionModel? ReadByIdResult { get; init; }
        public List<UserSessionModel> Created { get; } = [];
        public List<UserSessionModel> Updated { get; } = [];
        public List<UserSessionModel> Deleted { get; } = [];
        public int ReadByUserIdentifierCalls { get; private set; }
        public int ReadIdCalls { get; private set; }

        public Task Create(UserSessionModel item)
        {
            Created.Add(item);
            return Task.CompletedTask;
        }

        public Task<UserSessionModel?> ReadByUserIdentifier(string userIdentifier)
        {
            ReadByUserIdentifierCalls++;
            return Task.FromResult(ReadByUserIdentifierResult);
        }

        public Task<UserSessionModel?> Read(int id)
        {
            ReadIdCalls++;
            return Task.FromResult(ReadByIdResult);
        }

        public Task<UserSessionModel?> Read(Expression<Func<UserSessionModel, bool>> filter)
        {
            return Task.FromResult(default(UserSessionModel));
        }

        public Task<IEnumerable<UserSessionModel>> ReadMany()
        {
            return Task.FromResult<IEnumerable<UserSessionModel>>([]);
        }

        public Task<IEnumerable<UserSessionModel>> ReadMany(Expression<Func<UserSessionModel, bool>> filter)
        {
            return Task.FromResult<IEnumerable<UserSessionModel>>([]);
        }

        public Task Update(UserSessionModel item)
        {
            Updated.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(UserSessionModel item)
        {
            Deleted.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<UserSessionModel> items)
        {
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<UserSessionModel, bool>> filter)
        {
            return Task.CompletedTask;
        }
    }

    sealed class SequenceUserSessionService : IUserSessionService
    {
        readonly Queue<ICoreSession?> _sessions;

        public SequenceUserSessionService(params ICoreSession?[] sessions)
        {
            _sessions = new Queue<ICoreSession?>(sessions);
        }

        public int DeleteCurrentCalls { get; private set; }

        public Task<ICoreSession?> GetCurrent()
        {
            return Task.FromResult(_sessions.Count == 0 ? null : _sessions.Dequeue());
        }

        public Task SetEventId(int? eventId)
        {
            return Task.CompletedTask;
        }

        public Task AppendSnapshot(SnapshotGroup snapshot)
        {
            return Task.CompletedTask;
        }

        public Task DeleteCurrent()
        {
            DeleteCurrentCalls++;
            return Task.CompletedTask;
        }
    }

    sealed class RecordingEnduranceEventRepository : IRepository<EnduranceEvent>
    {
        readonly Dictionary<int, EnduranceEvent> _events;

        public RecordingEnduranceEventRepository(params EnduranceEvent[] events)
        {
            _events = events.ToDictionary(x => x.Id);
        }

        public Task Create(EnduranceEvent item)
        {
            _events[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task<EnduranceEvent?> Read(int id)
        {
            _events.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public Task<EnduranceEvent?> Read(Expression<Func<EnduranceEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(_events.Values.FirstOrDefault(predicate));
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany()
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>(_events.Values.ToArray());
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany(Expression<Func<EnduranceEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<EnduranceEvent>>(_events.Values.Where(predicate).ToArray());
        }

        public Task Update(EnduranceEvent item)
        {
            _events[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task Delete(EnduranceEvent item)
        {
            _events.Remove(item.Id);
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<EnduranceEvent> items)
        {
            foreach (var item in items)
            {
                _events.Remove(item.Id);
            }

            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<EnduranceEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            foreach (var item in _events.Values.Where(predicate).ToArray())
            {
                _events.Remove(item.Id);
            }

            return Task.CompletedTask;
        }
    }

    sealed class RecordingSocketService : INtsSocketService
    {
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public Not.Application.RPC.SignalR.SocketConnectionStatus Status { get; set; }
        public EnduranceEvent? Event { get; set; }
        public List<int> ConnectCalls { get; } = [];
        public int DisconnectCalls { get; private set; }

        public Task Connect(EnduranceEvent enduranceEvent)
        {
            ConnectCalls.Add(enduranceEvent.Id);
            Event = enduranceEvent;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            DisconnectCalls++;
            Event = null;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }
}
