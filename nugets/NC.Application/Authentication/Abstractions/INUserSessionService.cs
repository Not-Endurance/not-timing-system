using NC.Injection;

namespace NC.Application.Authentication.Abstractions;

public interface INUserSession
{
    Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
        where TSessionState : class;
}
