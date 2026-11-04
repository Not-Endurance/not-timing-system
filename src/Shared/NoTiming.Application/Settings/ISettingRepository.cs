using NC.Application.CRUD.Ports;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Application.Settings;

public interface ISettingRepository : IRepository<Setting>
{
    Task<Setting?> Get(Guid accountId);
}
