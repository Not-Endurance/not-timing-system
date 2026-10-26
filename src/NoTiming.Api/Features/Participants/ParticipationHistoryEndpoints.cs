using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Not.Identity;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// The history of an Athlete or a Horse (#638, ADR-0010): <c>GET /api/participations/history</c>, a named view of the
/// Participations with one <c>participation-histories</c> resource for each ride of an Event that has ended, in any Tenant,
/// newest first, with the placing the ride has in the Ranking of its own Competition and Category and the other Rankings that
/// count it. The filter starts with the Participant it is of, <c>athleteId eq {id}</c> or <c>horseId eq {id}</c>, and may go on
/// with <c>and</c> and the members of a ride; <c>sort</c> and <c>page[...]</c> are the ones of every list. It is for signed-in
/// accounts alone, and reads across Tenants through the named reads of <see cref="CrossTenantReads"/>.
/// </summary>
internal static class ParticipationHistoryEndpoints
{
    const string PARTICIPATION_HISTORIES = "participation-histories";

    public static IEndpointRouteBuilder MapParticipationHistory(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/participations/history",
                (
                    HttpContext context,
                    UserManager<NIdentityUser> users,
                    ParticipationHistoryReader reader,
                    AccountText text,
                    TimeProvider time
                ) => ListAsync(context, users, reader, text, time)
            )
            .RequireAuthorization();
        return app;
    }

    static async Task<IResult> ListAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        ParticipationHistoryReader reader,
        AccountText text,
        TimeProvider time
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        // The Participant says whose rides to read, before anything else is looked at.
        if (HistoryFilter.SubjectOf(context.Request.Query["filter"].ToString()) is not { } subject)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "subject-required",
                "Name the Athlete or the Horse the rides are of.",
                "Start the filter with the Athlete or the Horse and join anything else to it with and: filter=athleteId eq <id> and ..."
            );
        }

        var query = ReferenceQuery<HistoryRide>.Read(context.Request, HistoryRide.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var culture = CultureInfo.GetCultureInfo(text.Resolve(context.Request));
        var (page, refusal) = await ReferenceEndpoints.ReadPageAsync(
            () =>
                reader.ReadAsync(
                    Standing.Account,
                    subject.Kind,
                    subject.Id,
                    culture,
                    time.GetUtcNow(),
                    query.Options,
                    query.Skip,
                    query.Size,
                    context.RequestAborted
                )
        );
        if (refusal != null)
        {
            return refusal;
        }

        // The names are told in the language of the request.
        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        return JsonApiResults.Collection(
            PARTICIPATION_HISTORIES,
            page!.Lines.Select(x => (x.Id.ToString(), x.Attributes())),
            links: ReferenceEndpoints.Links(context.Request, query, page.HasMore),
            meta: JsonApiPaging.Meta(query.Size, query.Number),
            itemLink: id => $"/api/participations/{id}"
        );
    }
}
