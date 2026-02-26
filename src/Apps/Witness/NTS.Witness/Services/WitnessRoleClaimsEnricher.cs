using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Not.Authentication;
using Not.Authentication.User;
using Not.Injection;

namespace NTS.Witness.Services;

public class WitnessRoleClaimsEnricher : IAuthenticationClaimsEnricher, IScoped
{
    public Task Enrich(ClaimsIdentity identity, NUser user, TicketReceivedContext _)
    {
        foreach (var role in user.Roles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }
        return Task.CompletedTask;
    }
}
