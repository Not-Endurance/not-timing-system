using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// A new person registers themselves (#601, ADR-0002): a registration is created with their names, address and country (the
/// page that asks for them is <see cref="AccountPages"/>). Registering mails a code and stores nothing; the account exists when
/// the code comes back to the sessions route, which is also where an address that already has an account signs in.
/// </summary>
internal static class RegistrationEndpoints
{
    const string REGISTRATIONS = "registrations";

    public static IEndpointRouteBuilder MapRegistrations(this IEndpointRouteBuilder app)
    {
        // Like the sign-in page this is anonymous, and the list of public endpoints says so: it is how anyone gets an account.
        app.MapPost("/api/registrations", PostRegistrations);
        return app;
    }

    static async Task<IResult> PostRegistrations(
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
            return AccountFailures.InvalidEmail.AsResult();
        }

        if (
            !RegistrationDetails.IsValidName(attributes.GivenName)
            || !RegistrationDetails.IsValidName(attributes.Surname)
        )
        {
            return AccountFailures.RegistrationInvalidName.AsResult();
        }

        var country = Guid.TryParse(attributes.CountryId, out var countryId)
            ? await countries.FindAsync(countryId, context.RequestAborted)
            : null;
        if (country is null)
        {
            return AccountFailures.RegistrationInvalidCountry.AsResult();
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
}
