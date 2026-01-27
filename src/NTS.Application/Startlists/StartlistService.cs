using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Startup;
using NTS.Application.Warp;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Objects;

namespace NTS.Application.Startlists;

public class StartlistService : NStatefulService, IStartUpcoming, IStartHistory, IStartupInitializer
{
    readonly IReadMany<Participation> _participations;
    readonly ISelectedEventContext _eventContext;
    readonly StartlistContext _context;

    public StartlistService(IReadMany<Participation> participations, ISelectedEventContext eventContext, StartlistContext context)
    {
        _participations = participations;
        _eventContext = eventContext;
        _context = context;
    }

    public IReadOnlyList<StartlistEntry> Upcoming => _context.Startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _context.Startlist?.History ?? [];

    protected override async Task<bool> CreateState(params IEnumerable<object> arguments)
    {
        var participations = await _participations.ReadMany();
        if (participations.Any())
        {
            _context.Startlist = new Startlist(participations);
        }
        else
        {
            var startlistEntries = new List<StartlistEntry>();
            var competitions = _eventContext.Event!.Competitions.Where(competition =>
                competition.Participations.Any() && competition.Phases.Count > 0);
            foreach (var competition in competitions)
            {
                var entries = competition.Participations.Select(participation =>
                    new StartlistEntry(
                        participation.Combination.Athlete.Names,
                        participation.Combination.Number,
                        1,
                        competition.Phases[0]!.Loop!.Distance,
                        Timestamp.Create((participation.StartTimeOverride ?? competition.Start).ToUniversalTime())!
                    )
                );
                startlistEntries.AddRange(entries);
            }
            _context.Startlist = new Startlist(startlistEntries);
        }
        return _context.Startlist.History.Any() || _context.Startlist.Upcoming.Any();
    }

    public void RunAtStartup()
    {
        Participation.PHASE_COMPLETED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.RESTORED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.ELIMINATED_EVENT.Subscribe(x => RemoveEntry(x.Participation));
    }

    public void Refresh()
    {
        _context.Startlist?.UpdateState();
        EmitChanged();
    }

    void RemoveEntry(Participation participation)
    {
        _context.Startlist?.Remove(participation.Combination.Number);
        EmitChanged();
    }

    void AddEntry(Participation participation)
    {
        _context.Startlist?.Add(new StartlistEntry(participation));
        EmitChanged();
    }
}
