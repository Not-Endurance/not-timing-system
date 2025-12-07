using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using static Not.Authentication.Provider.SuffixConstants;

namespace Not.Authentication.User;

public class NUserResolver : IUserResolver
{
    public Task UserResolution(TicketReceivedContext context, List<NUser> allowedUsersByEmail)
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

        // find user in allow list
        var authUser = allowedUsersByEmail
            .FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

        // deny if: no email / email from unregistered provider / email not in allow list
        if (
            string.IsNullOrWhiteSpace(email)
            || !email.EndsWithProviderSuffix([GMAIL])
            || authUser == null
        )
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
                newIdentity!.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.CompletedTask;
    }
}
