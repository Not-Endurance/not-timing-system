namespace NTS.Tools.IdentityMigration;

/// <summary>What the Guid of an id is scoped to (#637, ADR-0009).</summary>
public enum IdScope
{
    /// <summary>Nothing: the integer is unique in its space (an Athlete, a Horse, a Club, a Country, a User, an Event).</summary>
    None = 0,

    /// <summary>The document that holds the id: a thing embedded in an aggregate, whose integers were unique in that aggregate at most.</summary>
    Document = 1,

    /// <summary>The Event the document names in <c>EventId</c>: the documents of an Event, and the copies of its Setup that they share.</summary>
    Event = 2,
}

/// <summary>What becomes of the value at an id path (#637).</summary>
public enum IdRole
{
    /// <summary>An integer that becomes the Guid of its space, its scope and the integer.</summary>
    Converted = 1,

    /// <summary>An Athlete or a Horse: the Guid is the identity's, the registry row's (ADR-0010), whatever the integer was.</summary>
    Identity = 2,

    /// <summary>A Guid from the day it was made. It is left as it is, and an integer there is a defect that stops the run.</summary>
    Born = 3,
}

/// <summary>One id of one collection: where it is in the documents, and which Guid it becomes (#637).</summary>
public sealed class IdPath
{
    public required string Collection { get; init; }

    /// <summary>The element that holds the id, as the elements are named from the document down, without the array indexes: <c>Competitions.Phases._id</c>.</summary>
    public required string Path { get; init; }

    /// <summary>The kind of thing the id is of. Copies of one thing share one, so that they share a Guid.</summary>
    public required string Space { get; init; }

    public IdScope Scope { get; init; }
    public IdRole Role { get; init; } = IdRole.Converted;

    /// <summary>The collection whose documents this id refers to, for an id that is a reference and not the id of a thing.</summary>
    public string? Target { get; init; }
}

/// <summary>
/// The table of ids (#637, ADR-0009): every id and every reference of every collection that the cutover converts, embedded ones
/// included, with the id space it is in and what it is scoped to. Copies of one thing share a space (an Athlete, a Horse, a Club, a
/// Country, a User, an Event, a Combination); what is embedded in one aggregate is scoped to it, because its integers were unique in
/// the process that made it and not beyond. A Core Phase, Official, Operator and Participation were made new when the Event started,
/// so they are spaces of their own and not the Setup's. A test over the types of the models fails when an id is not here.
/// </summary>
public static class IdPaths
{
    const string CONFIGURE_EVENTS = "configure_events";
    const string COMBINATION = "Combination";
    const string ID = "_id";
    static readonly List<IdPath> Rows = [.. Build()];
    static readonly string[] Names = [.. Rows.Select(x => x.Collection).Distinct()];
    static readonly Dictionary<(string Collection, string Path), IdPath> ByPath = Rows.ToDictionary(x =>
        (x.Collection, x.Path)
    );
    static readonly HashSet<(string Collection, string Path)> RetiredMembers =
    [
        ("athletes", "User"),
        ("configure_events", "Competitions.Participations.Combination.Athlete.User"),
        ("configure_events", "Combinations.Athlete.User"),
        ("event_participations", "Combination.Athlete.User"),
    ];
    public const string ATHLETE = "Athlete";
    public const string HORSE = "Horse";
    public const string CLUB = "Club";
    public const string COUNTRY = "Country";
    public const string USER = "User";
    public const string EVENT = "Event";
    public const string ATHLETES = "athletes";
    public const string HORSES = "horses";
    public const string USERS = "users";
    public const string EVENT_INFORMATIONS = "event_informations";
    public const string PARTICIPATIONS = "event_participations";

    public static IdPath? Find(string collection, string path)
    {
        return ByPath.GetValueOrDefault((collection, path));
    }

    /// <summary>
    /// Whether the member at the path is one that nothing reads any more, so that what is inside it is left as it is. Older documents
    /// still store the <c>User</c> an Athlete had (retired by #635); its ids are not converted, and are not looked for.
    /// </summary>
    public static bool IsRetired(string collection, string path)
    {
        return RetiredMembers.Contains((collection, path));
    }

    public static IReadOnlyList<IdPath> All => Rows;

    /// <summary>The collections the table covers.</summary>
    public static IReadOnlyList<string> Collections => Names;

    static IEnumerable<IdPath> Build()
    {
        // the registry and the reference data, and the accounts
        yield return Row(ATHLETES, ID, ATHLETE, role: IdRole.Identity);
        foreach (var row in Person(ATHLETES, ""))
        {
            yield return row;
        }

        yield return Row(HORSES, ID, HORSE, role: IdRole.Identity);
        yield return Row("clubs", ID, CLUB);
        yield return Row("countries", ID, COUNTRY);
        yield return Row(USERS, ID, USER);

        // a Setup: the Event it will be, and everything embedded in it, scoped to it
        yield return Row(CONFIGURE_EVENTS, ID, EVENT);
        yield return Row(CONFIGURE_EVENTS, "Country._id", COUNTRY);
        yield return Row(CONFIGURE_EVENTS, "Competitions._id", "Competition", IdScope.Document);
        yield return Row(CONFIGURE_EVENTS, "Competitions.Phases._id", "SetupPhase", IdScope.Document);
        yield return Row(CONFIGURE_EVENTS, "Competitions.Phases.Loop._id", "Loop", IdScope.Document);
        yield return Row(CONFIGURE_EVENTS, "Competitions.Participations._id", "SetupParticipation", IdScope.Document);
        foreach (var row in Combination(CONFIGURE_EVENTS, "Competitions.Participations.Combination", IdScope.Document))
        {
            yield return row;
        }

        yield return Row(CONFIGURE_EVENTS, "Officials._id", "SetupOfficial", IdScope.Document);
        yield return Row(CONFIGURE_EVENTS, "Officials.User._id", USER, target: USERS);
        yield return Row(CONFIGURE_EVENTS, "Operators._id", "SetupOperator", IdScope.Document);
        yield return Row(CONFIGURE_EVENTS, "Operators.User._id", USER, target: USERS);
        yield return Row(CONFIGURE_EVENTS, "Loops._id", "Loop", IdScope.Document);
        foreach (var row in Combination(CONFIGURE_EVENTS, "Combinations", IdScope.Document))
        {
            yield return row;
        }

        // an Event that has started, and what it keeps: each document names the Event, and the copies it shares with the Setup are the Event's
        yield return Row(EVENT_INFORMATIONS, ID, EVENT);
        yield return Row(EVENT_INFORMATIONS, "Country._id", COUNTRY);
        yield return Row("event_officials", ID, "Official", IdScope.Event);
        yield return Row("event_officials", "EventId", EVENT, target: EVENT_INFORMATIONS);
        yield return Row("event_officials", "UserId", USER, target: USERS);
        yield return Row("event_operators", ID, "Operator", IdScope.Event);
        yield return Row("event_operators", "EventId", EVENT, target: EVENT_INFORMATIONS);
        yield return Row("event_operators", "UserId", USER, target: USERS);
        yield return Row(PARTICIPATIONS, ID, "Participation", IdScope.Event);
        yield return Row(PARTICIPATIONS, "EventId", EVENT, target: EVENT_INFORMATIONS);
        foreach (var row in Combination(PARTICIPATIONS, "Combination", IdScope.Event))
        {
            yield return row;
        }

        yield return Row(PARTICIPATIONS, "Phases._id", "Phase", IdScope.Document);
        yield return Row(PARTICIPATIONS, "Phases.Events._id", "TimeEvent", role: IdRole.Born);
        yield return Row(PARTICIPATIONS, "Phases.Events.ActorId", USER, role: IdRole.Born);
        yield return Row("event_rankings", ID, "Ranking", IdScope.Event);
        yield return Row("event_rankings", "EventId", EVENT, target: EVENT_INFORMATIONS);
        yield return Row(
            "event_rankings",
            "Entries.ParticipationId",
            "Participation",
            IdScope.Event,
            target: PARTICIPATIONS
        );
        yield return Row("event_handouts", ID, "Handout", IdScope.Event);
        yield return Row("event_handouts", "EventId", EVENT, target: EVENT_INFORMATIONS);
        yield return Row("event_handouts", "ParticipationId", "Participation", IdScope.Event, target: PARTICIPATIONS);

        // what a person keeps per Event: its own _id is an object id, and stays
        yield return Row("event_user_sessions", "Id", "UserSession", IdScope.Event);
        yield return Row("event_user_sessions", "EventId", EVENT, target: EVENT_INFORMATIONS);
        yield return Row("event_user_sessions", "State.SnapshotHistory.GroupId", "SnapshotGroup", role: IdRole.Born);
    }

    /// <summary>A Combination and the Athlete and the Horse in it.</summary>
    static IEnumerable<IdPath> Combination(string collection, string path, IdScope scope)
    {
        yield return Row(collection, $"{path}.{ID}", COMBINATION, scope);
        yield return Row(collection, $"{path}.Athlete.{ID}", ATHLETE, role: IdRole.Identity);
        foreach (var row in Person(collection, $"{path}.Athlete."))
        {
            yield return row;
        }

        yield return Row(collection, $"{path}.Horse.{ID}", HORSE, role: IdRole.Identity);
    }

    /// <summary>The Country and the Club an Athlete carries.</summary>
    static IEnumerable<IdPath> Person(string collection, string prefix)
    {
        yield return Row(collection, $"{prefix}Country.{ID}", COUNTRY);
        yield return Row(collection, $"{prefix}Club.{ID}", CLUB);
    }

    static IdPath Row(
        string collection,
        string path,
        string space,
        IdScope scope = IdScope.None,
        IdRole role = IdRole.Converted,
        string? target = null
    )
    {
        return new IdPath
        {
            Collection = collection,
            Path = path,
            Space = space,
            Scope = scope,
            Role = role,
            Target = target,
        };
    }
}
