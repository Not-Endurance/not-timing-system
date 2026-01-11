using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Nodes;
using Not.Application.Krud.Services;
using Not.Notify;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class UpcomingEventBehind
    : KrudServiceBase<UpcomingEvent, EnduranceEventFormModel>,
        IKrudMirror<Loop>,
        IKrudMirror<Combination>,
        IKrudMirror<Athlete>,
        IKrudMirror<Horse>
{
    readonly IKrudParentNodeOf<Competition> _competitionParentNode;
    readonly IKrudParentNodeOf<Combination> _combinationParentNode;
    readonly IKrudParentNodeOf<Official> _officialParentNode;
    readonly IKrudParentNodeOf<Loop> _loopParentNode;
    readonly IUpdate<UpcomingEvent> _updater;
    readonly ISelectedEventContext _eventContext;

    public UpcomingEventBehind(
        IRepository<UpcomingEvent> events,
        IKrudParentNodeOf<Competition> competitionParentNode,
        IKrudParentNodeOf<Combination> combinationParentNode,
        IKrudParentNodeOf<Official> officialParentNode,
        IKrudParentNodeOf<Loop> loopParentNode,
        ISelectedEventContext eventContext
    )
        : base(events, [])
    {
        _updater = events;
        _competitionParentNode = competitionParentNode;
        _combinationParentNode = combinationParentNode;
        _officialParentNode = officialParentNode;
        _loopParentNode = loopParentNode;
        _eventContext = eventContext;
    }

    protected override UpcomingEvent CreateEntity(EnduranceEventFormModel model)
    {
        return new UpcomingEvent(
            model.Name,
            model.Place,
            model.Country,
            model.FeiShowId,
            model.FeiId,
            model.FeiEventCode
        );
    }

    protected override UpcomingEvent UpdateEntity(EnduranceEventFormModel model)
    {
        return new UpcomingEvent(
            model.Id,
            model.Name,
            model.Place,
            model.Country,
            model.FeiShowId,
            model.FeiId,
            model.FeiEventCode,
            _competitionParentNode.Children,
            _officialParentNode.Children,
            _loopParentNode.Children,
            _combinationParentNode.Children
        );
    }

    public override Task Delete(UpcomingEvent entity)
    {
        NotifyHelper.Inform("Upcoming events cannot be deleted");
        return Task.CompletedTask;
    }

    // TODO: separate the Athlete, Horse, Loop and Combination AggregateRoots
    // from the UpcomingEvent AggregateRoot and maintain synced state using a domain event dispatcher
    // I.e. - Horse updates, raising a domain Event which updates UpcomingEvent state and (maybe?) triggers re-render
    public async Task Reflect(Loop loop)
    {
        foreach (var competitions in _competitionParentNode.Children)
        {
            foreach (var phase in competitions.Phases)
            {
                phase.Reflect(loop);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Combination combination)
    {
        foreach (var competitions in _competitionParentNode.Children)
        {
            foreach (var participation in competitions.Participations)
            {
                participation.Reflect(combination);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Athlete athlete)
    {
        foreach (var combination in _combinationParentNode.Children)
        {
            combination.Reflect(athlete);
            foreach (var competition in _competitionParentNode.Children)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }

        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Horse horse)
    {
        foreach (var combination in _combinationParentNode.Children)
        {
            combination.Reflect(horse);
            foreach (var competition in _competitionParentNode.Children)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }
        await _updater.Update(_eventContext.Event!);
    }
}
