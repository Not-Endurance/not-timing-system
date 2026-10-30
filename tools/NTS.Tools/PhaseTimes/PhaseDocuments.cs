using MongoDB.Bson;

namespace NTS.Tools.PhaseTimes;

/// <summary>
/// A Participation document of before ADR-0005 turned into one of after it: the flat Arrive, Present and Represent times of
/// each Phase become accepted time events of the manual method, and the Representation request has its new name. What the
/// document holds besides stays as it is.
/// </summary>
internal static class PhaseDocuments
{
    const string PHASES = "Phases";
    const string EVENTS = "Events";
    const string OLD_FLAG = "IsReinspectionRequested";
    const string FLAG = "IsRepresentRequested";
    static readonly (string Field, string Kind, bool IsRepresent)[] TIMES =
    [
        ("ArriveTime", "Arrived", false),
        ("PresentTime", "Presented", false),
        ("RepresentTime", "Presented", true),
    ];

    /// <summary>
    /// The document in the new shape and the number of events made for it, or null when it needs nothing, or when a flat
    /// time is something other than a date, which is added to <paramref name="unreadable"/> and not guessed at.
    /// </summary>
    public static (BsonDocument Document, int Events)? Migrate(
        BsonDocument participation,
        List<UnreadableTime> unreadable
    )
    {
        if (!participation.TryGetValue(PHASES, out var phases) || phases is not BsonArray phaseArray)
        {
            return null;
        }

        var migrated = participation.DeepClone().AsBsonDocument;
        var events = 0;
        var changed = false;
        var readable = true;
        var migratedPhases = migrated[PHASES].AsBsonArray;
        for (var index = 0; index < phaseArray.Count; index++)
        {
            if (migratedPhases[index] is not BsonDocument phase)
            {
                continue;
            }

            foreach (var (field, _, _) in TIMES)
            {
                if (phase.TryGetValue(field, out var value) && !value.IsBsonNull && !value.IsBsonDateTime)
                {
                    unreadable.Add(
                        new UnreadableTime(participation["_id"], $"{PHASES}[{index}].{field}", value.BsonType)
                    );
                    readable = false;
                }
            }

            var made = MigratePhase(phase);
            changed |= made.Changed;
            events += made.Events;
        }

        return readable && changed ? (migrated, events) : null;
    }

    static (bool Changed, int Events) MigratePhase(BsonDocument phase)
    {
        var changed = TIMES.Any(x => phase.Contains(x.Field)) || phase.Contains(OLD_FLAG);
        var made = new BsonArray();
        var hasEvents = phase.TryGetValue(EVENTS, out var existing) && existing is BsonArray { Count: > 0 };
        if (!hasEvents)
        {
            foreach (var (field, kind, isRepresent) in TIMES)
            {
                if (phase.TryGetValue(field, out var time) && time.IsBsonDateTime)
                {
                    made.Add(Event(kind, isRepresent, time));
                }
            }
        }

        foreach (var (field, _, _) in TIMES)
        {
            phase.Remove(field);
        }

        if (phase.TryGetValue(OLD_FLAG, out var requested))
        {
            phase.Remove(OLD_FLAG);
            if (requested.IsBoolean && requested.AsBoolean)
            {
                phase[FLAG] = true;
            }
        }

        if (made.Count > 0)
        {
            phase[EVENTS] = made;
        }

        return (changed, made.Count);
    }

    /// <summary>What the storage of <c>TimeEventModel</c> writes for an accepted event of the manual method with no actor.</summary>
    static BsonDocument Event(string kind, bool isRepresent, BsonValue time)
    {
        var made = new BsonDocument
        {
            ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["Kind"] = kind,
            ["Time"] = time,
        };
        if (isRepresent)
        {
            made["IsRepresent"] = true;
        }

        made["Outcome"] = "Accepted";
        made["Method"] = "Manual";
        return made;
    }
}
