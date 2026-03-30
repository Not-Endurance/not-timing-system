using Not.Injection;

namespace NTS.Judge.Features.Core.Rankings.CustomRankings;

public interface ICustomRankingService : IScoped
{
    Task Create(CustomRankingModel model);
}
