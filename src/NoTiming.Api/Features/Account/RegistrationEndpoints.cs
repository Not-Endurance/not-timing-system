using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// A new person registers themselves (#601, ADR-0002): a page that asks for their names, address and country, and a
/// registration that is created with them. Registering mails a code and stores nothing; the account exists when the
/// code comes back to the sessions route, which is also where an address that already has an account signs in.
/// </summary>
internal static class RegistrationEndpoints
{
    const string REGISTRATIONS = "registrations";

    public static IEndpointRouteBuilder MapRegistration(this IEndpointRouteBuilder app)
    {
        // Like the sign-in page these have to be anonymous: they are how anyone gets an account.
        app.MapGet("/register", AccountPages.Register).AllowAnonymous();
        app.MapGet("/privacy", AccountPages.Privacy).AllowAnonymous();
        app.MapPost("/api/registrations", Register).AllowAnonymous();
        return app;
    }

    static async Task<IResult> Register(
        HttpContext context,
        CodeSignIn codeSignIn,
        SelectableCountries countries,
        AuthRateLimiter limits,
        EventThrottle throttle,
        AccountText text,
        ILoggerFactory loggers
    )
    {
        var read = await JsonApiRequests.ReadAsync<RegistrationAttributes>(context.Request, REGISTRATIONS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var attributes = read.Attributes!;

        // A browser that has registered before keeps its secret, so that asking again inside the cooldown of a code,
        // which leaves the details of the first request standing, does not cut the browser off from them.
        var presented = context.Request.Cookies[RegistrationTokens.COOKIE];
        var token = RegistrationTokens.IsWellFormed(presented) ? presented! : RegistrationTokens.New();

        // The page has a field that no person sees or reaches, and only a program that fills in every field fills it
        // in. It is answered as anyone is, so that it learns nothing, and nothing is sent or kept.
        if (!string.IsNullOrWhiteSpace(attributes.Website))
        {
            if (throttle.ShouldLog(AuthEvents.HONEYPOT_FILLED.Name!, out var leftOut))
            {
                loggers
                    .CreateLogger(typeof(RegistrationEndpoints))
                    .LogWarning(
                        AuthEvents.HONEYPOT_FILLED,
                        "A registration was dropped: the hidden field was filled in. {LeftOut} dropped since the last report.",
                        leftOut
                    );
            }

            return Accepted(context, token);
        }

        if (!CodeSignIn.IsValidEmail(attributes.Email))
        {
            return Invalid("invalid-email", "Enter a valid email address.");
        }

        if (
            !RegistrationDetails.IsValidName(attributes.GivenName)
            || !RegistrationDetails.IsValidName(attributes.Surname)
        )
        {
            return Invalid("invalid-name", "Enter your first name and your surname.");
        }

        var country = Guid.TryParse(attributes.CountryId, out var countryId)
            ? await countries.FindAsync(countryId, context.RequestAborted)
            : null;
        if (country is null)
        {
            return Invalid("invalid-country", "Choose a country from the list.");
        }

        // A registration draws on the same budgets as a request for a code, and costs the same whether or not the
        // address has an account or may register.
        if (limits.TrySend(attributes.Email!, ClientAddress.Of(context)) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        await codeSignIn.RequestRegistrationAsync(
            attributes.Email!,
            new RegistrationDetails(attributes.GivenName!, attributes.Surname!, country),
            token,
            text.Resolve(context.Request),
            context.RequestAborted
        );

        // 202 for an address with an account, without one and cooling down alike: the answer tells nothing.
        return Accepted(context, token);
    }

    /// <summary>
    /// Every 202 sets the same cookie, whatever was done with the request, so that setting it tells nothing either.
    /// </summary>
    static IResult Accepted(HttpContext context, string token)
    {
        context.Response.Cookies.Append(RegistrationTokens.COOKIE, token, RegistrationTokens.Options());
        return Results.Accepted();
    }

    static IResult Invalid(string code, string title)
    {
        return JsonApiResults.Error(StatusCodes.Status400BadRequest, code, title);
    }
}

internal sealed class RegistrationAttributes
{
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }

    /// <summary>The id of one of the countries the page lists, as the page sends it.</summary>
    public string? CountryId { get; set; }

    /// <summary>The field of the page that people do not see: it is empty unless a program filled it in.</summary>
    public string? Website { get; set; }
}
