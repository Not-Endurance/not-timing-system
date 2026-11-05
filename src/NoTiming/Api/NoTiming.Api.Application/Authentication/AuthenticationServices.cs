using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NoTiming.Api.Application.Authentication.Account;
using NoTiming.Api.Application.Authentication.Adoption;
using NoTiming.Api.Application.Authentication.Codes;
using NoTiming.Api.Application.Authentication.Email;
using NoTiming.Api.Application.Authentication.Passkeys;
using NoTiming.Api.Application.Authentication.RateLimits;
using NoTiming.Api.Application.Authentication.Requests;
using NoTiming.Api.Application.Authentication.Sessions;
using NoTiming.Api.Application.Authentication.SignIn;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication;

public static class AuthenticationServices
{
    const string CEREMONY_COOKIE = "__Host-ceremony";

    /// <summary>
    /// Sign-in by passkey or emailed code, over a server-side cookie session. The session scheme is the default, so the
    /// REST routes and the hubs read the same session. The settings are validated when the host starts.
    /// </summary>
    public static IServiceCollection AddNoTimingAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthenticationSettings>().Bind(configuration.GetSection(AuthenticationSettings.SECTION)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthenticationSettings>, AuthenticationSettingsValidator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IdentityUserStore>();
        services.AddIdentityCore<IdentityUserDocument>().AddUserStore<IdentityUserStore>().AddSignInManager();
        services.AddHttpContextAccessor();
        services.AddHostedService<AuthenticationIndexes>();

        // The passkey handler's options: one relying party, discoverable keys, user verification, and no attestation.
        services
            .AddOptions<IdentityPasskeyOptions>()
            .Configure<IOptions<AuthenticationSettings>>(
                (options, settingsOptions) =>
                {
                    var settings = settingsOptions.Value;
                    options.ServerDomain = settings.RelyingPartyId;
                    options.UserVerificationRequirement = "required";
                    options.ResidentKeyRequirement = "required";
                    options.AttestationConveyancePreference = "none";
                    options.VerifyAttestationStatement = _ => ValueTask.FromResult(true);
                    options.ValidateOrigin = context =>
                        ValueTask.FromResult(
                            !context.CrossOrigin
                                && string.Equals(context.Origin, settings.AppOrigin, StringComparison.OrdinalIgnoreCase)
                        );
                }
            );

        // A passkey ceremony keeps its challenge in this cookie for the few minutes it takes. Nothing is signed in with it.
        services
            .AddAuthentication()
            .AddTwoFactorUserIdCookie()
            .Configure<IOptions<AuthenticationSettings>, TimeProvider>(
                (cookie, _, time) =>
                {
                    cookie.TimeProvider = time;
                    cookie.Cookie.Name = CEREMONY_COOKIE;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                    cookie.Cookie.Path = "/";
                    cookie.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                }
            );

        services.AddSingleton<SessionStore>();
        services.AddSingleton<CodeChallengeStore>();
        services.AddSingleton<RateCounterStore>();
        services.AddSingleton<CodeSendGate>();
        services.AddSingleton<AccountSignInService>();
        services.AddSingleton<AccountService>();
        services.AddScoped<PasskeyService>();
        services.AddScoped<IPasskeyCeremony, SignInManagerPasskeyCeremony>();
        services.AddSingleton<IPasskeyUsers>(provider => provider.GetRequiredService<IdentityUserStore>());
        services.AddSingleton<IPasskeySessionStore>(provider => provider.GetRequiredService<SessionStore>());
        services.AddSingleton<ILegacyUserAdoption, LegacyUserAdoption>();
        services.AddScoped<StateChangeGuard>();

        services.AddSingleton<EmailDispatchQueue>();
        services.AddHostedService<EmailDispatchService>();
        services.AddHttpClient<BrevoEmailSender>();
        services.AddSingleton<ConsoleEmailSender>();
        services.AddSingleton<OutboxEmailSender>();
        services.AddSingleton<IEmailSender>(provider => ChooseEmailSender(provider));

        // The session is the default scheme: the REST routes and the hubs read the same cookie the Ui signs in with.
        services
            .AddAuthentication(SessionAuthenticationHandler.SCHEME)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SCHEME, _ => { });

        return services;
    }

    static IEmailSender ChooseEmailSender(IServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<AuthenticationSettings>>().Value.EmailProvider.ToLowerInvariant() switch
        {
            "brevo" => provider.GetRequiredService<BrevoEmailSender>(),
            "console" => provider.GetRequiredService<ConsoleEmailSender>(),
            "outbox" => provider.GetRequiredService<OutboxEmailSender>(),
            var unknown => throw new InvalidOperationException($"Unknown email provider '{unknown}'."),
        };
    }
}
