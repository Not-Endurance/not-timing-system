using MongoDB.Bson;

namespace NTS.Tests.Unit.Temporary;

/// <summary>
/// Documents in the shapes the data has today, as the identity migration meets them (#636): a Setup row, a configure event with
/// its pool of Combinations and the Participations of its Competitions, a Participation of a started Event, and a Ranking or a
/// Handout of before ADR-0006 with a copy of a Participation inside. Ids are the integers of before, or 0 for none.
/// </summary>
internal static class IdentityMigrationDocuments
{
    public static BsonDocument Country(string name, string? isoCode = null, string? nfCode = null)
    {
        var country = new BsonDocument { ["_id"] = 1, ["Name"] = name };
        if (isoCode != null)
        {
            country["IsoCode"] = isoCode;
        }

        if (nfCode != null)
        {
            country["NfCode"] = nfCode;
        }

        return country;
    }

    /// <summary>An Athlete or a Horse as it is embedded or stored: its id is the integer it had before ids were Guids.</summary>
    public static BsonDocument Person(
        BsonValue id,
        string? name,
        string? nameEnglish = null,
        string? fei = null,
        BsonDocument? country = null,
        string? club = null
    )
    {
        var person = new BsonDocument { ["_id"] = id };
        if (name != null)
        {
            person["Name"] = name;
        }

        if (nameEnglish != null)
        {
            person["NameEnglish"] = nameEnglish;
        }

        if (fei != null)
        {
            person["FeiId"] = fei;
        }

        if (country != null)
        {
            person["Country"] = country;
        }

        if (club != null)
        {
            person["Club"] = new BsonDocument { ["_id"] = 7, ["Name"] = club };
        }

        return person;
    }

    public static BsonDocument Combination(int number, BsonDocument athlete, BsonDocument horse)
    {
        return new BsonDocument
        {
            ["_id"] = 1000 + number,
            ["Number"] = number,
            ["Athlete"] = athlete.DeepClone(),
            ["Horse"] = horse.DeepClone(),
        };
    }

    /// <summary>The Setup of an Event: a pool of Combinations, and Competitions that hold Participations of Combinations.</summary>
    public static BsonDocument Setup(
        BsonValue id,
        string name,
        IEnumerable<BsonDocument> pool,
        params (string Competition, BsonValue ParticipationId, BsonDocument Combination)[] participations
    )
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["Name"] = name,
            ["Combinations"] = new BsonArray(pool),
            ["Competitions"] = new BsonArray(
                participations
                    .GroupBy(x => x.Competition)
                    .Select(x => new BsonDocument
                    {
                        ["_id"] = 500,
                        ["Name"] = x.Key,
                        ["Participations"] = new BsonArray(
                            x.Select(y => new BsonDocument
                            {
                                ["_id"] = y.ParticipationId,
                                ["Combination"] = y.Combination.DeepClone(),
                            })
                        ),
                    })
            ),
        };
    }

    /// <summary>A Participation of an Event that has started.</summary>
    public static BsonDocument Participation(BsonValue id, BsonValue eventId, BsonDocument combination)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["EventId"] = eventId,
            ["Competition"] = new BsonDocument { ["Name"] = "CEI 1*" },
            ["Combination"] = combination.DeepClone(),
        };
    }

    /// <summary>A Ranking of before ADR-0006: every entry holds a copy of its Participation.</summary>
    public static BsonDocument Ranking(BsonValue id, BsonValue eventId, params BsonDocument[] copies)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["EventId"] = eventId,
            ["Name"] = "CEI 1*",
            ["Entries"] = new BsonArray(
                copies.Select(x => new BsonDocument { ["Participation"] = x.DeepClone(), ["IsNotRanked"] = false })
            ),
        };
    }

    /// <summary>A Ranking of the shape since: its entries name the Participation by id and hold no person.</summary>
    public static BsonDocument ReferencingRanking(BsonValue id, BsonValue eventId, params BsonValue[] participations)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["EventId"] = eventId,
            ["Name"] = "CEI 1*",
            ["Entries"] = new BsonArray(participations.Select(x => new BsonDocument { ["ParticipationId"] = x })),
        };
    }

    public static BsonDocument Handout(BsonValue id, BsonValue eventId, BsonDocument copy)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["EventId"] = eventId,
            ["Participation"] = copy.DeepClone(),
        };
    }
}
