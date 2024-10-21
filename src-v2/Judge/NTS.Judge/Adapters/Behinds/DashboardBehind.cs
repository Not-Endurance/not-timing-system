using Not.Application.Ports.CRUD;
using Not.Domain;
using Not.Notifier;
using Not.Safe;
using NTS.Domain.Core.Entities;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class DashboardBehind : IDashboardBehind
{
    private readonly IRepository<Domain.Setup.Entities.EnduranceEvent> _setupRepository;
    private readonly IRepository<EnduranceEvent> _coreEventRespository;
    private readonly IRepository<Official> _coreOfficialRepository;
    private readonly IRepository<Participation> _participationRepository;

    public DashboardBehind(
        IRepository<Domain.Setup.Entities.EnduranceEvent> setupRepository,
        IRepository<EnduranceEvent> coreEventRespository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository)
    {
        _setupRepository = setupRepository;
        _coreEventRespository = coreEventRespository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
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
    }

    async Task CreateEvent(Domain.Setup.Entities.EnduranceEvent setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            NotifyHelper.Warn("Cannot start Endurance event: there are no competitions configured");
            return;
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.Start);
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        var enduranceEvent = new EnduranceEvent(setupEvent.Country, setupEvent.Place, "", startDate, endDate, null, null, null); // TODO: fix city and place
        await _coreEventRespository.Create(enduranceEvent);
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
