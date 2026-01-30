using Not.Application.Behinds.Adapters;
using Not.Injection;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRankingService : IRankingContext, ITransient
{
    Task ArchiveEnduranceEvent();
}
