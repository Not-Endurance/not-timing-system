using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using NTS.Application.Watcher;

namespace NTS.Application.UserSession;

public interface INtsUserSessionRepository
    : IRepository<NtsUserSessionModel>,
        INUserSessionRepository<NtsUserSessionModel>
{ }
