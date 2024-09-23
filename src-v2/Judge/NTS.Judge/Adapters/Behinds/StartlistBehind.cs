using Not.Application.Ports.CRUD;
using NTS.Domain;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects;
using NTS.Judge.Blazor.Pages.Dashboard;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;
public class StartlistBehind : IStartlistBehind
{
    private readonly IRepository<Participation> _participations;

    public StartlistBehind(IRepository<Participation> participations)
    {
        _participations = participations;
    }

    public StartList UpcomingStarts { get; private set; } = default!;
    public StartList StartHistory { get; private set; } = default!;

    public async Task Initialize()
    {
        var participations = await _participations.ReadAll();
        UpcomingStarts = new StartList(participations, StartList.UPCOMING_STARTS);
        StartHistory = new StartList(participations, StartList.START_HISTORY);
    }

    public void Subscribe(Func<Task> action)
    {
        throw new NotImplementedException();
    }
}
