using Not.Application.CRUD.Ports;
using NTS.Application.Watcher;

namespace NTS.Application.UserSession;

public interface IUserSessionRepository : IRepository<UserSessionModel>
{
    Task<UserSessionModel?> ReadByUserIdentifier(string userIdentifier);
}
