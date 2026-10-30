using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// The Athlete or the Horse a history of Participations is of (the rest-api skill): the filter starts with
/// <c>athleteId eq {guid}</c> or <c>horseId eq {guid}</c> (<see cref="LeadingFilter"/>), which is how the server learns whose
/// rides to read, and may go on with <c>and</c> and anything else the grammar allows. Nothing else asks for the rides of a
/// Participant without saying whose, and no filter turns the list into another's: the rides are read for the one named first,
/// and what follows can only leave some of them out.
/// </summary>
internal static class HistoryFilter
{
    const string ATHLETE_ID = "athleteId";
    const string HORSE_ID = "horseId";

    /// <summary>The Athlete or the Horse the filter starts with, or none when it does not start with one.</summary>
    public static (ParticipantKind Kind, Guid Id)? SubjectOf(string? filter)
    {
        if (LeadingFilter.Of(filter, ATHLETE_ID, HORSE_ID) is not { } subject)
        {
            return null;
        }

        return (subject.Member == ATHLETE_ID ? ParticipantKind.Athlete : ParticipantKind.Horse, subject.Id);
    }
}
