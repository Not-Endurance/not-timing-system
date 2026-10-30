using System.Text.RegularExpressions;

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
        var match = filter == null ? Match.Empty : Leading().Match(filter.Trim());
        return match.Success && Guid.TryParse(match.Groups["id"].Value, out var id) ? id : null;
    }

    [GeneratedRegex(
        @"^\(?eventId eq (?<id>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\)?(?: and |$)"
    )]
    private static partial Regex Leading();
}
