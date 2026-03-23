using Not.Injection;

namespace Not.Application.Authentication.Abstractions;

public interface INUserSession : IScoped
{
    Task<INUserSessionModel<TSessionState>?> GetCurrent<TSessionState>()
        where TSessionState : class;
}
