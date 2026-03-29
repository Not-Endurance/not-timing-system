using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Provider;
using Not.Application.Configurations;
using Not.Application.Authentication.User;
using Not.Blazor.Client.Authentication.Services;

namespace Not.Blazor.Client.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddNClientAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddMsalAuthentication(options => Configure(options, configuration))
            .AddAccountClaimsPrincipalFactory<ClientSideAccountClaimsPrincipalFactory>();

        return services.AddScoped<NUserResolver>().AddSettings<NClientAuthenticationSettings>(configuration);
    }

    static void Configure(RemoteAuthenticationOptions<MsalProviderOptions> options, IConfiguration configuration)
    {
        var settings = CreateSettings(configuration);
        options.ProviderOptions.Authentication.Authority = ResolveAuthority(settings);
        options.ProviderOptions.Authentication.ClientId = settings.ClientId;
        options.ProviderOptions.Authentication.RedirectUri = RemoteAuthenticationDefaults.LoginCallbackPath;
        options.ProviderOptions.Authentication.PostLogoutRedirectUri = RemoteAuthenticationDefaults.LogoutCallbackPath;

        // User roles are injected from local user resolution, not from incoming provider role claims.
        options.UserOptions.RoleClaim = ClaimTypes.Role;
        AddDefaultAccessTokenScope(options, settings);

        options.AuthenticationPaths.LogInPath = RemoteAuthenticationDefaults.LoginPath;
        options.AuthenticationPaths.LogInCallbackPath = RemoteAuthenticationDefaults.LoginCallbackPath;
        options.AuthenticationPaths.LogInFailedPath = AuthenticationContents.AUTHENTICATION;
        options.AuthenticationPaths.LogOutPath = RemoteAuthenticationDefaults.LogoutPath;
        options.AuthenticationPaths.LogOutCallbackPath = RemoteAuthenticationDefaults.LogoutCallbackPath;
        options.AuthenticationPaths.LogOutSucceededPath = AuthenticationContents.AUTHENTICATION;
        options.AuthenticationPaths.LogOutFailedPath = AuthenticationContents.AUTHENTICATION;
    }

    static NClientAuthenticationSettings CreateSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(nameof(NClientAuthenticationSettings));
        var settings = new NClientAuthenticationSettings();
        section.Bind(settings);

        Validate(settings);
        return settings;
    }

    static void Validate(NClientAuthenticationSettings settings)
    {
        RequireConfigValue(settings.ClientId, nameof(NClientAuthenticationSettings.ClientId));
        RequireConfigValue(settings.Instance, nameof(NClientAuthenticationSettings.Instance));
        RequireConfigValue(settings.TenantId, nameof(NClientAuthenticationSettings.TenantId));
    }

    static string ResolveAuthority(NClientAuthenticationSettings settings)
    {
        var instance = settings.Instance!.TrimEnd('/');
        if (instance.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            instance = instance[..^"/v2.0".Length];
        }

        var tenantId = settings.TenantId!.Trim('/');
        if (instance.EndsWith($"/{tenantId}", StringComparison.OrdinalIgnoreCase))
        {
            return instance;
        }

        return $"{instance}/{tenantId}";
    }

    static void AddDefaultAccessTokenScope(
        RemoteAuthenticationOptions<MsalProviderOptions> options,
        NClientAuthenticationSettings settings
    )
    {
        var scope = NClientAuthenticationSettingsScopeResolver.ResolveScope(settings);
        if (string.IsNullOrWhiteSpace(scope))
        {
            return;
        }

        if (!options.ProviderOptions.DefaultAccessTokenScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            options.ProviderOptions.DefaultAccessTokenScopes.Add(scope);
        }
    }

    static string RequireConfigValue(string? value, string settingPath)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required authentication setting '{settingPath}'.");
    }
}
