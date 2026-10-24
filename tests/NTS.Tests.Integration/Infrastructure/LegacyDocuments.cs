using MongoDB.Bson;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Documents as they were stored before ADR-0006: a copy of the Participation inside every Ranking entry and every
/// Handout, and the values the domain derives stored beside the times of each Phase and as a Total.
/// </summary>
internal static class LegacyDocuments
{
    /// <summary>The nine values of a Phase that the domain derives and that were stored beside its times.</summary>
    public static readonly string[] DERIVED_PHASE_FIELDS =
    [
        "RequiredInspectionTime",
        "OutTime",
        "LoopInterval",
        "PhaseInterval",
        "RecoveryInterval",
        "AverageLoopSpeed",
        "AveragePhaseSpeed",
        "AverageSpeed",
        "IsComplete",
    ];

    public static BsonBinaryData Uuid(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    /// <summary>
    /// Adds the derived values the code before wrote to a Participation document, and a Total. They are wrong on
    /// purpose, so that whatever reads one shows the difference from what the domain computes.
    /// </summary>
    public static void AddDerivedValues(BsonDocument participation)
    {
        var wrongTime = new BsonDateTime(new DateTime(2027, 4, 28, 8, 0, 0, DateTimeKind.Utc));
        foreach (var phase in participation["Phases"].AsBsonArray.Select(x => x.AsBsonDocument))
        {
            phase["RequiredInspectionTime"] = wrongTime;
            phase["OutTime"] = wrongTime;
            phase["LoopInterval"] = "09:09:09";
            phase["PhaseInterval"] = "09:09:09";
            phase["RecoveryInterval"] = "09:09:09";
            phase["AverageLoopSpeed"] = 99.9;
            phase["AveragePhaseSpeed"] = 99.9;
            phase["AverageSpeed"] = 99.9;
            phase["IsComplete"] = false;
        }

        participation["Total"] = new BsonDocument
        {
            ["LastArriveTime"] = wrongTime,
            ["AverageSpeed"] = 99.9,
            ["Interval"] = "09:09:09",
            ["RideInterval"] = "09:09:09",
            ["RecoveryInterval"] = "09:09:09",
            ["RecoveryIntervalWithoutFinal"] = "09:09:09",
        };
    }

    /// <summary>The document without what the migration removes: the Total and the derived values of each Phase.</summary>
    public static BsonDocument WithoutDerivedValues(BsonDocument participation)
    {
        var stripped = participation.DeepClone().AsBsonDocument;
        stripped.Remove("Total");
        foreach (var phase in stripped["Phases"].AsBsonArray.Select(x => x.AsBsonDocument))
        {
            foreach (var field in DERIVED_PHASE_FIELDS)
            {
                phase.Remove(field);
            }
        }

        return stripped;
    }

    /// <summary>A small Participation document with one finished Phase, in the shape of before.</summary>
    public static BsonDocument Participation(BsonValue id, BsonValue eventId, int number)
    {
        var start = new BsonDateTime(new DateTime(2026, 4, 28, 8, 0, 0, DateTimeKind.Utc));
        var participation = new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["EventId"] = eventId,
            ["Category"] = "Senior",
            ["Competition"] = new BsonDocument { ["Name"] = "CEI 1*", ["Ruleset"] = "FEI" },
            ["Combination"] = new BsonDocument
            {
                ["_id"] = number + 1000,
                ["Number"] = number,
                ["Distance"] = "40",
            },
            ["Phases"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = number + 2000,
                    ["Gate"] = "GATE1/40",
                    ["Length"] = 40.0,
                    ["MaxRecovery"] = 40,
                    ["Ruleset"] = "FEI",
                    ["IsFinal"] = true,
                    ["StartTime"] = start,
                    ["ArriveTime"] = new BsonDateTime(start.ToUniversalTime().AddHours(2)),
                    ["PresentTime"] = new BsonDateTime(start.ToUniversalTime().AddHours(2).AddMinutes(10)),
                },
            },
            ["IsDeleted"] = false,
            ["DeletedVersion"] = BsonNull.Value,
        };
        AddDerivedValues(participation);
        return participation;
    }

    /// <summary>A Ranking document with an embedded copy of a Participation in each entry.</summary>
    public static BsonDocument Ranking(BsonValue id, BsonValue eventId, string name, params BsonDocument[] entries)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["EventId"] = eventId,
            ["Name"] = name,
            ["Ruleset"] = "FEI",
            ["Category"] = "Senior",
            ["FeiEventId"] = BsonNull.Value,
            ["Entries"] = new BsonArray(entries),
            ["IsDeleted"] = false,
            ["DeletedVersion"] = BsonNull.Value,
        };
    }

    /// <summary>An entry of a Ranking of before: the copy, the stored rank and the mark, written even when empty.</summary>
    public static BsonDocument RankingEntry(BsonDocument copy, int? rank = null, bool isNotRanked = false)
    {
        return new BsonDocument
        {
            ["Participation"] = copy.DeepClone(),
            ["Rank"] = rank.HasValue ? new BsonInt32(rank.Value) : BsonNull.Value,
            ["IsNotRanked"] = isNotRanked,
        };
    }

    /// <summary>A Handout document of before: a copy of one Participation.</summary>
    public static BsonDocument Handout(BsonValue id, BsonValue eventId, BsonDocument copy)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["EventId"] = eventId,
            ["Participation"] = copy.DeepClone(),
            ["IsDeleted"] = false,
            ["DeletedVersion"] = BsonNull.Value,
        };
    }
}
