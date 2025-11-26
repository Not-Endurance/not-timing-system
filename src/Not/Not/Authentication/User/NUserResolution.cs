using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Not.Authentication.User;
public class NUserResolution : IUserResolver
{
    public Task UserResolution(TicketReceivedContext context, Dictionary<string, NUser> allowedUsersByEmail)
    {
        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        var oldIdentity = (ClaimsIdentity)context.Principal!.Identity!;

        var newIdentity = new ClaimsIdentity(
            oldIdentity.Claims,
            oldIdentity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role
        );

        // clear old identities and add new one
        context.Principal = new ClaimsPrincipal(newIdentity);

        // deny if no email / not gmail / not in allow list
        if (
            string.IsNullOrWhiteSpace(email)
            || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
            || !allowedUsersByEmail.TryGetValue(email, out var authUser)
        )
        {
            context.Response.Redirect("/access-denied");
            context.Response.StatusCode = 403;
            context.HandleResponse();
            return Task.CompletedTask;
        }

        foreach (var role in authUser.Roles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                newIdentity!.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.CompletedTask;
    }
}
