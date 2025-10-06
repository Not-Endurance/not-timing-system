using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Judge.Features.Core.Reset;

namespace NTS.Judge.Core.Behinds.Adapters;

public class StartlistBehind : ObservableBehind, IStartlistUpcoming, IStartlistHistory, ICoreState
{
    readonly IRepository<Participation> _participationRepository;
    StartList? _startlist;

    public StartlistBehind(IRepository<Participation> participationRepository)
    {
        _participationRepository = participationRepository;
    }

    public IReadOnlyList<StartlistEntry> Upcoming => _startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _startlist?.History ?? [];

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var participations = await _participationRepository.ReadAll();
        _startlist = new StartList(participations, EmitChange);

        return _startlist.Any();
    }
}
