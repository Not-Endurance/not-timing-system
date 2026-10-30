using NTS.Contracts;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// What a request may do before any endpoint looks at it (#602): reach it at all, and change state through it.
/// </summary>
internal enum AccessDecision
{
    Allow,

    /// <summary>The endpoint is not public and nobody is signed in: 401, never a redirect.</summary>
    NotSignedIn,

    /// <summary>A signed-in caller tried to change state without the header that only a page of this site sends.</summary>
    WriteHeaderRequired,
}

/// <summary>How an endpoint is reached: by the people who are signed in, or by anybody.</summary>
internal enum EndpointAccess
{
    Protected,

    /// <summary>A view that anyone may read, signed in or not (ADR-0001), or a route that has nothing to give.</summary>
    PublicRead,

    /// <summary>The routes where a person signs in or registers, which are anonymous because they make a caller.</summary>
    SignIn,
}

/// <summary>
/// The rules that every endpoint gets without asking for them. They are composed here, as one function of what is
/// known before an endpoint runs, so that they are decided in one place and can be told apart in a table:
/// <list type="number">
/// <item>An endpoint that is not on the list of public endpoints needs a signed-in caller (deny by default).</item>
/// <item>A signed-in caller changes state only with the header <see cref="WRITE_HEADER"/>: a browser sends a header
/// like that to another origin only after the origin has allowed it, so another site cannot write with a visitor's
/// cookie, which a sibling subdomain could otherwise do under SameSite.</item>
/// </list>
/// What a caller may do inside an endpoint (an Event, a Tenant) is decided there, by the one access policy of #643.
/// </summary>
internal static class AccessBaseline
{
    public const string WRITE_HEADER = ApplicationConstants.WRITE_HEADER;
    public const string WRITE_HEADER_VALUE = ApplicationConstants.WRITE_HEADER_VALUE;

    public static AccessDecision Decide(string method, EndpointAccess access, bool signedIn, string? writeHeader)
    {
        if (access != EndpointAccess.Protected)
        {
            return AccessDecision.Allow;
        }

        if (!signedIn)
        {
            return AccessDecision.NotSignedIn;
        }

        return ChangesState(method) && writeHeader != WRITE_HEADER_VALUE
            ? AccessDecision.WriteHeaderRequired
            : AccessDecision.Allow;
    }

    static bool ChangesState(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}
