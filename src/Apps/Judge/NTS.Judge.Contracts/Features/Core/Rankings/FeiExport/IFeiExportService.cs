using Not.Injection;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;

public interface IFeiExportService : ITransient
{
    Task Create(Ranklist ranklist);
}
