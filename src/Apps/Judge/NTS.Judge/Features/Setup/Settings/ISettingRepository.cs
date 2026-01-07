using Not.Application.CRUD.Ports;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Settings;

public interface ISettingRepository : IRepository<Setting>
{
    Task<Setting?> Get(Guid accountId);
}
