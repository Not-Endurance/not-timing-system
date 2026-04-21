namespace Not.Application.Authentication.Abstractions;

public interface INAuthenticationSessionStorage
{
    Task<DateTimeOffset?> ReadSessionStartedAtAsync();
    Task WriteSessionStartedAt(DateTimeOffset startedAtUtc);
    Task ClearSessionStartedAt();
    Task<DateTimeOffset?> ReadSigninFlowStartedAtAsync();
    Task WriteSigninFlowStartedAt();
    Task ClearSigninFlowStartedAt();
}
