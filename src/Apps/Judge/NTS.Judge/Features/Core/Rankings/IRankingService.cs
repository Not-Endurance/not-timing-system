using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRankingService : IStatefulService
{
    Ranklist? Ranklist { get; }
    Task ArchiveEnduranceEvent();
}
