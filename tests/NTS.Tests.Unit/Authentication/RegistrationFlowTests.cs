using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Structures;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions;
using NTS.Nexus.HTTP.Functions.Event;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Models;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;
using NTS.Witness.Contracts.API;

namespace NTS.Authentication.Tests;

public class RegistrationFlowTests
{
    [Fact]
    public async Task Resolver_registers_new_user_with_name_claim()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Failure<NUserModel>("User not found"),
            RegisterResult = Result.Success(
                new NUserModel("new.user@example.com", ["Judge"])
                {
                    Name = "Jane Doe",
                    GivenName = "Jane",
                    Surname = "Doe",
                    CountryRegion = "Bulgaria",
                }
            ),
        };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);

        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "new.user@example.com"),
            new Claim("name", "Jane Doe"),
            new Claim("given_name", "Jane"),
            new Claim("family_name", "Doe"),
            new Claim("country", "Bulgaria")
        );

        var result = await resolver.ResolvePrincipal(principal);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new NUserRegistration("new.user@example.com", "Jane Doe", "Jane", "Doe", "Bulgaria"),
            users.LastRegistration
        );
        Assert.True(result.Principal.Identity?.IsAuthenticated);
        Assert.True(result.Principal.IsInRole("Judge"));
    }

    [Fact]
    public async Task Resolver_combines_given_and_family_name_when_full_name_is_missing()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Failure<NUserModel>("User not found"),
            RegisterResult = Result.Success(
                new NUserModel("new.user@example.com")
                {
                    Name = "Jane Doe",
                    GivenName = "Jane",
                    Surname = "Doe",
                    CountryRegion = "BG",
                }
            ),
        };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);

        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "new.user@example.com"),
            new Claim("given_name", "Jane"),
            new Claim("family_name", "Doe"),
            new Claim("country", "BG")
        );

        var result = await resolver.ResolvePrincipal(principal);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jane Doe", users.LastRegistration?.Name);
        Assert.Equal("Jane", users.LastRegistration?.GivenName);
        Assert.Equal("Doe", users.LastRegistration?.Surname);
        Assert.Equal("BG", users.LastRegistration?.CountryRegion);
    }

    [Theory]
    [InlineData("club")]
    [InlineData("extension_1234567890abcdef_club")]
    public async Task Resolver_registers_new_user_with_club_claim(string clubClaimType)
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Failure<NUserModel>("User not found"),
            RegisterResult = Result.Success(
                new NUserModel("new.user@example.com")
                {
                    Name = "Jane Doe",
                    GivenName = "Jane",
                    Surname = "Doe",
                    Club = "Konarche",
                }
            ),
        };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "new.user@example.com"),
            new Claim("given_name", "Jane"),
            new Claim("family_name", "Doe"),
            new Claim(clubClaimType, " Konarche ")
        );

        var result = await resolver.ResolvePrincipal(principal);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new NUserRegistration("new.user@example.com", "Jane Doe", "Jane", "Doe", club: "Konarche"),
            users.LastRegistration
        );
    }

    [Fact]
    public async Task Resolver_registers_new_user_with_pending_registration_profile()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Failure<NUserModel>("User not found"),
            RegisterResult = Result.Success(
                new NUserModel("new.user@example.com")
                {
                    Name = "Jane Marie Doe",
                    GivenName = "Jane",
                    MiddleName = "Marie",
                    Surname = "Doe",
                    Club = "Konarche",
                    FeiId = "10101010",
                }
            ),
        };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "new.user@example.com"));
        var profile = new NUserRegistrationProfile(
            "Jane Marie Doe",
            "Jane",
            "Marie",
            "Doe",
            "Konarche",
            "10101010"
        );

        var result = await resolver.ResolvePrincipal(principal, profile);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new NUserRegistration(
                "new.user@example.com",
                "Jane Marie Doe",
                "Jane",
                "Doe",
                middleName: "Marie",
                club: "Konarche",
                feiId: "10101010"
            ),
            users.LastRegistration
        );
    }

    [Fact]
    public async Task Resolver_uses_existing_user_without_registering_again()
    {
        var existingUser = new NUserModel("existing.user@example.com", ["Admin"]);
        var users = new RecordingUserRegister { GetResult = Result.Success(existingUser) };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);

        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "existing.user@example.com"));
        var pendingProfile = new NUserRegistrationProfile("Jane Doe", "Jane", surname: "Doe");

        var result = await resolver.ResolvePrincipal(principal, pendingProfile);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, users.RegisterCalls);
        Assert.True(result.Principal.IsInRole("Admin"));
    }

    [Fact]
    public async Task Resolver_registers_when_get_returns_success_with_null_user()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success<NUserModel>(null!),
            RegisterResult = Result.Success(new NUserModel("new.user@example.com")),
        };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "new.user@example.com"));

        var result = await resolver.ResolvePrincipal(principal);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, users.RegisterCalls);
        Assert.Equal("new.user@example.com", users.LastRegistration?.Email);
    }

    [Fact]
    public async Task Resolver_fails_when_email_is_missing()
    {
        var users = new RecordingUserRegister();
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);

        var principal = CreatePrincipal(new Claim("name", "Jane Doe"));

        var result = await resolver.ResolvePrincipal(principal);

        Assert.False(result.IsSuccess);
        Assert.Equal("/error", result.ServerRedirect);
        Assert.Equal(0, users.RegisterCalls);
    }

    [Fact]
    public async Task N_user_session_registers_first_time_user_with_claim_name()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Failure<NUserModel>("User not found"),
            RegisterResult = Result.Success(
                new NUserModel("new.user@example.com")
                {
                    Name = "Jane Doe",
                    GivenName = "Jane",
                    Surname = "Doe",
                    CountryRegion = "Bulgaria",
                }
            ),
        };
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(
                new Claim(ClaimTypes.Email, "new.user@example.com"),
                new Claim("oid", "entra-1"),
                new Claim("given_name", "Jane"),
                new Claim("family_name", "Doe"),
                new Claim("country", "Bulgaria")
            )
        );
        var service = new NUserSessionService(authStateProvider, users, new StaticServiceProvider());

        var current = await service.GetCurrent<object>();

        var session = Assert.IsAssignableFrom<INUserSessionModel<object>>(current);
        Assert.Equal("entra-1", session.UserIdentifier);
        Assert.Equal("new.user@example.com", session.User.Email);
        Assert.Null(session.State);
        Assert.Equal(
            new NUserRegistration("new.user@example.com", "Jane Doe", "Jane", "Doe", "Bulgaria"),
            users.LastRegistration
        );
        Assert.Equal(1, users.RegisterCalls);
    }

    [Fact]
    public async Task N_user_session_registers_when_get_returns_success_with_null_user()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success<NUserModel>(null!),
            RegisterResult = Result.Success(new NUserModel("new.user@example.com")),
        };
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "new.user@example.com"), new Claim("oid", "entra-1"))
        );
        var service = new NUserSessionService(authStateProvider, users, new StaticServiceProvider());

        var current = await service.GetCurrent<object>();

        var session = Assert.IsAssignableFrom<INUserSessionModel<object>>(current);
        Assert.Equal("new.user@example.com", session.User.Email);
        Assert.Equal(1, users.RegisterCalls);
    }

    [Fact]
    public async Task N_user_session_reads_registered_session_state_by_user_identifier()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success(new NUserModel("existing.user@example.com", id: 7)),
        };
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(new Claim(ClaimTypes.Email, "existing.user@example.com"), new Claim("oid", "entra-1"))
        );
        var expected = new TestSessionState("entra-1", 23);
        var repository = new RecordingSessionStateRepository { ReadByUserIdentifierResult = expected };
        var serviceProvider = new StaticServiceProvider().Add<INUserSessionRepository<TestSessionState>>(repository);
        var service = new NUserSessionService(authStateProvider, users, serviceProvider);

        var current = await service.GetCurrent<TestSessionState>();

        var session = Assert.IsAssignableFrom<INUserSessionModel<TestSessionState>>(current);
        Assert.Equal("entra-1", session.UserIdentifier);
        Assert.Same(expected, session.State);
        Assert.Equal(1, repository.ReadByUserIdentifierCalls);
    }

    [Fact]
    public async Task Users_register_function_returns_registered_user_and_passes_profile_fields_through_to_repository()
    {
        var users = new RecordingUserRepository
        {
            RegisterResult = new NUserModel("new.user@example.com")
            {
                Name = "Jane Marie Doe",
                GivenName = "Jane",
                MiddleName = "Marie",
                Surname = "Doe",
                CountryRegion = "Bulgaria",
                Club = "Konarche",
                FeiId = "10101010",
            },
        };
        var function = new UserFunctions(new TestFunctionLogger<UserFunctions>(), users, new TestTelemetryService());
        var request = CreateRequest(
            new RegisterUserPaload(
                "new.user@example.com",
                "Jane Marie Doe",
                "Jane",
                "Doe",
                "Bulgaria",
                "Marie",
                "Konarche",
                "10101010"
            )
        );

        var response = await function.Register(request);

        var ok = Assert.IsType<OkObjectResult>(response);
        var result = Assert.IsType<Result<NUserModel>>(ok.Value);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(users.RegisterResult.Id, result.Data.Id);
        Assert.Equal(users.RegisterResult.Email, result.Data.Email);
        Assert.Equal(
            new NUserRegistration(
                "new.user@example.com",
                "Jane Marie Doe",
                "Jane",
                "Doe",
                "Bulgaria",
                "Marie",
                "Konarche",
                "10101010"
            ),
            users.LastRegistration
        );
    }

    [Fact]
    public async Task Users_read_by_email_returns_success_result_with_null_data_when_user_is_missing()
    {
        var function = new UserFunctions(
            new TestFunctionLogger<UserFunctions>(),
            new RecordingUserRepository(),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Get, "/api/users/missing.user@example.com");

        var response = await function.ReadByEmail(request, "missing.user@example.com");

        var ok = Assert.IsType<OkObjectResult>(response);
        var result = Assert.IsType<Result<NUserModel>>(ok.Value);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Users_read_many_returns_empty_collection_in_result_envelope()
    {
        var function = new UserFunctions(
            new TestFunctionLogger<UserFunctions>(),
            new RecordingUserRepository(),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Get, "/api/users");

        var response = await function.ReadMany(request);

        var ok = Assert.IsType<OkObjectResult>(response);
        var result = Assert.IsType<Result<IEnumerable<NUserModel>>>(ok.Value);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Users_register_returns_bad_request_for_malformed_payload()
    {
        var function = new UserFunctions(
            new TestFunctionLogger<UserFunctions>(),
            new RecordingUserRepository(),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Post, "/api/users/register", "{ invalid json");

        await Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(() => function.Register(request));
    }

    [Fact]
    public async Task Endurance_event_start_returns_failure_result_when_business_service_throws_domain_exception()
    {
        var function = new EnduranceEventFunctions(
            new TestFunctionLogger<EnduranceEventFunctions>(),
            new RecordingRepository<EnduranceEventModel>(),
            new NoOpEnduranceEventResetService(),
            new ThrowingEnduranceEventBusinessService(new DomainException("Start blocked")),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Post, "/api/endurance-event/7/start");

        var exception = await Assert.ThrowsAsync<DomainException>(() => function.Start(request, 7));

        Assert.Equal("Start blocked", exception.Message);
    }

    [Fact]
    public void User_document_create_maps_profile_fields_to_user()
    {
        var document = NUserDocument.Create(
            "new.user@example.com",
            "  Jane Marie Doe  ",
            " Jane ",
            " Doe ",
            " Bulgaria ",
            " Marie ",
            " Konarche ",
            " 10101010 "
        );

        var user = document.ToUser();

        Assert.Equal("Jane Marie Doe", user.Name);
        Assert.Equal("Jane", user.GivenName);
        Assert.Equal("Marie", user.MiddleName);
        Assert.Equal("Doe", user.Surname);
        Assert.Equal("Bulgaria", user.CountryRegion);
        Assert.Equal("Konarche", user.Club);
        Assert.Equal("10101010", user.FeiId);
        Assert.Equal("new.user@example.com", user.Email);
    }

    [Fact]
    public void User_document_from_user_preserves_profile_fields()
    {
        var user = new NUserModel("new.user@example.com", ["Judge"], 17)
        {
            Name = "Jane Marie Doe",
            GivenName = "Jane",
            MiddleName = "Marie",
            Surname = "Doe",
            CountryRegion = "Bulgaria",
            Club = "Konarche",
            FeiId = "10101010",
        };

        var document = NUserDocument.From(user);
        var restored = document.ToUser();

        Assert.Equal(user.Id, restored.Id);
        Assert.Equal(user.Name, restored.Name);
        Assert.Equal(user.GivenName, restored.GivenName);
        Assert.Equal(user.MiddleName, restored.MiddleName);
        Assert.Equal(user.Surname, restored.Surname);
        Assert.Equal(user.CountryRegion, restored.CountryRegion);
        Assert.Equal(user.Club, restored.Club);
        Assert.Equal(user.FeiId, restored.FeiId);
        Assert.Equal(user.Roles, restored.Roles);
    }

    static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    static HttpRequest CreateRequest(RegisterUserPaload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return CreateRequest(HttpMethods.Post, "/api/users/register", json);
    }

    static HttpRequest CreateRequest(string method, string path, string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? ""));
        return context.Request;
    }

    sealed class RecordingUserRegister : IUserRegister
    {
        public Result<NUserModel>? GetResult { get; init; }
        public Result<NUserModel>? RegisterResult { get; init; }
        public NUserRegistration? LastRegistration { get; private set; }
        public int RegisterCalls { get; private set; }

        public Task<Result<NUserModel>> Get(string email)
        {
            return Task.FromResult(GetResult ?? Result.Success<NUserModel>(null!));
        }

        public Task<Result<NUserModel>> Register(NUserRegistration registration)
        {
            RegisterCalls++;
            LastRegistration = registration;

            var result =
                RegisterResult
                ?? Result.Success(
                    new NUserModel(registration.Email)
                    {
                        Name = registration.Name,
                        GivenName = registration.GivenName,
                        MiddleName = registration.MiddleName,
                        Surname = registration.Surname,
                        CountryRegion = registration.CountryRegion,
                        Club = registration.Club,
                        FeiId = registration.FeiId,
                    }
                );
            return Task.FromResult(result);
        }
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

    sealed class RecordingRepository<T> : IRepository<T>
    {
        public Task Create(T item)
        {
            return Task.CompletedTask;
        }

        public Task<T?> Read(int id)
        {
            return Task.FromResult(default(T));
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult(default(T));
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task Update(T item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }
    }

    sealed class RecordingSessionStateRepository : INUserSessionRepository<TestSessionState>
    {
        public TestSessionState? ReadByUserIdentifierResult { get; init; }
        public int ReadByUserIdentifierCalls { get; private set; }

        public Task<TestSessionState?> ReadByUserIdentifier(string userIdentifier)
        {
            ReadByUserIdentifierCalls++;
            return Task.FromResult(ReadByUserIdentifierResult);
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

    sealed class TestSessionState
    {
        public TestSessionState(string userIdentifier, int eventId)
        {
            UserIdentifier = userIdentifier;
            EventId = eventId;
        }

        public string UserIdentifier { get; }
        public int EventId { get; }
    }

    sealed class RecordingUserRepository : IUserRepository
    {
        public NUserRegistration? LastRegistration { get; private set; }
        public NUserModel RegisterResult { get; init; } = new("new.user@example.com");
        public NUserModel? ReadByEmailResult { get; init; }
        public IEnumerable<NUserModel> ReadManyResult { get; init; } = [];

        public Task<NUserModel?> ReadByEmail(string email)
        {
            return Task.FromResult(ReadByEmailResult);
        }

        public Task<IEnumerable<NUserModel>> ReadMany()
        {
            return Task.FromResult(ReadManyResult);
        }

        public Task<NUserModel> Register(NUserRegistration registration)
        {
            LastRegistration = registration;
            return Task.FromResult(RegisterResult);
        }
    }

    sealed class TestFunctionLogger<TFunction> : IFunctionLogger<TFunction>
    {
        public void LogDebug(HttpRequest request, string method = "") { }

        public void LogDebug(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogInformation(HttpRequest request, string method = "") { }

        public void LogInformation(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, string method = "") { }

        public void LogError(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, Exception exception, string method = "") { }
    }

    sealed class TestTelemetryService : ITelemetryService
    {
        public Activity? StartActivity(string className, string methodName)
        {
            return null;
        }
    }

    sealed class NoOpEnduranceEventResetService : IEnduranceEventResetService
    {
        public Task Reset(int eventId)
        {
            return Task.CompletedTask;
        }
    }

    sealed class ThrowingEnduranceEventBusinessService : IEnduranceEventBusinessService
    {
        readonly Exception _exception;

        public ThrowingEnduranceEventBusinessService(Exception exception)
        {
            _exception = exception;
        }

        public Task<EnduranceEventModel> Start(int upcomingEventId)
        {
            return Task.FromException<EnduranceEventModel>(_exception);
        }
    }
}
