using Not.Application.Authentication.Abstractions;

namespace Not.Application.Authentication.User;

public abstract class NUserSessionModel
{
    protected NUserSessionModel() { }

    protected NUserSessionModel(string userIdentifier)
    {
        UserIdentifier = userIdentifier;
    }

    public string UserIdentifier { get; set; } = "";
}

public class NUserSessionModel<TSessionState> : NUserSessionModel, INUserSessionModel<TSessionState>
{
    protected NUserSessionModel()
    {
        User = null!;
    }

    public NUserSessionModel(string userIdentifier, NUserModel user, TSessionState? state = default)
        : base(userIdentifier)
    {
        User = user;
        State = state;
    }

    public NUserModel User { get; protected set; }
    public TSessionState? State { get; protected set; }
}
