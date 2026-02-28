using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Provider;
using Not.Application.Authentication.User;

namespace Not.Application.Authentication;

public static class AuthenticationExtensions
{
    public static void RegisterNAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IUserResolver, NUserResolver>();
        services.Configure<NAuthenticationSettings>(configuration);

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = NAuthenticationSettings.Scheme;
            })
            .AddCookie()
            .AddMicrosoftEntra(configuration);
    }

    public static AuthenticationBuilder AddMicrosoftEntra(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        var settings = CreateSettings(configuration);
        var callbackPath = string.IsNullOrWhiteSpace(settings.CallbackPath)
            ? "/signin-oidc"
            : settings.CallbackPath;
        var signedOutCallbackPath = string.IsNullOrWhiteSpace(settings.SignedOutCallbackPath)
            ? "/signout-callback-oidc"
            : settings.SignedOutCallbackPath;

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

    static Task ResolveTicketReceived(TicketReceivedContext context)
    {
        var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
        return userResolver.Resolve(context);
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
        RequireConfigValue(settings.ClientSecret, nameof(NAuthenticationSettings.ClientSecret));
        RequireConfigValue(settings.Instance, nameof(NAuthenticationSettings.Instance));
        RequireConfigValue(settings.TenantId, nameof(NAuthenticationSettings.TenantId));
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
