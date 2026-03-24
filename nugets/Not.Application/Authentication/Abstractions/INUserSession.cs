using Not.Application.Authentication.User;

namespace Not.Application.Authentication.Abstractions;

public interface INUserSessionModel
{
    string UserIdentifier { get; }
    NUserModel User { get; }
}

public interface INUserSessionModel<out TSessionState> : INUserSessionModel
{
    TSessionState? State { get; }
}
