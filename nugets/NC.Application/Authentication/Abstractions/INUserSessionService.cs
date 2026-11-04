using Not.Injection;

namespace Not.Application.Authentication.Abstractions;

public interface INUserSession
{
    Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
        where TSessionState : class;
}
