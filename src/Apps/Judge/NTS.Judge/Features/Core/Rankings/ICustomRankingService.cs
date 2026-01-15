using Not.Injection;

namespace NTS.Judge.Features.Core.Rankings;

public interface ICustomRankingService : ISingleton
{
    Task Create(CustomRankingModel model);
}
