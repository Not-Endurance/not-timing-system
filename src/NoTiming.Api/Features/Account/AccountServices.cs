using Microsoft.AspNetCore.DataProtection;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Email;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

internal static class AccountServices
{
    /// <summary>
    /// Names the data protection key ring, which protects the session cookie and the stored codes (ADR-0002): the
    /// default key ring of the host is used, so what one host protects another of the same name can read.
    /// </summary>
    public const string APPLICATION_NAME = "NoTiming";

    public static IServiceCollection AddAccount(this IServiceCollection services)
    {
        // The connection string and the email sender are read when they are first needed, not when the services are
        // registered, so the settings of the host are all in place (a test host adds its own after Program has run).
        services.AddSingleton<IMongoClient>(provider => new MongoClient(
            ConnectionStringOf(provider.GetRequiredService<IConfiguration>())
        ));
        services.AddSingleton<IEmailSender>(CreateEmailSender);
        services.AddSingleton<IEmailOutbox>(provider =>
            provider.GetRequiredService<IEmailSender>() as IEmailOutbox
            ?? throw new InvalidOperationException("The email sender keeps no outbox: set Email:Sender to Outbox.")
        );

        services.AddDataProtection().SetApplicationName(APPLICATION_NAME);
        services.AddSingleton(AccountText.Load());
        services.AddSingleton<SelectableCountries>();
        services.AddSingleton<TenantPlacement>();
        services.AddSingleton<RegistrationPolicy>();
        services.AddSingleton<EventThrottle>();
        services
            .AddOptions<AuthRateLimitOptions>()
            .BindConfiguration(AuthRateLimitOptions.SECTION)
            .Validate(
                AuthRateLimitOptions.IsValid,
                "Auth:RateLimits: every limit is at least 1 and the window is not zero."
            )
            .ValidateOnStart();
        services.AddSingleton<AuthRateLimiter>();
        services.AddScoped<CodeSignIn>();
        services.AddPasskeys();

        services.AddNIdentity(
            options =>
            {
                options.CookieName = "__Host-NoTiming";
                options.CeremonyCookieName = "__Host-NoTiming-Ceremony";
            },
            cookie =>
            {
                // The Ui asks who is signed in and navigates to the sign-in page itself: no redirect, only the answer.
                cookie.Events.OnRedirectToLogin = context =>
                    JsonApiResults.NotSignedIn().ExecuteAsync(context.HttpContext);
                cookie.Events.OnRedirectToAccessDenied = context =>
                    JsonApiResults.WriteErrorAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "not-allowed",
                        "You may not do this."
                    );
            }
        );
        services.AddAuthorization();

        return services;
    }

    static string ConnectionStringOf(IConfiguration configuration)
    {
        var connectionString = configuration["MONGO_CONNECTION_STRING"];
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "MONGO_CONNECTION_STRING is not set. The Api keeps its users and their sessions in MongoDB."
            )
            : connectionString;
    }

    static IEmailSender CreateEmailSender(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var sender = configuration["Email:Sender"] ?? (environment.IsDevelopment() ? "Console" : null);
        return sender switch
        {
            "Brevo" => CreateBrevoSender(configuration),
            "Console" => ActivatorUtilities.CreateInstance<ConsoleEmailSender>(provider),
            "Outbox" => new OutboxEmailSender(),
            null => new UnconfiguredEmailSender(),
            _ => throw new InvalidOperationException(
                $"The email sender '{sender}' is not known: use Brevo, Console or Outbox."
            ),
        };
    }

    /// <summary>
    /// Brevo's key and sender identity come from the configuration of the host (its secrets). A host that has none
    /// starts, and the first code it has to send fails loudly. The client lives as long as the host does, so its
    /// connections are renewed now and then to follow a change of the provider's addresses.
    /// </summary>
    static BrevoEmailSender CreateBrevoSender(IConfiguration configuration)
    {
        var options = configuration.GetSection(BrevoOptions.SECTION).Get<BrevoOptions>() ?? new BrevoOptions();
        var client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
        return new BrevoEmailSender(client, options);
    }
}

/// <summary>
/// What a host without an email provider has: it starts, and the first code it has to send fails loudly instead of
/// being dropped.
/// </summary>
internal sealed class UnconfiguredEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("No email provider is configured, so no code can be sent.");
    }
}
