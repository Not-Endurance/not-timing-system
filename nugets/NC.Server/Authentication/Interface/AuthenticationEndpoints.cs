using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Passkeys;

namespace NC.Server.Authentication.Interface;

public static class AuthenticationEndpoints
{
    /// <summary>
    /// Maps the sign-in, registration, session, profile and passkey routes under <c>/api</c>. The host maps these routes and
    /// defines none of them. Every state-changing route is guarded against cross-site requests.
    /// </summary>
    public static void MapNoTimingAuthentication(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        AccountEndpoints.Map(api);
        PasskeyEndpoints.Map(api);
    }
}
