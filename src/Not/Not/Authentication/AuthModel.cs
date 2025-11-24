using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Not.Authentication;

public class AuthUser
{
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}

public class AuthConfig
{
    public List<AuthUser> Users { get; set; } = [];
}

public static class Auth
{
    public static AuthConfig GetAuthConfigFromAppSettings(WebApplicationBuilder builder)
    {
        return builder.Configuration.GetSection("Auth").Get<AuthConfig>() ?? new AuthConfig();
    }
}
