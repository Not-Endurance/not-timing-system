using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Not.Identity;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// The search for a Participant, an Athlete or a Horse, by name (#638, ADR-0010):
/// <c>GET /api/participants?filter=contains(name,'text')</c>, over the registries of every Tenant at once. A Participant is
/// found in either script by a few letters of each word of the name, in any order, so that a Latin text finds a Cyrillic name
/// and the other way round. It is the way to the history of a Participant, and what it tells of a match is what a person needs
/// to choose one and nothing more: the <c>id</c>, the <c>kind</c> and the <c>displayName</c>, in the language of the request.
/// It takes at least three characters, answers at most ten matches, and draws on the budget of searches that the other
/// searches of the registries draw on.
/// </summary>
internal static class ParticipantEndpoints
{
    const string PARTICIPANTS = "participants";

    public static IEndpointRouteBuilder MapParticipants(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/participants",
                (
                    HttpContext context,
                    UserManager<NIdentityUser> users,
                    CrossTenantReads reads,
                    SearchRateLimiter limiter,
                    AccountText text
                ) => SearchAsync(context, users, reads, limiter, text)
            )
            .RequireAuthorization();
        return app;
    }

    static async Task<IResult> SearchAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        CrossTenantReads reads,
        SearchRateLimiter limiter,
        AccountText text
    )
    {
        var admission = await RegistrySearchEndpoints.AdmitAsync(context, users, limiter);
        if (admission.Refusal != null)
        {
            return admission.Refusal;
        }

        var culture = CultureInfo.GetCultureInfo(text.Resolve(context.Request));
        var found = await reads.SearchParticipantsAsync(admission.Caller!, admission.Text!, context.RequestAborted);

        // The names are told in the language of the request.
        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        return JsonApiResults.Collection(
            PARTICIPANTS,
            found.Select(x =>
            {
                var named = x.NameIn(null, culture);
                return (x.Id.ToString(), (object)new { kind = named.Kind, displayName = named.DisplayName });
            })
        );
    }
}
