using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Adoption;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Email.Outbox;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Profiles;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Server.Authentication.SignIn;
using NC.Server.Authentication.Stores.MongoDB.Indexes;
using NC.Server.Authentication.Stores.MongoDB.Mapping;
using NC.Server.Authentication.Stores.MongoDB.Migration;
using NC.Server.Authentication.Stores.MongoDB.Repositories;

namespace NC.Server.Authentication.Interface;

public static class AuthenticationServices
{
    const string CEREMONY_COOKIE = "__Host-ceremony";

    /// <summary>
    /// Sign-in by passkey or emailed code, over a server-side cookie session. The session scheme is the default, so the
    /// REST routes and the hubs read the same session. The settings are validated when the host starts. The emails are
    /// written in the languages the host passes in, which the host reads from the resources it ships.
    /// </summary>
    public static IServiceCollection AddNoTimingAuthentication<TProfile>(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<string> emailLanguages
    )
        where TProfile : class, new()
    {
        AuthenticationBsonMapping.Register();
        services.AddSingleton(new EmailLanguage(emailLanguages));

        services
            .AddOptions<AuthenticationSettings>()
            .Bind(configuration.GetSection(AuthenticationSettings.SECTION))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthenticationSettings>, AuthenticationSettingsValidator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IdentityUserRepository>();
        services.AddSingleton<IIdentityUserRepository>(provider =>
            provider.GetRequiredService<IdentityUserRepository>()
        );
        services.AddIdentityCore<IdentityUserDocument>().AddUserStore<IdentityUserRepository>().AddSignInManager();
        services.AddHttpContextAccessor();
        services.AddHostedService<AuthenticationIndexes>();
        services.AddHostedService<UserProfileMigration>();

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

        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<ICodeChallengeRepository, CodeChallengeRepository>();
        services.AddSingleton<IRateCounterRepository, RateCounterRepository>();
        services.AddSingleton<CodeSendGate>();
        services.AddSingleton<AccountSignInService>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<SignInCompletion>();
        services.AddScoped<PasskeyService>();
        services.AddScoped<IPasskeyManager>(provider => provider.GetRequiredService<PasskeyService>());
        services.AddScoped<IAuthentication<TProfile>, AuthenticationService<TProfile>>();
        services.AddScoped<AccountProfiles<TProfile>>();
        services.AddSingleton<IUserDirectory<TProfile>, UserDirectory<TProfile>>();
        services.TryAddSingleton<IProfileValidator<TProfile>, AcceptAnyProfile<TProfile>>();
        services.AddScoped<IPasskeyCeremony, SignInManagerPasskeyCeremony>();
        services.AddSingleton<ILegacyUserAdoption, LegacyUserAdoption>();

        services.AddSingleton<EmailDispatchQueue>();
        services.AddHostedService<EmailDispatchService>();
        services.AddHttpClient<BrevoEmailSender>();
        services.AddSingleton<ConsoleEmailSender>();
        services.AddSingleton<OutboxEmailSender>();
        services.AddSingleton<IEmailSender>(provider => ChooseEmailSender(provider));

        // The session is the default scheme: the REST routes and the hubs read the same cookie the Ui signs in with.
        services
            .AddAuthentication(SessionAuthenticationHandler.SCHEME)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationHandler.SCHEME,
                _ => { }
            );

        services.AddAuthorization(options =>
            options.AddPolicy(AuthenticationPolicies.SIGNED_IN, policy => policy.RequireAuthenticatedUser())
        );
        return services;
    }

    static IEmailSender ChooseEmailSender(IServiceProvider provider)
    {
        return provider
            .GetRequiredService<IOptions<AuthenticationSettings>>()
            .Value.EmailProvider.ToLowerInvariant() switch
        {
            "brevo" => provider.GetRequiredService<BrevoEmailSender>(),
            "console" => provider.GetRequiredService<ConsoleEmailSender>(),
            "outbox" => provider.GetRequiredService<OutboxEmailSender>(),
            var unknown => throw new InvalidOperationException($"Unknown email provider '{unknown}'."),
        };
    }
}
