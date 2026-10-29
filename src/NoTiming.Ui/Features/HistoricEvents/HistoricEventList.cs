using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.HistoricEvents;

/// <summary>
/// The list of Historic Events, last day first (#628): what the list page shows and nothing more. An Event is opened from
/// it through the viewed-event provider (#630, ADR-0007), and no operation of the list changes an Event that has ended.
/// </summary>
public sealed class HistoricEventList : IKrudListBehind<EventInformation>, IScoped
{
    readonly IEventInformationService _events;

    public HistoricEventList(IEventInformationService events)
    {
        _events = events;
    }

    public Task<IEnumerable<EventInformation>> ReadMany()
    {
        return _events.GetHistoric();
    }

    public Task Delete(EventInformation entity)
    {
        throw CreateReadOnlyException();
    }

    public Task<KrudDeleteImpact> PreviewDelete(EventInformation entity)
    {
        return Task.FromResult(new KrudDeleteImpact(entity.ToString(), []));
    }

    public Task DeleteCascade(EventInformation entity)
    {
        throw CreateReadOnlyException();
    }

    static NotSupportedException CreateReadOnlyException()
    {
        return new NotSupportedException("Historic Events are read-only.");
    }
}
