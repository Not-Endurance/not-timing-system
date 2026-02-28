using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Provider;
using Not.Application.Authentication.User;

namespace Not.Application.Authentication;

public static class AuthenticationExtensions
{
    public static void RegisterNAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NUserResolver>();
        services.AddScoped<IUserResolver>(provider => provider.GetRequiredService<NUserResolver>());
        services.Configure<NAuthenticationSettings>(configuration);

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = NAuthenticationSettings.Scheme;
            })
            .AddCookie()
            .AddMicrosoftEntra(configuration);
    }

    public static void RegisterNAuthenticationWasm(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NUserResolver>();
        services.AddScoped<IUserResolver>(provider => provider.GetRequiredService<NUserResolver>());
        services.Configure<NAuthenticationSettings>(configuration);

        services
            .AddMsalAuthentication(options => ConfigureWasmAuthenticationOptions(options, configuration))
            .AddAccountClaimsPrincipalFactory<NWasmAccountClaimsPrincipalFactory>();
    }

    public static AuthenticationBuilder AddMicrosoftEntra(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        var settings = CreateSettings(configuration, requireClientSecret: true);
        var callbackPath = ResolveCallbackPath(settings);
        var signedOutCallbackPath = ResolveSignedOutCallbackPath(settings);

        authBuilder.AddOpenIdConnect(
            NAuthenticationSettings.Scheme,
            options =>
            {
                options.Authority = ResolveAuthority(settings);
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.CallbackPath = callbackPath;
                options.SignedOutCallbackPath = signedOutCallbackPath;
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = ResolveTicketReceived,
                };
            }
        );

        return authBuilder;
    }

    static void ConfigureWasmAuthenticationOptions(
        RemoteAuthenticationOptions<MsalProviderOptions> options,
        IConfiguration configuration
    )
    {
        var settings = CreateSettings(configuration, requireClientSecret: false);
        options.ProviderOptions.Authentication.Authority = ResolveAuthority(settings);
        options.ProviderOptions.Authentication.ClientId = settings.ClientId;

        // User roles are injected from local user resolution, not from incoming provider role claims.
        options.UserOptions.RoleClaim = ClaimTypes.Role;

        options.AuthenticationPaths.LogInPath = "/signin";
        options.AuthenticationPaths.LogOutPath = "/signout";
        options.AuthenticationPaths.LogInCallbackPath = ResolveCallbackPath(settings);
        options.AuthenticationPaths.LogOutCallbackPath = ResolveSignedOutCallbackPath(settings);
        options.AuthenticationPaths.LogInFailedPath = "/access-denied";
        options.AuthenticationPaths.LogOutFailedPath = "/access-denied";
        options.AuthenticationPaths.LogOutSucceededPath = "/profile";
    }

    static Task ResolveTicketReceived(TicketReceivedContext context)
    {
        var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
        return userResolver.Resolve(context);
    }

    static NAuthenticationSettings CreateSettings(IConfiguration configuration, bool requireClientSecret)
    {
        var section = configuration.GetSection(nameof(NAuthenticationSettings));
        var settings = new NAuthenticationSettings();
        section.Bind(settings);

        Validate(settings, requireClientSecret);
        return settings;
    }

    static void Validate(NAuthenticationSettings settings, bool requireClientSecret)
    {
        RequireConfigValue(settings.ClientId, nameof(NAuthenticationSettings.ClientId));
        if (requireClientSecret)
        {
            RequireConfigValue(settings.ClientSecret, nameof(NAuthenticationSettings.ClientSecret));
        }
        RequireConfigValue(settings.Instance, nameof(NAuthenticationSettings.Instance));
        RequireConfigValue(settings.TenantId, nameof(NAuthenticationSettings.TenantId));
    }

    static string ResolveCallbackPath(NAuthenticationSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.CallbackPath) ? "/signin-oidc" : settings.CallbackPath;
    }

    static string ResolveSignedOutCallbackPath(NAuthenticationSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.SignedOutCallbackPath)
            ? "/signout-callback-oidc"
            : settings.SignedOutCallbackPath;
    }

    static string ResolveAuthority(NAuthenticationSettings settings)
    {
        var instance = settings.Instance!.TrimEnd('/');
        var tenantId = settings.TenantId!.Trim('/');
        return $"{instance}/{tenantId}/v2.0";
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
