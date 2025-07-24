using Not.Injection;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public interface ICustomRankingService : ISingleton
{
    Task Create(CustomRankingModel model);
}
