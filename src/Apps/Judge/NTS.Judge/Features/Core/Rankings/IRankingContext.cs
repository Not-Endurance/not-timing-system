using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRankingContext : IStatefulService
{
    Ranking Current { get; }
}
