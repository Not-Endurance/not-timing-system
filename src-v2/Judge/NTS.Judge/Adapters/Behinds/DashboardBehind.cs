using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Domain;
using Not.Notifier;
using Not.Safe;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class DashboardBehind : ObservableBehind, IDashboardBehind, ISnapshotProcess
{
    private readonly IRepository<Domain.Setup.Entities.Event> _setupRepository;
    private readonly IRepository<Event> _coreEventRespository;
    private readonly IRepository<Official> _coreOfficialRepository;
    private readonly IRepository<Participation> _participationRepository;
    private readonly IRepository<SnapshotResult> _snapshotResultRepository;
    private ITagRead _tagReader;

    public DashboardBehind(
        IRepository<Domain.Setup.Entities.Event> setupRepository,
        IRepository<Event> coreEventRespository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<SnapshotResult> snapshotResultRepository,
        ITagRead tagReader)
    {
        _setupRepository = setupRepository;
        _coreEventRespository = coreEventRespository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
        _snapshotResultRepository = snapshotResultRepository;
        _tagReader = tagReader;
    }

    public IEnumerable<Participation> Participations { get; private set; } = new List<Participation>();

    protected override async Task<bool> PerformInitialization()
    {
        Participations = await _participationRepository.ReadAll();
        return Participations.Any();
    }
    
    async Task SafeStart()
    {
        var setupEvent = await _setupRepository.Read(0);
        if (setupEvent == null)
        {
            // TODO: Create ValidationException containing localization logic and inherit form it in DomainException. Use that here instead
            throw new DomainException("Cannot start - event is not configured");
        }
        await CreateEvent(setupEvent);
        await CreateOfficials(setupEvent.Officials);
        foreach(var tag in await _tagReader.ReadTags())
        {
            await Process(tag);
        };
    }

    public async Task Process(Snapshot snapshot)
    {
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == snapshot.Number);
        if (participation == null)
        {
            return;
        }
        var result = participation.Process(snapshot);
        if (result.Type == SnapshotResultType.Applied)
        {
            await _participationRepository.Update(participation);
        }
        await _snapshotResultRepository.Create(result);

        EmitChange();
    }

    async Task CreateEvent(Domain.Setup.Entities.Event setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            NotifyHelper.Warn("Cannot start Endurance event: there are no competitions configured");
            return;
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.StartTime);
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        var @event = new Event(setupEvent.Country, setupEvent.Place, "", startDate, endDate, null, null, null); // TODO: fix city and place
        await _coreEventRespository.Create(@event);
    }

    async Task CreateOfficials(IEnumerable<Domain.Setup.Entities.Official> setupOfficials)
    {
        foreach (var setupOfficial in setupOfficials)
        {
            var official = new Official(setupOfficial.Person, setupOfficial.Role);
            await _coreOfficialRepository.Create(official);
        }
    }

    #region SafePattern

    public Task Start()
    {
        return SafeHelper.Run(SafeStart);
    }

    #endregion
}
