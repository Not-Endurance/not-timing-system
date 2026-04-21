using System.Reflection;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Configuration;
using Not.Blazor.Client.Authentication;

namespace NTS.Authentication.Tests;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void Configure_does_not_add_default_api_scope_when_scope_is_blank()
    {
        var options = Configure(CreateConfiguration(scope: ""));

        Assert.Empty(options.ProviderOptions.DefaultAccessTokenScopes);
    }

    [Fact]
    public void Configure_adds_fully_qualified_scope_when_scope_is_configured()
    {
        var options = Configure(CreateConfiguration(resourceClientId: "resource-client", scope: "nts-client-scope"));

        Assert.Equal(
            ["api://resource-client/nts-client-scope"],
            options.ProviderOptions.DefaultAccessTokenScopes.ToArray()
        );
    }

    [Fact]
    public void Configure_prefers_explicit_audience_over_resource_client_id()
    {
        var options = Configure(
            CreateConfiguration(
                resourceClientId: "resource-client",
                audience: "api://preferred-audience",
                scope: "nts-client-scope"
            )
        );

        Assert.Equal(
            ["api://preferred-audience/nts-client-scope"],
            options.ProviderOptions.DefaultAccessTokenScopes.ToArray()
        );
    }

    [Fact]
    public void CreateSettings_binds_session_lifetime_from_configuration()
    {
        var settings = CreateSettings(CreateConfiguration(sessionLifetime: "12:00:00"));

        Assert.Equal(TimeSpan.FromHours(12), settings.SessionLifetime);
    }

    static RemoteAuthenticationOptions<MsalProviderOptions> Configure(IConfiguration configuration)
    {
        var options = new RemoteAuthenticationOptions<MsalProviderOptions>();
        var configure = typeof(AuthenticationExtensions).GetMethod(
            "Configure",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        Assert.NotNull(configure);
        configure!.Invoke(null, [options, configuration]);
        return options;
    }

    static Not.Application.Authentication.Provider.NClientAuthenticationSettings CreateSettings(IConfiguration configuration)
    {
        var createSettings = typeof(AuthenticationExtensions).GetMethod(
            "CreateSettings",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        Assert.NotNull(createSettings);
        return (Not.Application.Authentication.Provider.NClientAuthenticationSettings)createSettings!.Invoke(
            null,
            [configuration]
        )!;
    }

    static IConfiguration CreateConfiguration(
        string? resourceClientId = null,
        string? audience = null,
        string? scope = null,
        string? sessionLifetime = null
    )
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.ClientId)}"
                    ] = "client-id",
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.Instance)}"
                    ] = "https://login.microsoftonline.com",
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.TenantId)}"
                    ] = "tenant-id",
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.ResourceClientId)}"
                    ] = resourceClientId,
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.Audience)}"
                    ] = audience,
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.Scope)}"
                    ] = scope,
                    [
                        $"{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings)}:{nameof(Not.Application.Authentication.Provider.NClientAuthenticationSettings.SessionLifetime)}"
                    ] = sessionLifetime,
                }
            )
            .Build();
    }
}
