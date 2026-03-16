using Microsoft.Extensions.DependencyInjection;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Localization;
using Not.Notify;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Tests.Domain;

public class UpcomingEventValidationTests
{
    static UpcomingEventValidationTests()
    {
        var services = new ServiceCollection();
        services.AddDummyLocalizer();
        services.AddSingleton<INotifier, Notifier>();
        _ = new ServiceLocator(services.BuildServiceProvider());
    }

    [Fact]
    public void AddCompetition_WhenNameExistsIgnoringCaseAndWhitespace_Throws()
    {
        var existing = CreateCompetition(11, "Qualifier");
        var @event = Event(competitions: new[] { existing });

        var duplicate = CreateCompetition(12, " qualifier ");

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Add(duplicate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition.Name), ex.Property);
    }

    [Fact]
    public void UpdateCompetition_WhenNameConflicts_Throws()
    {
        var first = CreateCompetition(11, "Qualifier");
        var second = CreateCompetition(12, "Final");
        var @event = Event(competitions: new[] { first, second });

        var conflictingUpdate = CreateCompetition(second.Id, "QUALIFIER");

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Update(conflictingUpdate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition.Name), ex.Property);
    }

    [Fact]
    public void AddLoop_WhenDistanceExists_Throws()
    {
        var existing = CreateLoop(21, 20);
        var @event = Event(loops: new[] { existing });

        var duplicate = CreateLoop(22, 20);

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Add(duplicate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop.Distance), ex.Property);
    }

    [Fact]
    public void UpdateLoop_WhenDistanceConflicts_Throws()
    {
        var first = CreateLoop(21, 20);
        var second = CreateLoop(22, 30);
        var @event = Event(loops: new[] { first, second });

        var conflictingUpdate = CreateLoop(second.Id, 20);

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Update(conflictingUpdate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop.Distance), ex.Property);
    }

    [Fact]
    public void AddCombination_WhenNumberExists_Throws()
    {
        var first = CreateCombination(31, 1001, CreateAthlete(501, "A"), CreateHorse(601, "H1"));
        var @event = Event(combinations: new[] { first });

        var duplicate = CreateCombination(32, first.Number, CreateAthlete(502, "B"), CreateHorse(602, "H2"));

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Add(duplicate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination.Number), ex.Property);
    }

    [Fact]
    public void AddCombination_WhenAthleteAlreadyAssigned_Throws()
    {
        var athlete = CreateAthlete(501, "A");
        var first = CreateCombination(31, 1001, athlete, CreateHorse(601, "H1"));
        var @event = Event(combinations: new[] { first });

        var duplicateAthlete = CreateCombination(32, 1002, athlete, CreateHorse(602, "H2"));

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Add(duplicateAthlete));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination.Athlete), ex.Property);
    }

    [Fact]
    public void UpdateCombination_WhenHorseAlreadyAssigned_Throws()
    {
        var first = CreateCombination(31, 1001, CreateAthlete(501, "A"), CreateHorse(601, "H1"));
        var second = CreateCombination(32, 1002, CreateAthlete(502, "B"), CreateHorse(602, "H2"));
        var @event = Event(combinations: new[] { first, second });

        var conflictingUpdate = CreateCombination(second.Id, second.Number, second.Athlete, first.Horse);

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Update(conflictingUpdate));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination.Horse), ex.Property);
    }

    [Fact]
    public void AddCombination_WhenAthleteHorsePairExists_ThrowsAthleteValidation()
    {
        var athlete = CreateAthlete(501, "A");
        var horse = CreateHorse(601, "H1");
        var first = CreateCombination(31, 1001, athlete, horse);
        var @event = Event(combinations: new[] { first });

        var duplicatePair = CreateCombination(32, 1002, athlete, horse);

        var ex = Assert.Throws<DomainPropertyException>(() => @event.Add(duplicatePair));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination.Athlete), ex.Property);
        Assert.Contains(nameof(NTS.Localization.NtsStrings.Combination_athlete__already_exists), ex.Message);
    }

    [Fact]
    public void Constructor_WhenExistingCollectionsContainDuplicates_Throws()
    {
        var first = CreateLoop(21, 20);
        var second = CreateLoop(22, 20);

        var ex = Assert.Throws<DomainPropertyException>(() => Event(loops: new[] { first, second }));

        Assert.Equal(nameof(NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop.Distance), ex.Property);
    }

    static UpcomingEvent Event(
        IEnumerable<Competition>? competitions = null,
        IEnumerable<Loop>? loops = null,
        IEnumerable<Combination>? combinations = null
    )
    {
        return new UpcomingEvent(
            name: "Event",
            place: "Place",
            country: Country(),
            showFeiId: null,
            feiId: null,
            feiEventCode: null,
            competitions: competitions ?? Array.Empty<Competition>(),
            officials: Array.Empty<Official>(),
            loops: loops ?? Array.Empty<Loop>(),
            combinations: combinations ?? Array.Empty<Combination>(),
            id: 1
        );
    }

    static NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition CreateCompetition(int id, string name)
    {
        return new NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition(
            name: name,
            type: CompetitionType.Qualification,
            ruleset: CompetitionRuleset.Regional,
            start: DateTimeOffset.UtcNow.AddDays(1),
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            feiId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: Array.Empty<Phase>(),
            participations: Array.Empty<Participation>(),
            id: id
        );
    }

    static NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop CreateLoop(int id, double distance)
    {
        return new NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop(distance, id);
    }

    static NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination CreateCombination(
        int id,
        int number,
        Athlete athlete,
        Horse horse
    )
    {
        return new NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination(number, athlete, horse, id);
    }

    static Athlete CreateAthlete(int id, string name)
    {
        return new Athlete(new Person(new[] { name, "Test" }), feiId: null, country: Country(), club: null, id: id);
    }

    static Horse CreateHorse(int id, string name)
    {
        return new Horse(name, feiId: null, id: id);
    }

    static Country Country()
    {
        return new Country(id: 100, name: "Bulgaria", isoCode: "BG", nfCode: "BUL", locale: "bg-BG");
    }
}
