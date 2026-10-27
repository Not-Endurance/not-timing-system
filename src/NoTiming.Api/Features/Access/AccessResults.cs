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
        return FailureOf(
                verdict.Reason
                    ?? throw new ArgumentException("An action that is allowed has no refusal.", nameof(verdict))
            )
            .AsResult();
    }

    /// <summary>The refusal as an error.</summary>
    public static JsonApiFailure FailureOf(Refusal reason)
    {
        return reason switch
        {
            Refusal.NotSignedIn => AccessFailures.NotSignedIn,
            Refusal.NotAllowed => AccessFailures.NotAllowed,
            Refusal.NotRoot => AccessFailures.NotRoot,
            Refusal.NotConsoleOperator => AccessFailures.NotConsoleOperator,
            Refusal.NotDeveloper => AccessFailures.NotDeveloper,
            Refusal.TenantNotOperational => AccessFailures.TenantNotOperational,
            Refusal.EventNotStarted => AccessFailures.EventNotStarted,
            Refusal.EventStarted => AccessFailures.EventStarted,
            Refusal.EventEnded => AccessFailures.EventEnded,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A refusal the Api has no answer for."),
        };
    }
}
