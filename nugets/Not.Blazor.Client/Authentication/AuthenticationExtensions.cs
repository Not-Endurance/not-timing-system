using System.Security.Claims;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Provider;
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

        return services.AddScoped<NUserResolver>().Configure<NAuthenticationSettings>(configuration);
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

        options.AuthenticationPaths.LogOutSucceededPath = AuthenticationContents.AUTHENTICATION;
        options.AuthenticationPaths.LogOutFailedPath = AuthenticationContents.AUTHENTICATION;
    }

    static NAuthenticationSettings CreateSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(nameof(NAuthenticationSettings));
        var settings = new NAuthenticationSettings();
        section.Bind(settings);

        Validate(settings);
        return settings;
    }

    static void Validate(NAuthenticationSettings settings)
    {
        RequireConfigValue(settings.ClientId, nameof(NAuthenticationSettings.ClientId));
        RequireConfigValue(settings.Instance, nameof(NAuthenticationSettings.Instance));
        RequireConfigValue(settings.TenantId, nameof(NAuthenticationSettings.TenantId));
    }

    static string ResolveAuthority(NAuthenticationSettings settings)
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

    static string RequireConfigValue(string? value, string settingPath)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required authentication setting '{settingPath}'.");
    }
}
