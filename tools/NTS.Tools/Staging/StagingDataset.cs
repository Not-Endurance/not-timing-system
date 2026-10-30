using System.Security.Cryptography;
using System.Text;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupClub = NTS.Domain.Setup.Aggregates.Club;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupOfficial = NTS.Domain.Setup.Aggregates.ConfigureEvents.Official;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;
using SetupUser = NTS.Domain.Setup.Aggregates.User;

namespace NTS.Tools.Staging;

/// <summary>A person of the Setup that an account stands for: its email, the id of the account and the role of an Official.</summary>
public sealed class SeededPerson
{
    public SeededPerson(string email, Guid accountId, OfficialRole role = OfficialRole.Steward)
    {
        Email = email;
        AccountId = accountId;
        Role = role;
    }

    public string Email { get; }
    public Guid AccountId { get; }
    public OfficialRole Role { get; }
}

/// <summary>
/// The Setup of the Event the staging seed makes (#607): a small competition of the kind the integration suite's Setup is,
/// made larger: three Phases over two loops, twelve combinations of seven kinds of riders, the FEI export configured whole,
/// and the Officials and Operators the person who runs the command names. The people and the horses are invented. Every id is
/// made from the name of the Event and of the thing, so that the same Event is made the same on every run and the seed can
/// tell what it made.
/// </summary>
public static class StagingDataset
{
    static readonly string[] RIDERS =
    [
        "Иван Петров|Ivan Petrov",
        "Мария Георгиева|Maria Georgieva",
        "Георги Димитров|Georgi Dimitrov",
        "Елена Стоянова|Elena Stoyanova",
        "Николай Тодоров|Nikolay Todorov",
        "Десислава Иванова|Desislava Ivanova",
        "Петър Николов|Petar Nikolov",
        "Виктория Ангелова|Viktoria Angelova",
        "Стефан Колев|Stefan Kolev",
        "Радина Маринова|Radina Marinova",
        "Мартин Христов|Martin Hristov",
        "Гергана Попова|Gergana Popova",
    ];
    static readonly string[] HORSES =
    [
        "Буря|Burya",
        "Зора|Zora",
        "Вихър|Vihar",
        "Мъгла|Mugla",
        "Орлик|Orlik",
        "Искра|Iskra",
        "Балкан|Balkan",
        "Росица|Rositsa",
        "Ветрец|Vetrets",
        "Зорница|Zornitsa",
        "Лъчезар|Luchezar",
        "Дъга|Duga",
    ];
    public const string TENANT_ID = "country-bg";
    public const int COMBINATIONS = 12;

    /// <summary>An id that is the same for the same key: the first sixteen bytes of its hash, marked as a name-based UUID.</summary>
    public static Guid IdOf(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public static string NameOf(string email)
    {
        var local = email.Split('@')[0];
        return string.Join(
            ' ',
            local.Split('.', '-', '_').Where(x => x.Length > 0).Select(x => char.ToUpperInvariant(x[0]) + x[1..])
        );
    }

    /// <param name="eventName">What the Event is called, and what its id is made from.</param>
    /// <param name="start">The instant the competition starts, which is the Start of its first Phase.</param>
    /// <param name="country">The country of the Tenant, which the Event and its riders are of.</param>
    public static SetupConfigureEvent Create(
        string eventName,
        DateTimeOffset start,
        Country country,
        string tenantId,
        Guid mainOperatorId,
        IReadOnlyList<SeededPerson> officials,
        IReadOnlyList<SeededPerson> operators
    )
    {
        var eventId = IdOf("event:" + eventName);
        Guid Id(string what)
        {
            return IdOf($"{eventId}:{what}");
        }

        var loop20 = new SetupLoop(20, Id("loop:20"));
        var loop10 = new SetupLoop(10, Id("loop:10"));
        var club = new SetupClub("Staging Riders Club", Id("club"));
        var combinations = Enumerable
            .Range(1, COMBINATIONS)
            .Select(number =>
            {
                var rider = RIDERS[number - 1].Split('|');
                var horse = HORSES[number - 1].Split('|');
                return new SetupCombination(
                    number,
                    new SetupAthlete(rider[0], rider[1], $"1001{number:0000}", country, club, Id($"athlete:{number}")),
                    new SetupHorse(horse[0], horse[1], $"103AA{number:00}", Id($"horse:{number}")),
                    Id($"combination:{number}")
                );
            })
            .ToList();
        var phases = new[]
        {
            new SetupPhase(loop20, recovery: 15, rest: 40, id: Id("phase:1")),
            new SetupPhase(loop20, recovery: 15, rest: 40, id: Id("phase:2"), isCompulsoryInspectionRequired: true),
            new SetupPhase(loop10, recovery: 20, rest: null, id: Id("phase:3")),
        };
        var participations = combinations
            .Select(x => new SetupParticipation(
                isNotRanked: false,
                combination: x,
                category: x.Number > COMBINATIONS - 3
                    ? ParticipationCategory.JuniorOrYoungAdult
                    : ParticipationCategory.Senior,
                startTimeOverride: null,
                maxSpeedOverride: null,
                minSpeedOverride: null,
                id: Id($"participation:{x.Number}")
            ))
            .ToList();
        var competition = new SetupCompetition(
            "Staging 50 km",
            CompetitionRuleset.FEI,
            start,
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            minSpeedRestriction: 12.0,
            maxSpeedRestriction: 25.0,
            feiEventId: "STAGING-EVENT",
            feiEventCode: "CEI1",
            feiCompetitionId: "STAGING-COMPETITION",
            feiRule: "Rule",
            feiScheduleNumber: "1",
            phases,
            participations,
            id: Id("competition")
        );
        var setupOfficials = officials
            .Select(x => new SetupOfficial(
                NameOf(x.Email),
                null,
                x.Role,
                Id($"official:{x.Email}"),
                new SetupUser(x.Email, NameOf(x.Email), [], x.AccountId)
            ))
            .ToList();
        var setupOperators = operators
            .Select(x => new SetupOperator(
                new SetupUser(x.Email, NameOf(x.Email), [], x.AccountId),
                Id($"operator:{x.Email}")
            ))
            .ToList();
        return new SetupConfigureEvent(
            eventName,
            "Sofia",
            country,
            "STAGING42",
            [competition],
            setupOfficials,
            [loop20, loop10],
            combinations,
            eventId,
            setupOperators,
            tenantId,
            mainOperatorId
        );
    }
}
