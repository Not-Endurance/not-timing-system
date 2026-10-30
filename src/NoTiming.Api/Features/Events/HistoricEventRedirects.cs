using NoTiming.Api.Features.Access;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The pages of the Historic Events were at <c>/past-events</c> until #628 named them as the glossary does, and a bookmark
/// still goes there. The host that serves the Ui sends it on to the new address with what it asked for, before the Ui is
/// loaded, so the Ui has no page at the old one. Only the addresses that were pages are sent on; the id of an Event is a
/// Guid, and what is not one is left to the Ui, which has no such page.
/// </summary>
internal static class HistoricEventRedirects
{
    public static IEndpointRouteBuilder MapHistoricEventRedirects(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/past-events",
            (HttpContext context) => Results.Redirect("/historic-events" + context.Request.QueryString, permanent: true)
        );
        app.MapGet(
            "/past-events/{eventId:guid}",
            (Guid eventId, HttpContext context) =>
                Results.Redirect($"/historic-events/{eventId}{context.Request.QueryString}", permanent: true)
        );
        return app;
    }
}
