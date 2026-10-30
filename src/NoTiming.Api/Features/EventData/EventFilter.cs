using System.Text.RegularExpressions;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The Event a list of the rows of an Event is of (the rest-api skill: an Event-scoped resource is flat and carries the
/// Event as an attribute): the filter starts with <c>eventId eq {guid}</c>, which is how the server learns whose Tenant to
/// read in, and may go on with <c>and</c> and anything else the grammar allows. The Event may be in parentheses, which is how
/// a client that joins several filters writes each of them. Nothing else can ask for rows without saying whose.
/// </summary>
internal static partial class EventFilter
{
    /// <summary>The Event the filter starts with, or none when it does not start with one.</summary>
    public static Guid? EventOf(string? filter)
    {
        return LeadingFilter.Of(filter, "eventId")?.Id;
    }

    /// <summary>
    /// Whether the filter names one of the members, as a comparison does: a member that is only a word of a text it compares with
    /// is not named.
    /// </summary>
    public static bool Names(string? filter, IEnumerable<string> members)
    {
        var outsideTexts = filter == null ? string.Empty : Texts().Replace(filter, "''");
        return members.Any(x => outsideTexts.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("'(?:[^']|'')*'")]
    private static partial Regex Texts();
}
