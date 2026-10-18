using System.Globalization;
using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.Shared;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// Finds the Athletes and the Horses in the documents that hold them (#636): the rows of Setup, the Combinations of a configure
/// event (its pool and the Participations of its Competitions), a Participation of a started Event, and the copies of a
/// Participation that a Ranking and a Handout of before ADR-0006 still embed. A person is whatever is stored under a field called
/// <c>Athlete</c> or <c>Horse</c>, wherever it is, so a shape that is nested otherwise is read as well. <see cref="All"/> is every
/// collection that holds one, in the order the report counts them.
/// </summary>
public static class IdentityCopies
{
    const string ATHLETE = "Athlete";
    const string HORSE = "Horse";
    const string PARTICIPATION = "Participation";
    const string PARTICIPATIONS_FIELD = "Participations";
    public const string ATHLETES = "athletes";
    public const string HORSES = "horses";
    public const string CONFIGURE_EVENTS = "configure_events";
    public const string PARTICIPATIONS = "event_participations";
    public const string RANKINGS = "event_rankings";
    public const string HANDOUTS = "event_handouts";
    public static readonly string[] All = [ATHLETES, HORSES, CONFIGURE_EVENTS, PARTICIPATIONS, RANKINGS, HANDOUTS];

    /// <summary>The text of an id as the reports tell it: a UUID as a Guid, anything else as it is.</summary>
    public static string Describe(BsonValue id)
    {
        return id is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            ? binary.ToGuid(GuidRepresentation.Standard).ToString()
            : id.ToString() ?? "";
    }

    public static IReadOnlyList<IdentityCopy> Read(string collection, BsonDocument document)
    {
        var origin = new Origin(collection, IdOf(document), EventOf(collection, document));
        var found = new List<IdentityCopy>();
        if (collection is ATHLETES or HORSES)
        {
            found.Add(
                Copy(
                    collection == ATHLETES ? IdentityKind.Athlete : IdentityKind.Horse,
                    document,
                    origin,
                    "",
                    null,
                    null
                )
            );
        }
        else
        {
            Walk(document, "", collection == PARTICIPATIONS ? PARTICIPATION : "", null, origin, found);
        }

        return found;
    }

    static void Walk(
        BsonValue value,
        string path,
        string field,
        string? participation,
        Origin origin,
        List<IdentityCopy> found
    )
    {
        if (value is BsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                Walk(array[i], $"{path}[{i}]", field, participation, origin, found);
            }

            return;
        }

        if (value is not BsonDocument document)
        {
            return;
        }

        var isParticipation = field is PARTICIPATION or PARTICIPATIONS_FIELD && document.Contains("_id");
        var current = isParticipation ? IdOf(document) : participation;
        var athlete = PersonAt(document, ATHLETE);
        var horse = PersonAt(document, HORSE);
        foreach (var element in document.Elements)
        {
            var child = path.Length == 0 ? element.Name : $"{path}.{element.Name}";
            if (element.Name == ATHLETE && athlete != null)
            {
                found.Add(Copy(IdentityKind.Athlete, athlete, origin, child, current, NameOf(horse)));
            }
            else if (element.Name == HORSE && horse != null)
            {
                found.Add(Copy(IdentityKind.Horse, horse, origin, child, current, NameOf(athlete)));
            }
            else
            {
                Walk(element.Value, child, element.Name, current, origin, found);
            }
        }
    }

    static IdentityCopy Copy(
        IdentityKind kind,
        BsonDocument person,
        Origin origin,
        string path,
        string? participation,
        string? partner
    )
    {
        return new IdentityCopy
        {
            Kind = kind,
            Collection = origin.Collection,
            DocumentId = origin.DocumentId,
            Path = path,
            IsSetupRow = path.Length == 0,
            LegacyId = LegacyIdOf(person),
            Name = Records.TextOf(person, "Name"),
            NameEnglish = Records.TextOf(person, "NameEnglish"),
            FeiId = FeiIdOf(person),
            Country = CountryOf(person),
            Club = ClubOf(person),
            EventId = origin.EventId,
            ParticipationId = participation,
            Partner = partner,
        };
    }

    static string IdOf(BsonDocument document)
    {
        return document.TryGetValue("_id", out var id) ? Describe(id) : "";
    }

    static string? EventOf(string collection, BsonDocument document)
    {
        if (collection == CONFIGURE_EVENTS)
        {
            return IdOf(document);
        }

        return document.TryGetValue("EventId", out var id) && !id.IsBsonNull ? Describe(id) : null;
    }

    static BsonDocument? PersonAt(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var person) ? person as BsonDocument : null;
    }

    static string? NameOf(BsonDocument? person)
    {
        return person == null ? null : Records.TextOf(person, "Name") ?? Records.TextOf(person, "NameEnglish");
    }

    /// <summary>
    /// The integer a copy was called before ids were Guids, as text. The id of an older document is under <c>_id</c> or <c>Id</c>;
    /// a Guid or an object id is not a legacy id, and neither is 0.
    /// </summary>
    static string? LegacyIdOf(BsonDocument person)
    {
        var id = person.GetValue("_id", person.GetValue("Id", BsonNull.Value));
        var text = id switch
        {
            BsonInt32 or BsonInt64 => id.ToString(),
            BsonDouble number when number.Value % 1 == 0 => number.Value.ToString("0", CultureInfo.InvariantCulture),
            BsonString => id.AsString,
            _ => null,
        };
        return IdentityLegacyIds.Of(text);
    }

    static string? FeiIdOf(BsonDocument person)
    {
        var stored = person.GetValue("FeiId", BsonNull.Value);
        var text = stored switch
        {
            BsonString => stored.AsString,
            BsonInt32 or BsonInt64 or BsonDouble => stored.ToString(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// The country as it is stored, by its name and its codes: Bulgaria is entered as BGR (the ISO code) and as BUL (the code of
    /// its federation), and the report shows which each copy has.
    /// </summary>
    static string? CountryOf(BsonDocument person)
    {
        if (!person.TryGetValue("Country", out var country))
        {
            return null;
        }

        if (country is BsonString typed)
        {
            return string.IsNullOrWhiteSpace(typed.Value) ? null : typed.Value.Trim();
        }

        if (country is not BsonDocument stored)
        {
            return null;
        }

        var name = Records.TextOf(stored, "Name");
        var codes = new[] { Records.TextOf(stored, "IsoCode"), Records.TextOf(stored, "NfCode") }
            .Where(x => x != null)
            .Distinct()
            .ToArray();
        return codes.Length == 0 ? name
            : name == null ? string.Join("/", codes)
            : $"{name} ({string.Join("/", codes)})";
    }

    static string? ClubOf(BsonDocument person)
    {
        return person.GetValue("Club", BsonNull.Value) switch
        {
            BsonDocument club => Records.TextOf(club, "Name"),
            BsonString name when !string.IsNullOrWhiteSpace(name.Value) => name.Value.Trim(),
            _ => null,
        };
    }

    sealed class Origin
    {
        public Origin(string collection, string documentId, string? eventId)
        {
            Collection = collection;
            DocumentId = documentId;
            EventId = eventId;
        }

        public string Collection { get; }
        public string DocumentId { get; }
        public string? EventId { get; }
    }
}
