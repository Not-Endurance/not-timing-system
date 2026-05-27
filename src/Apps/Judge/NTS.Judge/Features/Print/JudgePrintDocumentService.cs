using Not.Application.CRUD.Ports;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Contracts.Features.Print;

namespace NTS.Judge.Features.Print;

public class JudgePrintDocumentService : IJudgePrintDocumentService
{
    readonly IEventInformationRepository _events;
    readonly IRepository<Handout> _handouts;
    readonly IRepository<Official> _officials;
    readonly IRepository<Ranking> _rankings;

    public JudgePrintDocumentService(
        IEventInformationRepository events,
        IRepository<Handout> handouts,
        IRepository<Official> officials,
        IRepository<Ranking> rankings
    )
    {
        _events = events;
        _handouts = handouts;
        _officials = officials;
        _rankings = rankings;
    }

    public async Task<IReadOnlyList<HandoutDocument>> CreateHandouts(int eventId)
    {
        var eventInformation = await _events.Read(eventId);
        if (eventInformation == null)
        {
            return [];
        }

        var officials = (await _officials.ReadMany(x => x.EventId == eventId)).ToList();
        var handouts = await _handouts.ReadMany(x => x.EventId == eventId);
        return handouts.Select(handout => new HandoutDocument(handout, eventInformation, officials)).ToList();
    }

    public async Task<ProtocolDocument?> CreateRanklist(int eventId, int rankingId)
    {
        var eventInformation = await _events.Read(eventId);
        var ranking = await _rankings.Read(x => x.EventId == eventId && x.Id == rankingId);
        if (eventInformation == null || ranking == null)
        {
            return null;
        }

        var officials = await _officials.ReadMany(x => x.EventId == eventId);
        return new ProtocolDocument(new Ranklist(ranking), eventInformation, officials);
    }
}
