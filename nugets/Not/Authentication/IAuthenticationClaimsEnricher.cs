using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Not.Authentication.User;

namespace Not.Authentication;

public interface IAuthenticationClaimsEnricher
{
    Task Enrich(ClaimsIdentity identity, NUser user, TicketReceivedContext context);
}
