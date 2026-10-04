using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The answers of the access policy (ADR-0012) in the format of the rest-api skill: a status and a stable code for each
/// reason an action is refused, so that a client branches on the code and a person reads the title. A role that is
/// missing is 401 or 403; an Event that is not at a stage that takes the action is 409, as it is for a write to one that
/// has ended (ADR-0007).
/// </summary>
internal static class AccessResults
{
    public static IResult Refused(Verdict verdict)
    {
        return Of(
            verdict.Reason ?? throw new ArgumentException("An action that is allowed has no refusal.", nameof(verdict))
        );
    }

    public static IResult Of(Refusal reason)
    {
        return reason switch
        {
            Refusal.NotSignedIn => JsonApiResults.NotSignedIn(),
            Refusal.NotAllowed => JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "not-allowed",
                "You may not do this."
            ),
            Refusal.NotTenantRoot => JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "not-tenant-root",
                "Only a Tenant Root of the Tenant may do this."
            ),
            Refusal.NotMainOperator => JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "not-main-operator",
                "Only the Main Operator of the Event may do this."
            ),
            Refusal.NotDeveloper => JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "not-developer",
                "Only the Developer may do this."
            ),
            Refusal.TenantNotOperational => JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "tenant-not-operational",
                "The Tenant has no Tenant Root yet, so it cannot hold Events."
            ),
            Refusal.EventNotStarted => JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "event-not-started",
                "The Event has not started."
            ),
            Refusal.EventStarted => JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "event-started",
                "The Event has started: this can only be done before it does."
            ),
            Refusal.EventEnded => JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "event-ended",
                "The Event has ended, and nothing about it can be changed."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A refusal the Api has no answer for."),
        };
    }
}
