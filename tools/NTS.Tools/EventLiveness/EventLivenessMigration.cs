using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.EventLiveness;

/// <summary>
/// ADR-0007: an Event is Live until the end of its last day and Historic from then on, which the clock decides, and no flag
/// is stored. This removes the flag the code before it kept, <c>IsActive</c>, from the documents of the Events. Old documents
/// still load without it, so the removal is cleanup and no precondition of the code. What the command is really for is the
/// list it makes first: the Events that were inactive and whose last day is still ahead, which are Live again under the
/// rule, and which the owner decides about (correct the last day, or remove the Event) before the flag that said otherwise
/// goes. The flag is read as an old shape: an Event with none counts as inactive only while some Event still stores one,
/// because once it is gone from every document (the command has run, or the Events were all made after it) a Live Event has
/// none and is not asking anything. A second run therefore finds nothing to ask and nothing to remove.
/// </summary>
public static class EventLivenessMigration
{
    const string ID = "_id";
    const string NAME = "Name";
    const string IS_ACTIVE = "IsActive";
    const string END_DAY = "EndDay";
    const string IS_DELETED = "IsDeleted";
    public const string EVENTS = "event_informations";

    /// <summary>
    /// Reports what it found, and with <paramref name="apply"/> removes the flag, unless an Event that is inactive has a last
    /// day ahead of <paramref name="now"/> or a last day is not a date: then nothing is written and the report says which.
    /// </summary>
    public static async Task<EventLivenessReport> Run(IMongoDatabase database, bool apply, DateTimeOffset now)
    {
        var events = database.GetCollection<BsonDocument>(EVENTS);
        var documents = await events.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var flagIsStored = documents.Any(x => x.Contains(IS_ACTIVE));
        var report = new EventLivenessReport(apply, now) { WithFlag = documents.Count(x => x.Contains(IS_ACTIVE)) };
        foreach (var document in documents)
        {
            Classify(document, now, flagIsStored, report);
        }

        if (!apply || report.Refused)
        {
            return report;
        }

        if (report.WithFlag > 0)
        {
            var removal = await events.UpdateManyAsync(
                Builders<BsonDocument>.Filter.Exists(IS_ACTIVE),
                Builders<BsonDocument>.Update.Unset(IS_ACTIVE)
            );
            report.Removed = (int)removal.ModifiedCount;
        }

        report.Applied = true;
        return report;
    }

    static void Classify(BsonDocument document, DateTimeOffset now, bool flagIsStored, EventLivenessReport report)
    {
        report.Seen++;
        if (document.TryGetValue(IS_DELETED, out var deleted) && deleted is BsonBoolean { Value: true })
        {
            report.Deleted++;
            return;
        }

        if (!document.TryGetValue(END_DAY, out var end) || end is not BsonDateTime lastDay)
        {
            report.Unreadable.Add(new UnreadableLastDay(document[ID], NameOf(document), end?.BsonType));
            return;
        }

        var endsAt = lastDay.ToUniversalTime();
        var isLive = now.UtcDateTime < endsAt;
        if (isLive)
        {
            report.Live++;
        }
        else
        {
            report.Historic++;
        }

        document.TryGetValue(IS_ACTIVE, out var flag);
        if (isLive && flagIsStored && flag is not BsonBoolean { Value: true })
        {
            report.Revived.Add(new RevivedEvent(document[ID], NameOf(document), endsAt, DescribeFlag(flag)));
        }
    }

    static string NameOf(BsonDocument document)
    {
        return document.TryGetValue(NAME, out var name) && name is BsonString text ? text.Value : "";
    }

    static string DescribeFlag(BsonValue? flag)
    {
        return flag == null ? $"{IS_ACTIVE} not stored" : $"{IS_ACTIVE} {flag.ToString()?.ToLowerInvariant()}";
    }
}
