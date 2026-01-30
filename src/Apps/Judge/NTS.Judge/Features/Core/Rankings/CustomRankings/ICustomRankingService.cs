using Not.Injection;

namespace NTS.Judge.Features.Core.Rankings.CustomRankings;

public interface ICustomRankingService : ISingleton
{
    Task Create(CustomRankingModel model);
}
