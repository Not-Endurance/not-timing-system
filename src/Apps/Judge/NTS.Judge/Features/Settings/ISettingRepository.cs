using Not.Application.CRUD.Ports;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Settings;

public interface ISettingRepository : IRepository<Setting>
{
    Task<Setting?> Get(Guid accountId);
}
