using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Not.Identity.Codes;
using Not.Identity.Email;
using Not.Identity.Mongo;
using Not.Identity.Sessions;

namespace Not.Identity;

public static class NIdentityServiceCollectionExtensions
{
    /// <summary>
    /// ASP.NET Core Identity over the application's user documents, a server-side cookie session and one-time codes
    /// (ADR-0002). The application registers an <c>IMongoClient</c> and an <see cref="IEmailSender"/>, and names its
    /// data protection application.
    /// </summary>
    public static IdentityBuilder AddNIdentity(
        this IServiceCollection services,
        Action<NIdentityOptions>? configure = null,
        Action<CookieAuthenticationOptions>? configureCookie = null
    )
    {
        MongoSerialization.Register();

        services.AddOptions<NIdentityOptions>().Configure(options => configure?.Invoke(options));
        services.TryAddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddDataProtection();

        // Identity adds its own normalizer only when there is none: names and emails are normalized to lower case.
        services.AddSingleton<ILookupNormalizer, LowerInvariantLookupNormalizer>();

        services.AddSingleton<MongoTicketStore>();
        services.AddSingleton<ITicketStore>(provider => provider.GetRequiredService<MongoTicketStore>());
        services.AddSingleton<ISessionRevoker>(provider => provider.GetRequiredService<MongoTicketStore>());
        services.AddSingleton<ICodeChallengeStore, MongoCodeChallengeStore>();
        services.AddHostedService<IdentityIndexInitializer>();
        services.AddHostedService<EmailSenderGuard>();

        var builder = services
            .AddIdentityCore<NIdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // the user name is the email, whatever it contains
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddUserStore<MongoUserStore<NIdentityUser>>()
            .AddSignInManager();

        services
            .AddOptions<SecurityStampValidatorOptions>()
            .Configure<IOptions<NIdentityOptions>>(
                (validator, identity) => validator.ValidationInterval = identity.Value.StampValidationInterval
            );

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddCookie(IdentityConstants.ApplicationScheme);

        // A passkey ceremony keeps its challenge in a cookie of this scheme for the few minutes it takes. It is not a
        // session: nothing is signed in with it.
        authentication
            .AddTwoFactorUserIdCookie()
            .Configure<IOptions<NIdentityOptions>, TimeProvider>(
                (cookie, identity, time) =>
                {
                    cookie.TimeProvider = time;
                    cookie.Cookie.Name = identity.Value.CeremonyCookieName;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                    cookie.Cookie.Path = "/";
                    cookie.ExpireTimeSpan = identity.Value.CeremonyLifetime;
                }
            );
        services
            .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<NIdentityOptions>, ITicketStore, TimeProvider>(
                (cookie, identity, tickets, time) =>
                {
                    cookie.TimeProvider = time; // one clock for the ticket store, the cookie and the stored codes
                    cookie.Cookie.Name = identity.Value.CookieName;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                    cookie.Cookie.Path = "/";
                    cookie.Cookie.Domain = null;
                    cookie.Cookie.IsEssential = true;
                    cookie.ExpireTimeSpan = identity.Value.SessionLifetime;
                    cookie.SlidingExpiration = true;
                    cookie.SessionStore = tickets;
                    cookie.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
                    configureCookie?.Invoke(cookie);
                }
            );

        return builder;
    }
}
