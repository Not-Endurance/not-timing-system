using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// NoTiming.Api hosted in this process (ADR-0011), on a MongoDB the test supplies. By default the tests reach it in
/// memory. In Kestrel mode it listens on a real loopback port, for clients that need one: the SignalR client of the
/// Ui, WebSockets and browsers.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The request header with which a test says what address a request comes from. The in-memory host has no
    /// connection to read it from: this stands in for the address a proxy would forward.
    /// </summary>
    public const string CLIENT_ADDRESS_HEADER = "X-Test-Client";
    public const string USER_SECRETS_FILE = "secrets.json";

    public static string NewDataProtectionKeys()
    {
        return Path.Combine(AppContext.BaseDirectory, "data-protection", Guid.NewGuid().ToString("N"));
    }

    readonly string _mongoConnectionString;
    readonly string _environment;
    readonly string? _emailSender;
    readonly string _dataProtectionKeys;
    readonly TimeProvider? _time;
    readonly bool _productionRateLimits;
    readonly bool _finaliseRankings;
    readonly Action<IServiceCollection>? _configureServices;
    readonly Action<IWebHostBuilder>? _configureHost;

    /// <param name="mongoConnectionString">The database of the users, their sessions and their codes.</param>
    /// <param name="kestrel">Listen on a real loopback port instead of being reached in memory.</param>
    /// <param name="environment">The hosting environment: Production behaves as it does in production.</param>
    /// <param name="emailSender">
    /// <c>Outbox</c> by default, so a test reads the codes it asks for. Production refuses the outbox, and a host in
    /// Production has no sender at all unless a test adds one.
    /// </param>
    /// <param name="dataProtectionKeys">
    /// The folder of the key ring. A host that must read what another protected shares it. By default each host has
    /// its own, under the test output, never in the user profile.
    /// </param>
    /// <param name="time">A clock the test moves, for the lifetimes of codes and sessions and the windows of the limits.</param>
    /// <param name="productionRateLimits">
    /// The limits of authentication as they are in production, which the tests of the limits run with. By default they
    /// are lifted far above anything a test does, so that a test that asks for many codes is not stopped by them.
    /// </param>
    /// <param name="finaliseRankings">
    /// The host is given no word on the finalisation of Rankings, as in production, where the sweep runs at start and every
    /// hour. By default it is told not to: a test that seeds Events must not find them finalised by a host it did not ask to.
    /// </param>
    public ApiFactory(
        string mongoConnectionString,
        bool kestrel = false,
        string environment = "Development",
        string? emailSender = null,
        string? dataProtectionKeys = null,
        TimeProvider? time = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IWebHostBuilder>? configureHost = null,
        bool productionRateLimits = false,
        bool finaliseRankings = false
    )
    {
        // Azure supplies PORT to the deployed host. A developer's own PORT must not bind a second listener here.
        Environment.SetEnvironmentVariable("PORT", null);

        _mongoConnectionString = mongoConnectionString;
        _environment = environment;
        _emailSender = emailSender ?? (environment == "Production" ? null : "Outbox");
        _dataProtectionKeys = dataProtectionKeys ?? NewDataProtectionKeys();
        _time = time;
        _productionRateLimits = productionRateLimits;
        _finaliseRankings = finaliseRankings;
        _configureServices = configureServices;
        _configureHost = configureHost;
        if (kestrel)
        {
            // Port 0 is a free port of its own. Without a port every factory binds 5000 and only one can run.
            UseKestrel(0);
        }
    }

    /// <summary>The real address in Kestrel mode.</summary>
    public Uri BaseAddress
    {
        get
        {
            using var client = CreateClient();
            return client.BaseAddress!;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                // The user-secrets of a developer hold what they run the platform with on their machine (the local sign in
                // as and a connection string, #607). Development reads them, so a test host that is in Development would
                // read them as well and depend on whose machine it runs on. The source names the file, not its path.
                foreach (
                    var secrets in configuration
                        .Sources.OfType<JsonConfigurationSource>()
                        .Where(x => x.Path == USER_SECRETS_FILE)
                        .ToList()
                )
                {
                    configuration.Sources.Remove(secrets);
                }
            }
        );
        builder.UseSetting("MONGO_CONNECTION_STRING", _mongoConnectionString);
        if (_emailSender != null)
        {
            builder.UseSetting("Email:Sender", _emailSender);
        }

        if (!_finaliseRankings)
        {
            builder.UseSetting("RankingFinalisation:Enabled", "false");
        }

        if (!_productionRateLimits)
        {
            foreach (var limit in RateLimitSettings())
            {
                builder.UseSetting($"Auth:RateLimits:{limit}", "1000000");
            }

            foreach (var limit in new[] { "PerAccount", "Overall" })
            {
                builder.UseSetting($"Search:RateLimits:{limit}", "1000000");
            }
        }

        _configureHost?.Invoke(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_dataProtectionKeys));
            services.AddSingleton<IStartupFilter, ClientAddressFromHeader>();
            if (_time != null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(_time);
            }

            _configureServices?.Invoke(services);
        });
    }

    static string[] RateLimitSettings()
    {
        return
        [
            "SendsPerAddress",
            "SendsPerClient",
            "SendsOverall",
            "FailedVerificationsPerAddress",
            "FailedVerificationsPerClient",
            "FailedVerificationsOverall",
        ];
    }

    /// <summary>Gives the request the address a test named, as a forwarding proxy would have.</summary>
    sealed class ClientAddressFromHeader : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(
                    (context, following) =>
                    {
                        if (
                            context.Request.Headers.TryGetValue(CLIENT_ADDRESS_HEADER, out var value)
                            && IPAddress.TryParse(value, out var address)
                        )
                        {
                            context.Connection.RemoteIpAddress = address;
                        }

                        return following(context);
                    }
                );
                next(app);
            };
        }
    }
}
