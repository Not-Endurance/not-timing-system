using NTS.Domain.Core.Aggregates;

namespace NTS.Application.PastEvents;

public interface IPastParticipationRepository
{
    Task<IEnumerable<Participation>> ReadForEvent(int eventId);
}

public interface IPastRankingRepository
{
    Task<IEnumerable<Ranking>> ReadForEvent(int eventId);
}

public interface IPastOfficialRepository
{
    Task<IEnumerable<Official>> ReadForEvent(int eventId);
}
