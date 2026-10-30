using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Features.Core;
using NTS.Contracts.Features.Console;
using static NoTiming.Ui.Features.Console.ApiAnswers;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The timeline of the selected Participation in the Console (#619, ADR-0005): its time events by Phase, each with the name of
/// the account that recorded it, which the Api tells to the people who may see how times were recorded and which a time event
/// does not carry. What a Participation tells the Main Operator is what is stored, so the events are the dashboard's own; they
/// are read again from it each time the timeline is read.
/// </summary>
public class ConsoleTimeline : IConsoleTimeline, IScoped
{
    readonly IConsoleDashboard _dashboard;
    readonly JsonApiClient _api;

    public ConsoleTimeline(IConsoleDashboard dashboard, JsonApiClient api)
    {
        _dashboard = dashboard;
        _api = api;
    }

    public async Task<IReadOnlyList<TimelinePhase>> Read()
    {
        var participation = _dashboard.Selected ?? throw new DomainException(Select_a_participation_string);
        var names = await NamesOf(participation.EventId);
        return
        [
            .. participation.Phases.Select(phase => new TimelinePhase
            {
                Id = phase.Id,
                Gate = phase.Gate,
                Entries =
                [
                    .. phase.Events.Select(timeEvent => new TimelineEntry
                    {
                        Id = timeEvent.Id,
                        Kind = TimelineWords.KindOf(timeEvent),
                        Times = TimelineWords.TimesOf(timeEvent),
                        Outcome = timeEvent.Outcome,
                        OutcomeText = OutcomeWords.Of(timeEvent.Outcome.ToString()),
                        Source = TimelineWords.SourceOf(timeEvent.Method),
                        Actor = timeEvent.ActorId is { } actor && names.TryGetValue(actor, out var name) ? name : null,
                        RecordedAt = timeEvent.RecordedAt,
                        DisabledAt = timeEvent.DisabledAt,
                        IsShown = phase.Shows(timeEvent),
                        CanDisable = timeEvent.CanBeDisabled,
                        CanEnable = timeEvent.CanBeEnabled,
                    }),
                ],
            }),
        ];
    }

    async Task<IReadOnlyDictionary<Guid, string>> NamesOf(Guid eventId)
    {
        var filter = Uri.EscapeDataString($"eventId eq {eventId}");
        var actors = await AcceptedPages(
            _api,
            $"time-event-actors?filter={filter}",
            x =>
                (
                    Id: Guid.TryParse(x.GetProperty("id").GetString(), out var id) ? id : (Guid?)null,
                    Name: Text(x.GetProperty("attributes"), "displayName") ?? string.Empty
                )
        );
        return actors.Where(x => x.Id != null).ToDictionary(x => x.Id!.Value, x => x.Name);
    }
}
