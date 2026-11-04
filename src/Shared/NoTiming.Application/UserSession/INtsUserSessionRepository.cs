using NC.Application.Authentication.Abstractions;
using NC.Application.CRUD.Ports;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;

namespace NoTiming.Application.UserSession;

public interface INoTimingUserSessionRepository
    : IRepository<NoTimingUserSessionModel>,
        INUserSessionRepository<NoTimingUserSessionModel>
{
    Task<NoTimingUserSessionModel?> ReadByUserIdentifier(string userIdentifier, int eventId);
    new Task Delete(NoTimingUserSessionModel item);
}
