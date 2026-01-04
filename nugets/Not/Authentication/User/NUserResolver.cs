using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using static Not.Authentication.Provider.SuffixConstants;

namespace Not.Authentication.User;

public class NUserResolver : IUserResolver
{
    public Task Resolve(TicketReceivedContext context)
    {
        var userDeserializer = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationSettings>();
        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null || context.Principal == null)
        {
            context.Response.Redirect("/error");
            context.Fail("Login error - missing user email");
            context.HandleResponse();
            return Task.CompletedTask;
        }
        if (context.Principal.Identity == null)
        {
            context.Response.Redirect("/error");
            context.Fail("Login error - missing user identity");
            context.HandleResponse();
            return Task.CompletedTask;
        }
        var oldIdentity = (ClaimsIdentity)context.Principal.Identity;

        var newIdentity = new ClaimsIdentity(
            oldIdentity.Claims,
            oldIdentity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role
        );

        // clear old identities and add new one
        context.Principal = new ClaimsPrincipal(newIdentity);

        // find user in allow list
        var authUser = userDeserializer.GetUserByEmail(email);

        // deny if: no email / email from unregistered provider / email not in allow list
        if (string.IsNullOrWhiteSpace(email) || authUser == null)
        {
            context.Response.Redirect("/access-denied");
            context.Fail("Not allowed");
            context.HandleResponse();
            return Task.CompletedTask;
        }

        foreach (var role in authUser.Roles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                newIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.CompletedTask;
    }
}
