using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    public static string NewDataProtectionKeys()
    {
        return Path.Combine(AppContext.BaseDirectory, "data-protection", Guid.NewGuid().ToString("N"));
    }

    readonly string _mongoConnectionString;
    readonly string _environment;
    readonly string? _emailSender;
    readonly string _dataProtectionKeys;
    readonly TimeProvider? _time;
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
    /// <param name="time">A clock the test moves, for the lifetimes of codes and sessions.</param>
    public ApiFactory(
        string mongoConnectionString,
        bool kestrel = false,
        string environment = "Development",
        string? emailSender = null,
        string? dataProtectionKeys = null,
        TimeProvider? time = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IWebHostBuilder>? configureHost = null
    )
    {
        // Azure supplies PORT to the deployed host. A developer's own PORT must not bind a second listener here.
        Environment.SetEnvironmentVariable("PORT", null);

        _mongoConnectionString = mongoConnectionString;
        _environment = environment;
        _emailSender = emailSender ?? (environment == "Production" ? null : "Outbox");
        _dataProtectionKeys = dataProtectionKeys ?? NewDataProtectionKeys();
        _time = time;
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
        builder.UseSetting("MONGO_CONNECTION_STRING", _mongoConnectionString);
        if (_emailSender != null)
        {
            builder.UseSetting("Email:Sender", _emailSender);
        }

        _configureHost?.Invoke(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_dataProtectionKeys));
            if (_time != null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(_time);
            }

            _configureServices?.Invoke(services);
        });
    }
}
