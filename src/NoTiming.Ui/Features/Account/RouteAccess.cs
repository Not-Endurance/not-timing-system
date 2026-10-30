namespace NoTiming.Ui.Features.Account;

/// <summary>What a page asks of the person before it is shown.</summary>
public enum RouteAccess
{
    /// <summary>The page is shown.</summary>
    Open,

    /// <summary>The page is for a person who is signed in: a visitor is taken to sign in, and back to the page.</summary>
    SignIn,

    /// <summary>The page waits for a complete profile: the person is taken to complete it.</summary>
    Profile,
}
