using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.Provider;
using Not.Blazor.Client;
using Not.Injection;

namespace NTS.Authentication.Tests;

public class NBlazorClientAuthenticationSessionStorageTests
{
    [Fact]
    public void NClientSideBlazor_registers_the_renamed_session_storage_service()
    {
        var jsRuntime = new RecordingJsRuntime();
        using var provider = CreateProvider(jsRuntime);

        var storage = provider.GetRequiredService<INAuthenticationSessionStorage>();

        Assert.Equal("NBlazorClientAuthenticationSessionStorage", storage.GetType().Name);
    }

    [Fact]
    public async Task Session_started_at_round_trips_through_local_storage()
    {
        var jsRuntime = new RecordingJsRuntime();
        using var provider = CreateProvider(jsRuntime);
        var storage = provider.GetRequiredService<INAuthenticationSessionStorage>();

        var startedAt = new DateTimeOffset(2026, 4, 22, 12, 34, 56, 789, TimeSpan.Zero);

        await storage.WriteSessionStartedAt(startedAt);

        Assert.Equal(
            startedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            jsRuntime.Read("not.auth.session-started-at")
        );
        Assert.Equal(startedAt, await storage.ReadSessionStartedAtAsync());

        await storage.ClearSessionStartedAt();

        Assert.Null(jsRuntime.Read("not.auth.session-started-at"));
        Assert.Null(await storage.ReadSessionStartedAtAsync());
        Assert.All(jsRuntime.Invocations, x => Assert.StartsWith("localStorage.", x, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signin_flow_started_at_round_trips_through_local_storage()
    {
        var jsRuntime = new RecordingJsRuntime();
        using var provider = CreateProvider(jsRuntime);
        var storage = provider.GetRequiredService<INAuthenticationSessionStorage>();

        var before = DateTimeOffset.UtcNow;
        await storage.WriteSigninFlowStartedAt();
        var after = DateTimeOffset.UtcNow;

        var flowStartedAt = await storage.ReadSigninFlowStartedAtAsync();
        Assert.NotNull(flowStartedAt);
        Assert.InRange(flowStartedAt.Value, before, after);

        await storage.ClearSigninFlowStartedAt();

        Assert.Null(jsRuntime.Read("not.auth.signin-flow-started-at"));
        Assert.Null(await storage.ReadSigninFlowStartedAtAsync());
        Assert.All(jsRuntime.Invocations, x => Assert.StartsWith("localStorage.", x, StringComparison.Ordinal));
    }

    static IServiceProvider CreateProvider(RecordingJsRuntime jsRuntime)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(jsRuntime);
        services.AddLogging();
        services.NClientSideBlazor(CreateConfiguration());
        services.AddNConventionalServices(typeof(NBlazorClientServices).Assembly);
        return services.BuildServiceProvider();
    }

    static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [
                        $"{nameof(NClientAuthenticationSettings)}:{nameof(NClientAuthenticationSettings.ClientId)}"
                    ] = "client-id",
                    [
                        $"{nameof(NClientAuthenticationSettings)}:{nameof(NClientAuthenticationSettings.Instance)}"
                    ] = "https://login.microsoftonline.com",
                    [
                        $"{nameof(NClientAuthenticationSettings)}:{nameof(NClientAuthenticationSettings.TenantId)}"
                    ] = "tenant-id",
                }
            )
            .Build();
    }

    sealed class RecordingJsRuntime : IJSRuntime
    {
        readonly Dictionary<string, string?> _localStorage = new(StringComparer.Ordinal);
        readonly List<string> _invocations = [];

        public IReadOnlyList<string> Invocations => _invocations;

        public string? Read(string key)
        {
            return _localStorage.TryGetValue(key, out var value) ? value : null;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        )
        {
            _invocations.Add(identifier);

            return identifier switch
            {
                "localStorage.getItem" => new ValueTask<TValue>(Cast<TValue>(ReadValue(args))),
                "localStorage.setItem" => WriteKey(args),
                "localStorage.removeItem" => RemoveKey(args),
                _ => throw new NotSupportedException(identifier),
            };
        }

        ValueTask<TValue> WriteKey<TValue>(object?[]? args)
        {
            var key = GetKey(args);
            var value = GetValue(args);
            _localStorage[key] = value;
            return new ValueTask<TValue>(default(TValue)!);
        }

        ValueTask<TValue> RemoveKey<TValue>(object?[]? args)
        {
            _localStorage.Remove(GetKey(args));
            return new ValueTask<TValue>(default(TValue)!);
        }

        static string GetKey(object?[]? args)
        {
            if (args is null || args.Length < 1 || args[0] is not string key)
            {
                throw new InvalidOperationException("A storage key was expected.");
            }

            return key;
        }

        static string? GetValue(object?[]? args)
        {
            if (args is null || args.Length < 2)
            {
                throw new InvalidOperationException("A storage value was expected.");
            }

            return args[1]?.ToString();
        }

        string? ReadValue(object?[]? args)
        {
            var key = GetKey(args);
            return _localStorage.TryGetValue(key, out var value) ? value : null;
        }

        static TValue Cast<TValue>(string? value)
        {
            if (value is null)
            {
                return default!;
            }

            return (TValue)(object)value;
        }
    }
}
