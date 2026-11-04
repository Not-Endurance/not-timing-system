namespace NC.Application.Authentication.Abstractions;

public interface INUserSessionRepository<TSessionState>
    where TSessionState : class
{
    Task<TSessionState?> ReadByUserIdentifier(string userIdentifier);
}
