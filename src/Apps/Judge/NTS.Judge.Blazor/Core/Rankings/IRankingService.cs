using Not.Blazor.Ports;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Blazor.Core.Rankings;

public interface IRankingService : INObservable
{
    Ranklist? Ranklist { get; }
    Task GenerateFeiExport();
    Task ArchiveEnduranceEvent();
}
