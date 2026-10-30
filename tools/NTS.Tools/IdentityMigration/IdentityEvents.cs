using System.Globalization;
using MongoDB.Bson;
using NTS.Tools.Shared;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The names the report gives the Events a copy is in (#636): the dates, then the name, so that a list of them is in time order.
/// A review reads them to tell whether two spellings were ever in the same Event, which two people were and one person was not.
/// </summary>
public static class IdentityEvents
{
    /// <summary>
    /// The label of each Event by its id as text. An Event that has started is named by its own document; a Setup that has no Event
    /// yet, by its name and the day its first Competition starts. An Event with no name is left out.
    /// </summary>
    public static Dictionary<string, string> Labels(
        IEnumerable<BsonDocument> informations,
        IEnumerable<BsonDocument> setups
    )
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var information in informations)
        {
            if (information.TryGetValue("_id", out var id) && Label(information, DaysOf(information)) is { } label)
            {
                labels[IdentityCopies.Describe(id)] = label;
            }
        }

        foreach (var setup in setups)
        {
            if (
                setup.TryGetValue("_id", out var id)
                && !labels.ContainsKey(IdentityCopies.Describe(id))
                && Label(setup, StartOfFirstCompetition(setup)) is { } label
            )
            {
                labels[IdentityCopies.Describe(id)] = label;
            }
        }

        return labels;
    }

    /// <summary>
    /// When each Event began, by its id as text, to tell the latest of the copies of a person: the start of an Event that has started,
    /// and the start of the first Competition of a Setup that has not.
    /// </summary>
    public static Dictionary<string, DateTime> Dates(
        IEnumerable<BsonDocument> informations,
        IEnumerable<BsonDocument> setups
    )
    {
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var information in informations)
        {
            if (
                information.TryGetValue("_id", out var id)
                && (DateOf(information, "StartDay") ?? DateOf(information, "EndDay")) is { } date
            )
            {
                dates[IdentityCopies.Describe(id)] = date;
            }
        }

        foreach (var setup in setups)
        {
            if (
                setup.TryGetValue("_id", out var id)
                && !dates.ContainsKey(IdentityCopies.Describe(id))
                && setup.TryGetValue("Competitions", out var competitions)
                && competitions is BsonArray array
                && array
                    .OfType<BsonDocument>()
                    .Select(x => DateOf(x, "Start"))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToList()
                    is { Count: > 0 } starts
            )
            {
                dates[IdentityCopies.Describe(id)] = starts.Min();
            }
        }

        return dates;
    }

    /// <summary>
    /// The first and the last day of an Event, as the days of the place. They are stored as instants: the first second of the first
    /// day and the last second of the last, wherever the Event is. Half a day on from the first and half a day back from the last is
    /// inside the right day in every zone, so the date is the same on whatever machine the report is made.
    /// </summary>
    static string? DaysOf(BsonDocument information)
    {
        var first = DateOf(information, "StartDay")?.AddHours(12);
        var last = DateOf(information, "EndDay")?.AddHours(-12);
        return first == null || last == null || first.Value.Date == last.Value.Date
            ? Day(first ?? last)
            : $"{Day(first)} to {Day(last)}";
    }

    /// <summary>The UTC day the first Competition of a Setup starts: a Setup has no days of its own, and a start is a time of the day.</summary>
    static string? StartOfFirstCompetition(BsonDocument setup)
    {
        var starts =
            setup.TryGetValue("Competitions", out var competitions) && competitions is BsonArray array
                ? array
                    .OfType<BsonDocument>()
                    .Select(x => DateOf(x, "Start"))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToList()
                : [];
        return starts.Count == 0 ? null : Day(starts.Min());
    }

    static string? Label(BsonDocument document, string? days)
    {
        if (Records.TextOf(document, "Name") is not { } name)
        {
            return null;
        }

        return days == null ? name : $"{days} {name}";
    }

    static DateTime? DateOf(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value is BsonDateTime date ? date.ToUniversalTime() : null;
    }

    static string? Day(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
