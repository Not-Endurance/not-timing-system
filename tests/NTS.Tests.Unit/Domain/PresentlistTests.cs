using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class PresentlistTests
{
    [Fact]
    public void Entries_include_present_until_presentation_time_is_recorded()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-10);
        var pending = CreateParticipation(
            1,
            [CreatePhase(start: arrive.AddHours(-1), arrive: arrive, rest: null, isFinal: true)]
        );
        var completed = CreateParticipation(
            2,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: arrive.AddMinutes(5),
                    rest: null,
                    isFinal: true
                ),
            ]
        );

        var presentlist = new Presentlist([pending, completed]);

        var entry = Assert.Single(presentlist.Entries);
        Assert.Equal(1, entry.Number);
        Assert.Equal(PresentlistEntryType.Present, entry.Type);
        Assert.Equal(arrive.AddMinutes(40), entry.Time.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_include_representation_until_representation_time_is_recorded()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-20);
        var present = arrive.AddMinutes(5);
        var pending = CreateParticipation(
            1,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    isRepresentationRequested: true,
                    rest: null,
                    isFinal: true
                ),
            ]
        );
        var completed = CreateParticipation(
            2,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    represent: present.AddMinutes(5),
                    isRepresentationRequested: true,
                    rest: null,
                    isFinal: true
                ),
            ]
        );

        var presentlist = new Presentlist([pending, completed]);

        var entry = Assert.Single(presentlist.Entries);
        Assert.Equal(1, entry.Number);
        Assert.Equal(PresentlistEntryType.Represent, entry.Type);
        Assert.Equal(arrive.AddMinutes(40), entry.Time.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_include_required_and_compulsory_inspections_after_phase_completion()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-20);
        var present = arrive.AddMinutes(5);
        var required = CreateParticipation(
            1,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    isRequiredInspectionRequested: true
                ),
            ]
        );
        var compulsory = CreateParticipation(
            2,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    isRequiredInspectionRequested: true,
                    isRequiredInspectionCompulsory: true
                ),
            ]
        );

        var presentlist = new Presentlist([required, compulsory]);

        Assert.Equal([PresentlistEntryType.CRI, PresentlistEntryType.RI], presentlist.Entries.Select(x => x.Type));
        Assert.All(presentlist.Entries, x => Assert.Equal(present.AddMinutes(25), x.Time.ToDateTimeOffset()));
    }

    [Fact]
    public void Entries_order_by_time_and_keep_soonest_entry_per_participation()
    {
        var now = DateTimeOffset.Now;
        var duplicate = CreateParticipation(
            1,
            [
                CreatePhase(start: now.AddHours(-1), arrive: now, rest: null, isFinal: true),
                CreatePhase(
                    start: now.AddHours(-2),
                    arrive: now.AddMinutes(-10),
                    present: now.AddMinutes(-5),
                    isRequiredInspectionRequested: true
                ),
            ]
        );
        var earlier = CreateParticipation(
            2,
            [CreatePhase(start: now.AddHours(-1), arrive: now.AddMinutes(-30), rest: null, isFinal: true)]
        );

        var presentlist = new Presentlist([duplicate, earlier]);

        Assert.Equal([2, 1], presentlist.Entries.Select(x => x.Number));
        Assert.Equal([PresentlistEntryType.Present, PresentlistEntryType.RI], presentlist.Entries.Select(x => x.Type));
    }

    [Fact]
    public void Entries_exclude_eliminated_participations()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-10);
        var active = CreateParticipation(
            1,
            [CreatePhase(start: arrive.AddHours(-1), arrive: arrive, rest: null, isFinal: true)]
        );
        var eliminated = CreateParticipation(
            2,
            [CreatePhase(start: arrive.AddHours(-1), arrive: arrive, rest: null, isFinal: true)],
            new Withdrawn()
        );

        var entry = Assert.Single(new Presentlist([active, eliminated]).Entries);

        Assert.Equal(1, entry.Number);
    }

    [Fact]
    public void ToggleInspection_raises_inspection_required_when_state_changes()
    {
        var participation = CreateParticipation(1, [CreatePhase()]);

        participation.ToggleInspection(true);

        Assert.Contains(participation.DequeueDomainEvents(), x => x is InspectionRequired);

        participation.ToggleInspection(true);

        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is InspectionRequired);
    }

    [Fact]
    public void ToggleInspection_rejects_required_inspection_while_representation_time_is_pending()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-20);
        var present = arrive.AddMinutes(5);
        var participation = CreateParticipation(
            1,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    isRepresentationRequested: true
                ),
            ]
        );

        Assert.Throws<DomainException>(() => participation.ToggleInspection(true));

        Assert.False(participation.Phases.Current.IsRequiredInspectionRequested);
        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is InspectionRequired);
    }

    [Fact]
    public void ToggleInspection_allows_required_inspection_after_representation_time_is_recorded()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-20);
        var present = arrive.AddMinutes(5);
        var participation = CreateParticipation(
            1,
            [
                CreatePhase(
                    start: arrive.AddHours(-1),
                    arrive: arrive,
                    present: present,
                    represent: present.AddMinutes(5),
                    isRepresentationRequested: true
                ),
            ]
        );

        participation.ToggleInspection(true);

        Assert.True(participation.Phases.Current.IsRequiredInspectionRequested);
        Assert.Contains(participation.DequeueDomainEvents(), x => x is InspectionRequired);
    }

    [Fact]
    public void ToggleRepresentation_raises_representation_required_when_state_changes()
    {
        var arrive = DateTimeOffset.Now.AddMinutes(-20);
        var present = arrive.AddMinutes(5);
        var participation = CreateParticipation(
            1,
            [CreatePhase(start: arrive.AddHours(-1), arrive: arrive, present: present)]
        );

        participation.ToggleRepresentation(true);

        Assert.Contains(participation.DequeueDomainEvents(), x => x is RepresentationRequired);

        participation.ToggleRepresentation(true);

        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is RepresentationRequired);
    }

    static Participation CreateParticipation(int number, IEnumerable<Phase> phases, Eliminated? eliminated = null)
    {
        var phaseList = phases.ToList();
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var totalDistance = phaseList.Sum(x => x.Length);
        var combination = new Combination(number, athlete, horse, null, $"{totalDistance:0.##}", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phaseList),
            eliminated,
            eventId: 1
        );
    }

    static Phase CreatePhase(
        DateTimeOffset? start = null,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        DateTimeOffset? represent = null,
        bool isRepresentationRequested = false,
        bool isRequiredInspectionRequested = false,
        bool isRequiredInspectionCompulsory = false,
        int? rest = 40,
        bool isFinal = false
    )
    {
        return new Phase(
            "",
            20,
            40,
            rest,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            Timestamp.Create(represent),
            isRepresentationRequested,
            isRequiredInspectionRequested,
            isRequiredInspectionCompulsory
        );
    }
}
