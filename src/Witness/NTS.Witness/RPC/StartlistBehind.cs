using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Setup.Aggregates;
using Participation = NTS.Domain.Core.Aggregates.Participation;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;
using Combination = NTS.Domain.Core.Aggregates.Participations.Combination;
using Horse = NTS.Domain.Core.Aggregates.Participations.Horse;
using Athlete = NTS.Domain.Core.Aggregates.Participations.Athlete;
using NTS.Domain.Objects;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using Not.Notify;

namespace NTS.Witness.RPC;

public class StartlistBehind : ObservableBehind, IStartlistHistory, IStartlistUpcoming
{
    StartList? _startlist;
    Action _action = () => {
        NotifyHelper.Inform("A startlist has been created!");
    };
    List<Participation> _participations;

public StartlistBehind()
    {
        var country = new Country(1000,null,null,null,null);
        var person = new Person(["Todomir", "Stroinov"]);
        var athlete = new Athlete(99, person, Domain.Enums.AthleteCategory.Senior,country, null,"guz");
        var horse = new Horse(100, "Rozomir", null);
        var combination = new Combination(199, 1, athlete, horse, "40", null,  null);
        var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(20, 15, 40, NTS.Domain.Enums.CompetitionRuleset.Regional, false, null, DateTimeOffset.Now.AddMinutes(23));
        var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(20, 20, 40, NTS.Domain.Enums.CompetitionRuleset.Regional, true, null, null);
        var phases = new List<NTS.Domain.Core.Aggregates.Participations.Phase>() { phase1, phase2 };
        var phaseCollection = new PhaseCollection(phases);
        var competition = new Competition("Splehnik", Domain.Enums.CompetitionRuleset.Regional, Domain.Enums.CompetitionType.Qualification);
        var participation = new Participation(2001, competition,combination,phaseCollection,null);
        _participations = [participation];
    }

    public IReadOnlyList<StartlistEntry> Upcoming => _startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _startlist?.History ?? [];

    protected override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        _startlist = new StartList(_participations, _action);

        return Task.FromResult(_startlist.Any());
    }

}
