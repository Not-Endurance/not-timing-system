using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using NTS.Contracts.Watcher;
using NTS.Contracts.Watcher.Models;

namespace NTS.Application.UserSession;

public interface INtsUserSessionRepository
    : IRepository<NtsUserSessionModel>,
        INUserSessionRepository<NtsUserSessionModel>
{
    Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier, int eventId);
    new Task Delete(NtsUserSessionModel item);
}
