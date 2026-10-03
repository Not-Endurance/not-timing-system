using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// NoTiming.Api hosted in this process (ADR-0011). By default the tests reach it in memory. In Kestrel mode it listens
/// on a real loopback port, for clients that need one: the SignalR client of the Ui, WebSockets and browsers.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    readonly string _environment;
    readonly Action<IServiceCollection>? _configureServices;
    readonly Action<IWebHostBuilder>? _configureHost;

    public ApiFactory(
        bool kestrel = false,
        string environment = "Development",
        Action<IServiceCollection>? configureServices = null,
        Action<IWebHostBuilder>? configureHost = null
    )
    {
        // Azure supplies PORT to the deployed host. A developer's own PORT must not bind a second listener here.
        Environment.SetEnvironmentVariable("PORT", null);

        _environment = environment;
        _configureServices = configureServices;
        _configureHost = configureHost;
        if (kestrel)
        {
            UseKestrel();
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
        _configureHost?.Invoke(builder);
        if (_configureServices != null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
    }
}
