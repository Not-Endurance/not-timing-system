using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>
/// Opens the Setup of an Event for the person who runs it and makes it the one the Krud pages of the Setup edit (#646). What a
/// person may do is the Api's word (the capabilities of the Event), and what the Setup holds is read from the Api each time
/// it is opened, so a page that was left and came back shows what is stored and not what it remembered.
/// </summary>
public class ConsoleSetup : IConsoleSetup, ITransient
{
    readonly IConsoleEvents _events;
    readonly IRepository<ConfigureEvent> _setups;
    readonly IEnumerable<IKrudNodeSetter> _nodes;
    readonly KrudGraphContext<ConfigureEvent> _graph;

    public ConsoleSetup(
        IConsoleEvents events,
        IRepository<ConfigureEvent> setups,
        IEnumerable<IKrudNodeSetter> nodes,
        KrudGraphContext<ConfigureEvent> graph
    )
    {
        _events = events;
        _setups = setups;
        _nodes = nodes;
        _graph = graph;
    }

    public async Task<OpenedSetup?> Open(Guid eventId)
    {
        await _events.Load();
        var row = _events.Events.FirstOrDefault(x => x.Id == eventId);
        if (row is not { RunsTheConsole: true })
        {
            return null;
        }

        await _graph.Settled(); // what was changed in it a moment ago is saved before it is read again
        var setup = await _setups.Read(eventId);
        if (setup == null)
        {
            return null;
        }

        foreach (var node in _nodes)
        {
            node.SetParent(setup);
        }

        var model = new ConfigureEventFormModel();
        model.MapFrom(setup);
        return new OpenedSetup(model, row.CanEditSetup, row.CanConfigure);
    }

    public async Task RemoveParticipation(Guid eventId, int participationNumber, Guid competitionId)
    {
        await _graph.Settled();
        var setup = await _setups.Read(eventId) ?? throw new DomainException(Event_not_found_string);
        var competition =
            setup.Competitions.FirstOrDefault(x => x.Id == competitionId)
            ?? throw new DomainException(Competition_not_found_string);
        var participation = competition.Participations.FirstOrDefault(x => x.Combination.Number == participationNumber);
        if (participation == null)
        {
            return;
        }

        competition.Remove(participation);
        await _setups.Update(setup);
    }
}
