using Not.Domain.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Features.Console.Rankings;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Rankings of a Live Event as the Main Operator changes them (#646): a Ranking of Participations it chooses is written to
/// the Api for the Event the view shows, one is removed, and the view reads its Rankings again, so that what is shown is what
/// is stored. A Ranking that is not taken is said in the Api's words, and the form that made it keeps what was typed.
/// </summary>
public class ConsoleRankings : IConsoleRankings, IKrudFormService<CustomRankingModel>, IScoped
{
    readonly ConsoleRankingWrites _writes;
    IViewedEvent? _view;

    public ConsoleRankings(ConsoleRankingWrites writes)
    {
        _writes = writes;
    }

    public void Show(IViewedEvent view)
    {
        _view = view;
    }

    public async Task Create(CustomRankingModel model)
    {
        var view = Shown();
        view.EnsureCanWrite();
        model.EventId = view.Event.Id;
        await Write(view, () => _writes.Create(model.MapToEntity()));
    }

    public Task Update(CustomRankingModel item)
    {
        throw new NotSupportedException("A custom Ranking is made new and never changed.");
    }

    public async Task Delete(Ranking ranking)
    {
        var view = Shown();
        view.EnsureCanWrite();
        await Write(view, () => _writes.Delete(ranking));
    }

    async Task Write(IViewedEvent view, Func<Task> write)
    {
        await write();
        var refused = _writes.LastError;
        await view.Refresh();
        if (refused != null)
        {
            view.Refused(refused.Code);
            throw new DomainException(refused.Message);
        }
    }

    IViewedEvent Shown()
    {
        return _view ?? throw new InvalidOperationException("The Rankings of no Event are open.");
    }
}
