using System.Text.RegularExpressions;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// The member a filter of a list starts with, when the list is of someone (the rest-api skill): <c>member eq {guid}</c>, which
/// is how the server learns whose rows to read, and may go on with <c>and</c> and anything else the grammar allows. The
/// comparison may be in parentheses, which is how a client that joins several filters writes each of them. Nothing else can ask
/// for the rows without saying whose.
/// </summary>
internal static partial class LeadingFilter
{
    /// <summary>The member, of the ones given, that the filter starts with and the id it is compared with, or none when it starts with no such comparison.</summary>
    public static (string Member, Guid Id)? Of(string? filter, params string[] members)
    {
        var match = filter == null ? Match.Empty : Leading().Match(filter.Trim());
        var member = match.Groups["member"].Value;
        return match.Success && members.Contains(member) && Guid.TryParse(match.Groups["id"].Value, out var id)
            ? (member, id)
            : null;
    }

    [GeneratedRegex(
        @"^\(?(?<member>[A-Za-z]+) eq (?<id>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\)?(?: and |$)"
    )]
    private static partial Regex Leading();
}
