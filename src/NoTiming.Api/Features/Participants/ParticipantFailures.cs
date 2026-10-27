using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// The answers of Participant that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class ParticipantFailures
{
    public static JsonApiFailure HorseLimitReached(int max)
    {
        return new JsonApiFailure(
            StatusCodes.Status422UnprocessableEntity,
            "horse-limit-reached",
            $"A person links at most {max} Horses."
        );
    }

    public static JsonApiFailure SubjectRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "subject-required",
            "Name the Athlete or the Horse the rides are of.",
            "Start the filter with the Athlete or the Horse and join anything else to it with and: filter=athleteId eq <id> and ..."
        );
}
