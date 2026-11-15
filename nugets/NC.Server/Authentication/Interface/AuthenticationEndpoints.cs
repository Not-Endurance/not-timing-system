using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Passkeys;

namespace NC.Server.Authentication.Interface;

public static class AuthenticationEndpoints
{
    /// <summary>
    /// Maps the sign-in, registration, session, profile and passkey routes under <c>/api</c>. The host maps these routes and
    /// defines none of them. Every state-changing route is guarded against cross-site requests. The app's profile type is
    /// given as <typeparamref name="TProfile"/>, the same type the module was registered with.
    /// </summary>
    public static void MapNoTimingAuthentication<TProfile>(this IEndpointRouteBuilder endpoints)
        where TProfile : class, new()
    {
        var api = endpoints.MapGroup("/api");
        AccountEndpoints<TProfile>.Map(api);
        PasskeyEndpoints<TProfile>.Map(api);
    }
}
