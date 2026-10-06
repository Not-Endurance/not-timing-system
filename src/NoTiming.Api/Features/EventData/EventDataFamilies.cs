using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The families of what an Event keeps that the Api serves (#604): the Participations, the Rankings, the Officials and the
/// Handouts. The Operators of an Event are not among them: they are grants (ADR-0012), and what a person may do about an
/// Event is told by its capabilities. What the families keep to themselves is the bookkeeping of the legacy documents, the
/// version of a Participation, which is told in its <c>meta</c>, and the account an Official is linked to, which is not a
/// name or a role and which no public view shows.
/// </summary>
internal static class EventDataFamilies
{
    static readonly string[] BOOKKEEPING = ["IsDeleted", "DeletedVersion"];

    public static IEndpointRouteBuilder MapEventData(this IEndpointRouteBuilder app)
    {
        app.Map(Participations());
        app.Map(Rankings());
        app.Map(Officials());
        app.Map(Handouts());
        return app;
    }

    public static EventDataFamily<ParticipationModel, Participation> Participations()
    {
        return new(
            "participations",
            TenantOwned.EVENT_PARTICIPATIONS,
            [.. BOOKKEEPING, nameof(ParticipationModel.Version)],
            announcesChanges: true,
            referencedBy:
            [
                new(TenantOwned.EVENT_RANKINGS, "Entries.ParticipationId"),
                new(TenantOwned.EVENT_HANDOUTS, nameof(HandoutModel.ParticipationId)),
            ]
        );
    }

    public static EventDataFamily<RankingModel, Ranking> Rankings()
    {
        return new("rankings", TenantOwned.EVENT_RANKINGS, BOOKKEEPING);
    }

    public static EventDataFamily<OfficialModel, Official> Officials()
    {
        return new("officials", TenantOwned.EVENT_OFFICIALS, [.. BOOKKEEPING, nameof(OfficialModel.UserId)]);
    }

    public static EventDataFamily<HandoutModel, Handout> Handouts()
    {
        return new("handouts", TenantOwned.EVENT_HANDOUTS, BOOKKEEPING);
    }
}
