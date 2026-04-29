using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Application.UserSession;

public interface INtsUserSessionRepository
    : IRepository<NtsUserSessionModel>,
        INUserSessionRepository<NtsUserSessionModel> { }
