using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json.Linq;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
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
        var principal = CreatePrincipal(new Claim("sub", "entra-sub"), new Claim(ClaimTypes.NameIdentifier, "name-id"));

        var result = NUserClaimsHelper.ResolveUserIdentifier(principal);

        Assert.Equal("entra-sub", result);
    }

    [Fact]
    public async Task Witness_user_session_persists_user_identifier_on_new_session()
    {
        var sessions = new RecordingUserSessionRepository();
        var service = CreateService(sessions);

        await service.SetEventId(17);

        var created = Assert.Single(sessions.Created);
        Assert.Equal(7, created.Id);
        Assert.Equal("entra-1", created.UserIdentifier);
        Assert.NotNull(created.State);
        Assert.Equal(17, created.State!.EventId);
        Assert.Empty(created.State.SnapshotHistory);
    }

    [Fact]
    public async Task Witness_user_session_get_current_returns_state_from_user_identifier_lookup()
    {
        var expected = CreateSessionDocument(id: 99, userIdentifier: "entra-1", eventId: 23);
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = expected };
        var service = CreateService(sessions);

        var current = await service.GetCurrent();

        Assert.NotNull(current);
        Assert.Equal(23, current!.EventId);
        Assert.Empty(current.SnapshotHistory);
        Assert.Equal(1, sessions.ReadByUserIdentifierStateCalls);
        Assert.Equal(0, sessions.ReadByUserIdentifierDocumentCalls);
        Assert.Equal(0, sessions.ReadIdCalls);
    }

    [Fact]
    public async Task Witness_user_session_returns_null_when_repository_has_no_user_identifier_match()
    {
        var sessions = new RecordingUserSessionRepository();
        var service = CreateService(sessions);

        var current = await service.GetCurrent();

        Assert.Null(current);
        Assert.Equal(1, sessions.ReadByUserIdentifierStateCalls);
        Assert.Equal(0, sessions.ReadByUserIdentifierDocumentCalls);
        Assert.Equal(0, sessions.ReadIdCalls);
        Assert.Empty(sessions.Updated);
    }

    [Fact]
    public async Task Witness_user_session_deletes_session_resolved_by_user_identifier()
    {
        var expected = CreateSessionDocument(id: 99, userIdentifier: "entra-1", eventId: 23);
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = expected };
        var service = CreateService(sessions);

        await service.DeleteCurrent();

        var deleted = Assert.Single(sessions.Deleted);
        Assert.Same(expected, deleted);
        Assert.Equal(1, sessions.ReadByUserIdentifierStateCalls);
        Assert.Equal(1, sessions.ReadByUserIdentifierDocumentCalls);
        Assert.Equal(0, sessions.ReadIdCalls);
    }

    [Fact]
    public async Task Witness_user_session_does_not_create_session_when_identifier_is_missing()
    {
        var users = new RecordingUserRegister { GetResult = Result.Success(new NUserModel("user@example.com", id: 7)) };
        var sessions = new RecordingUserSessionRepository();
        var service = CreateService(
            sessions,
            principal: CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com")),
            users: users
        );

        await service.SetEventId(17);

        Assert.Empty(sessions.Created);
        Assert.Equal(0, users.GetCalls);
    }

    [Fact]
    public async Task Witness_user_session_keeps_existing_session_for_same_event()
    {
        var existing = CreateSessionDocument(id: 7, userIdentifier: "entra-1", eventId: 23);
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = existing };
        var service = CreateService(sessions);

        await service.SetEventId(23);

        Assert.Empty(sessions.Created);
        Assert.Empty(sessions.Deleted);
        Assert.Empty(sessions.Updated);
    }

    [Fact]
    public async Task Witness_user_session_replaces_existing_session_for_different_event()
    {
        var existing = CreateSessionDocument(id: 7, userIdentifier: "entra-1", eventId: 5);
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = existing };
        var service = CreateService(sessions);

        await service.SetEventId(23);

        var deleted = Assert.Single(sessions.Deleted);
        Assert.Same(existing, deleted);

        var created = Assert.Single(sessions.Created);
        Assert.Equal(7, created.Id);
        Assert.Equal("entra-1", created.UserIdentifier);
        Assert.NotNull(created.State);
        Assert.Equal(23, created.State!.EventId);
        Assert.Empty(created.State.SnapshotHistory);
        Assert.Empty(sessions.Updated);
    }

    [Fact]
    public async Task Witness_user_session_append_snapshot_updates_only_nested_state_history()
    {
        var existing = CreateSessionDocument(
            id: 7,
            userIdentifier: "entra-1",
            eventId: 23,
            snapshotHistory: [SnapshotGroupModel.MapFrom(CreateSnapshotGroup(31, SnapshotType.Arrive))]
        );
        var sessions = new RecordingUserSessionRepository { ReadByUserIdentifierResult = existing };
        var service = CreateService(sessions);

        await service.AppendSnapshot(CreateSnapshotGroup(44, SnapshotType.Present));

        var updated = Assert.Single(sessions.Updated);
        Assert.Same(existing, updated);
        Assert.NotNull(updated.State);
        Assert.Equal(23, updated.State!.EventId);
        Assert.Equal(2, updated.State.SnapshotHistory.Length);
        Assert.Equal(SnapshotType.Present, updated.State.SnapshotHistory[1].Type);
        Assert.Null(JObject.Parse(updated.ToJson())["EventId"]);
        Assert.Null(JObject.Parse(updated.ToJson())["SnapshotHistory"]);
    }

    [Fact]
    public void Witness_user_session_serializes_state_without_root_level_event_fields()
    {
        var session = CreateSessionDocument(
            id: 7,
            userIdentifier: "entra-1",
            eventId: 23,
            snapshotHistory: [SnapshotGroupModel.MapFrom(CreateSnapshotGroup(31, SnapshotType.Arrive))]
        );

        var json = JObject.Parse(session.ToJson());

        Assert.Null(json["EventId"]);
        Assert.Null(json["SnapshotHistory"]);
        Assert.NotNull(json["State"]);
        Assert.Equal(23, json["State"]!["EventId"]!.Value<int>());
        Assert.Single((JArray)json["State"]!["SnapshotHistory"]!);
    }

    static WitnessUserSessionService CreateService(
        RecordingUserSessionRepository sessions,
        ClaimsPrincipal? principal = null,
        RecordingUserRegister? users = null
    )
    {
        principal ??= CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"), new Claim("oid", "entra-1"));
        users ??= new RecordingUserRegister { GetResult = Result.Success(new NUserModel("user@example.com", id: 7)) };

        var serviceProvider = new StaticServiceProvider().Add<INUserSessionRepository<NtsUserSessionStateModel>>(
            sessions
        );
        var nUserSessionService = new NUserSessionService(
            new StaticAuthenticationStateProvider(principal),
            users,
            serviceProvider
        );

        return new WitnessUserSessionService(nUserSessionService, sessions);
    }

    static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    static NtsUserSessionModel CreateSessionDocument(
        int id,
        string userIdentifier,
        int? eventId = null,
        SnapshotGroupModel[]? snapshotHistory = null
    )
    {
        var session = new NtsUserSessionModel { Id = id, UserIdentifier = userIdentifier };
        session.ReplaceState(
            new NtsUserSessionStateModel { EventId = eventId, SnapshotHistory = snapshotHistory ?? [] }
        );
        return session;
    }

    static SnapshotGroup CreateSnapshotGroup(int number, SnapshotType type)
    {
        return new SnapshotGroup(
            [
                new NTS.Domain.Watcher.Snapshot(
                    number,
                    new Person(["Test", "Rider"]),
                    new Timestamp(DateTimeOffset.UtcNow)
                ),
            ],
            type
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

    sealed class StaticServiceProvider : IServiceProvider
    {
        readonly Dictionary<Type, object> _services = [];

        public StaticServiceProvider Add<TService>(TService service)
            where TService : class
        {
            _services[typeof(TService)] = service;
            return this;
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service) ? service : null;
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
            return Task.FromResult(RegisterResult ?? Result.Success(new NUserModel(registration.Email, id: 7)));
        }
    }

    sealed class RecordingUserSessionRepository
        : INtsUserSessionRepository,
            INUserSessionRepository<NtsUserSessionStateModel>
    {
        public NtsUserSessionModel? ReadByUserIdentifierResult { get; init; }
        public NtsUserSessionModel? ReadByIdResult { get; init; }
        public List<NtsUserSessionModel> Created { get; } = [];
        public List<NtsUserSessionModel> Updated { get; } = [];
        public List<NtsUserSessionModel> Deleted { get; } = [];
        public int ReadByUserIdentifierDocumentCalls { get; private set; }
        public int ReadByUserIdentifierStateCalls { get; private set; }
        public int ReadIdCalls { get; private set; }

        public Task Create(NtsUserSessionModel item)
        {
            Created.Add(item);
            return Task.CompletedTask;
        }

        public Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
        {
            ReadByUserIdentifierDocumentCalls++;
            return Task.FromResult(ReadByUserIdentifierResult);
        }

        public Task<NtsUserSessionModel?> Read(int id)
        {
            ReadIdCalls++;
            return Task.FromResult(ReadByIdResult);
        }

        public Task<NtsUserSessionModel?> Read(Expression<Func<NtsUserSessionModel, bool>> filter)
        {
            return Task.FromResult(default(NtsUserSessionModel));
        }

        public Task<IEnumerable<NtsUserSessionModel>> ReadMany()
        {
            return Task.FromResult<IEnumerable<NtsUserSessionModel>>([]);
        }

        public Task<IEnumerable<NtsUserSessionModel>> ReadMany(Expression<Func<NtsUserSessionModel, bool>> filter)
        {
            return Task.FromResult<IEnumerable<NtsUserSessionModel>>([]);
        }

        public Task Update(NtsUserSessionModel item)
        {
            Updated.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(NtsUserSessionModel item)
        {
            Deleted.Add(item);
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<NtsUserSessionModel> items)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<NtsUserSessionModel, bool>> filter)
        {
            return Task.CompletedTask;
        }

        Task<NtsUserSessionStateModel?> INUserSessionRepository<NtsUserSessionStateModel>.ReadByUserIdentifier(
            string userIdentifier
        )
        {
            ReadByUserIdentifierStateCalls++;
            return Task.FromResult(ReadByUserIdentifierResult?.State?.Copy());
        }
    }
}
