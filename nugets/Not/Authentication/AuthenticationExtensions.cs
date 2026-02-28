using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Authentication.Provider;
using Not.Authentication.User;

namespace Not.Authentication;

public static class AuthenticationExtensions
{
    public static void RegisterNAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthenticationBuilder, IConfiguration>? providerConfiguration = null
    )
    {
        services.AddScoped<IAuthenticationSettings, NAuthenticationSettings>();
        services.AddSingleton<IUserResolver, NUserResolver>();
        var authSection = configuration.GetSection(NAuthenticationProviderSettings.SECTION_NAME);
        services.Configure<NAuthenticationProviderSettings>(authSection);

        var providerSettings = ResolveProviderSettings(configuration);
        var providerScheme = ResolveScheme(providerSettings.MicrosoftEntra.Scheme);
        var defaultChallengeScheme = string.IsNullOrWhiteSpace(providerSettings.DefaultChallengeScheme)
            ? providerScheme
            : providerSettings.DefaultChallengeScheme;

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = defaultChallengeScheme;
            })
            .AddCookie();

        if (providerConfiguration == null)
        {
            authBuilder.AddMicrosoftEntraAuth(providerSettings.MicrosoftEntra);
            return;
        }

        providerConfiguration(authBuilder, configuration);
    }

    public static AuthenticationBuilder AddMicrosoftEntraAuth(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        var providerSettings = ResolveProviderSettings(configuration);
        return authBuilder.AddMicrosoftEntraAuth(providerSettings.MicrosoftEntra);
    }

    public static AuthenticationBuilder AddMicrosoftEntraAuth(
        this AuthenticationBuilder authBuilder,
        MicrosoftEntraAuthenticationProviderSettings microsoftEntraSettings
    )
    {
        ValidateMicrosoftEntraSettings(microsoftEntraSettings);
        var scheme = ResolveScheme(microsoftEntraSettings.Scheme);
        var callbackPath = string.IsNullOrWhiteSpace(microsoftEntraSettings.CallbackPath)
            ? "/signin-oidc"
            : microsoftEntraSettings.CallbackPath;
        var signedOutCallbackPath = string.IsNullOrWhiteSpace(microsoftEntraSettings.SignedOutCallbackPath)
            ? "/signout-callback-oidc"
            : microsoftEntraSettings.SignedOutCallbackPath;

        authBuilder.AddOpenIdConnect(
            scheme,
            options =>
            {
                options.Authority = ResolveAuthority(microsoftEntraSettings);
                options.ClientId = RequireConfigValue(
                    microsoftEntraSettings.ClientId,
                    $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:ClientId"
                );
                options.ClientSecret = RequireConfigValue(
                    microsoftEntraSettings.ClientSecret,
                    $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:ClientSecret"
                );
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

    public static Task ResolveTicketReceived(TicketReceivedContext context)
    {
        var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
        return userResolver.Resolve(context);
    }

    static NAuthenticationProviderSettings ResolveProviderSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(NAuthenticationProviderSettings.SECTION_NAME);
        var settings = new NAuthenticationProviderSettings();
        section.Bind(settings);

        ValidateMicrosoftEntraSettings(settings.MicrosoftEntra);
        return settings;
    }

    static void ValidateMicrosoftEntraSettings(MicrosoftEntraAuthenticationProviderSettings? microsoftEntraSettings)
    {
        if (microsoftEntraSettings == null)
        {
            throw new InvalidOperationException(
                $"Missing required authentication section '{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}'."
            );
        }

        RequireConfigValue(
            microsoftEntraSettings.ClientId,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:ClientId"
        );
        RequireConfigValue(
            microsoftEntraSettings.ClientSecret,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:ClientSecret"
        );

        if (!string.IsNullOrWhiteSpace(microsoftEntraSettings.Authority))
        {
            return;
        }

        RequireConfigValue(
            microsoftEntraSettings.Instance,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:Instance"
        );
        RequireConfigValue(
            microsoftEntraSettings.TenantId,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:TenantId"
        );
    }

    static string ResolveAuthority(MicrosoftEntraAuthenticationProviderSettings microsoftEntraSettings)
    {
        if (!string.IsNullOrWhiteSpace(microsoftEntraSettings.Authority))
        {
            return microsoftEntraSettings.Authority;
        }

        var instance = RequireConfigValue(
            microsoftEntraSettings.Instance,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:Instance"
        ).TrimEnd('/');
        var tenantId = RequireConfigValue(
            microsoftEntraSettings.TenantId,
            $"{MicrosoftEntraAuthenticationProviderSettings.SECTION_NAME}:TenantId"
        ).Trim('/');
        return $"{instance}/{tenantId}/v2.0";
    }

    static string ResolveScheme(string? scheme)
    {
        return string.IsNullOrWhiteSpace(scheme) ? OpenIdConnectDefaults.AuthenticationScheme : scheme;
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
