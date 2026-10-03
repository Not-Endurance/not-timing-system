using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Passkeys through ASP.NET Core Identity (ADR-0002): discoverable, user verification required, no attestation
/// validated, and never a password. The relying-party ID is configuration, because a passkey is bound to it for good:
/// the apex in production, <c>staging.&lt;apex&gt;</c> in staging, <c>localhost</c> locally. A host that has none
/// does not offer passkeys at all, so it can be deployed before the domain is final.
/// </summary>
internal static class PasskeyServices
{
    public const string RELYING_PARTY_SETTING = "Passkeys:RelyingPartyId";
    public const string ANTIFORGERY_HEADER = "X-XSRF-TOKEN";
    public const string ANTIFORGERY_COOKIE = "__Host-NoTiming-Xsrf";
    public const string ANTIFORGERY_COOKIE_DEVELOPMENT = "NoTiming-Xsrf";

    public static string? RelyingPartyIdOf(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[RELYING_PARTY_SETTING];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().ToLowerInvariant();
        }

        return environment.IsDevelopment() ? "localhost" : null;
    }

    /// <summary>
    /// An origin may use the passkeys of a relying party when it is the relying party's domain or one of its
    /// subdomains, over https. Only <c>localhost</c> may use plain http, for development.
    /// </summary>
    public static bool IsAllowedOrigin(string? origin, string relyingPartyId)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var inTheDomain = host == relyingPartyId || host.EndsWith("." + relyingPartyId, StringComparison.Ordinal);
        var secure = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && host == "localhost");
        return inTheDomain && secure;
    }

    public static IServiceCollection AddPasskeys(this IServiceCollection services)
    {
        // The same tokens that Razor forms would carry: the pages here are static, so the server puts one in each.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = ANTIFORGERY_HEADER;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // The antiforgery system refuses a Secure-only cookie on a request that is not https, and development runs on
        // plain http. There the cookie follows the request and has no __Host- prefix (a browser rejects that prefix on a
        // cookie that is not Secure). Everywhere else it is host-only and Secure.
        services
            .AddOptions<AntiforgeryOptions>()
            .Configure<IHostEnvironment>(
                (options, environment) =>
                {
                    var development = environment.IsDevelopment();
                    options.Cookie.Name = development ? ANTIFORGERY_COOKIE_DEVELOPMENT : ANTIFORGERY_COOKIE;
                    options.Cookie.SecurePolicy = development
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                }
            );

        services.AddSingleton(provider => new PasskeyAvailability(
            RelyingPartyIdOf(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IHostEnvironment>()
            )
        ));
        services
            .AddOptions<IdentityPasskeyOptions>()
            .Configure<PasskeyAvailability>(
                (options, availability) =>
                {
                    if (availability.RelyingPartyId is not { } relyingPartyId)
                    {
                        return;
                    }

                    options.ServerDomain = relyingPartyId;
                    options.UserVerificationRequirement = "required";
                    options.ResidentKeyRequirement = "required"; // discoverable: the person does not type who they are
                    options.AttestationConveyancePreference = "none";
                    options.VerifyAttestationStatement = _ => ValueTask.FromResult(true); // no attestation is validated
                    options.ValidateOrigin = context =>
                        ValueTask.FromResult(!context.CrossOrigin && IsAllowedOrigin(context.Origin, relyingPartyId));
                }
            );

        services.AddScoped<PasskeyService>();
        return services;
    }
}

/// <summary>Whether this host offers passkeys, which it does only when it knows its relying-party ID.</summary>
internal sealed class PasskeyAvailability
{
    public PasskeyAvailability(string? relyingPartyId)
    {
        RelyingPartyId = relyingPartyId;
    }

    public string? RelyingPartyId { get; }
    public bool Enabled => RelyingPartyId != null;
}
