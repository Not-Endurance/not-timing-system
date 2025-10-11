using Not.Application.Behinds.Adapters;
using Not.Notify;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Objects.Startlists;

using Participation = NTS.Domain.Core.Aggregates.Participation;

namespace NTS.Witness.RPC;

public class StartlistBehind : ObservableBehind, IStartlistHistory, IStartlistUpcoming
{
    StartList? _startlist;
    Action _action = () =>
    {
        NotifyHelper.Inform("A startlist has been created!");
    };

    public StartlistBehind()
    {
        Participations = DummyData.CreateParticipationsForStartlist();
    }

    List<Participation> Participations { get; set; } = default!;

    public IReadOnlyList<StartlistEntry> Upcoming => _startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _startlist?.History ?? [];

    protected override Task<bool> PerformInitialization(IEnumerable<object> arguments)
    {
        _startlist = new StartList(Participations, _action);

        return Task.FromResult(_startlist.Any());
    }
}
