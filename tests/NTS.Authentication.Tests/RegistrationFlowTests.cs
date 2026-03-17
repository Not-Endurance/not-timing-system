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
using Not.Structures;
using NTS.Nexus.HTTP.Functions;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Models;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;
using NTS.Witness.Contracts.API;
using NTS.Witness.Features.Sessions;

namespace NTS.Authentication.Tests;

public class RegistrationFlowTests
{
    [Fact]
    public async Task Resolver_registers_new_user_with_name_claim()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success<NUserModel>(null!),
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
            GetResult = Result.Success<NUserModel>(null!),
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

    [Fact]
    public async Task Resolver_uses_existing_user_without_registering_again()
    {
        var existingUser = new NUserModel("existing.user@example.com", ["Admin"]);
        var users = new RecordingUserRegister { GetResult = Result.Success(existingUser) };
        var resolver = new NUserResolver(users, NullLogger<NUserResolver>.Instance);

        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "existing.user@example.com"));

        var result = await resolver.ResolvePrincipal(principal);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, users.RegisterCalls);
        Assert.True(result.Principal.IsInRole("Admin"));
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
    public async Task Witness_user_session_registers_first_time_user_with_claim_name()
    {
        var users = new RecordingUserRegister
        {
            GetResult = Result.Success<NUserModel>(null!),
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
        var sessions = new RecordingRepository<NTS.Application.Watcher.UserSessionModel>();
        var authStateProvider = new StaticAuthenticationStateProvider(
            CreatePrincipal(
                new Claim(ClaimTypes.Email, "new.user@example.com"),
                new Claim("given_name", "Jane"),
                new Claim("family_name", "Doe"),
                new Claim("country", "Bulgaria")
            )
        );
        var service = new WitnessUserSessionService(authStateProvider, users, sessions);

        var current = await service.GetCurrent();

        Assert.Null(current);
        Assert.Equal(
            new NUserRegistration("new.user@example.com", "Jane Doe", "Jane", "Doe", "Bulgaria"),
            users.LastRegistration
        );
        Assert.Equal(1, users.RegisterCalls);
    }

    [Fact]
    public async Task Users_register_function_passes_profile_fields_through_to_repository()
    {
        var users = new RecordingUserRepository
        {
            RegisterResult = new NUserModel("new.user@example.com")
            {
                Name = "Jane Doe",
                GivenName = "Jane",
                Surname = "Doe",
                CountryRegion = "Bulgaria",
            },
        };
        var function = new UserFunctions(new TestFunctionLogger(), users, new TestTelemetryService());
        var request = CreateRequest(
            new RegisterUserPaload("new.user@example.com", "Jane Doe", "Jane", "Doe", "Bulgaria")
        );

        var response = await function.Register(request);

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<NUserModel>(ok.Value);
        Assert.Equal("Jane Doe", payload.Name);
        Assert.Equal("Jane", payload.GivenName);
        Assert.Equal("Doe", payload.Surname);
        Assert.Equal("Bulgaria", payload.CountryRegion);
        Assert.Equal(
            new NUserRegistration("new.user@example.com", "Jane Doe", "Jane", "Doe", "Bulgaria"),
            users.LastRegistration
        );
    }

    [Fact]
    public void User_document_create_maps_profile_fields_to_user()
    {
        var document = NUserDocument.Create("new.user@example.com", "  Jane Doe  ", " Jane ", " Doe ", " Bulgaria ");

        var user = document.ToUser();

        Assert.Equal("Jane Doe", user.Name);
        Assert.Equal("Jane", user.GivenName);
        Assert.Equal("Doe", user.Surname);
        Assert.Equal("Bulgaria", user.CountryRegion);
        Assert.Equal("new.user@example.com", user.Email);
    }

    static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    static HttpRequest CreateRequest(RegisterUserPaload payload)
    {
        var context = new DefaultHttpContext();
        var json = JsonSerializer.Serialize(payload);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/users/register";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
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
                        Surname = registration.Surname,
                        CountryRegion = registration.CountryRegion,
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

        public Task Delete(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }
    }

    sealed class RecordingUserRepository : IUserRepository
    {
        public NUserRegistration? LastRegistration { get; private set; }
        public NUserModel RegisterResult { get; init; } = new("new.user@example.com");

        public Task<NUserModel?> ReadByEmail(string email)
        {
            return Task.FromResult<NUserModel?>(null);
        }

        public Task<NUserModel> Register(NUserRegistration registration)
        {
            LastRegistration = registration;
            return Task.FromResult(RegisterResult);
        }
    }

    sealed class TestFunctionLogger : IFunctionLogger<UserFunctions>
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
}
