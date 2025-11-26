using Microsoft.AspNetCore.Authentication;

namespace Not.Authentication.User;

public interface IUserResolver
{
    public Task UserResolution(TicketReceivedContext context, Dictionary<string, NUser> allowedUsersByEmail);
}
