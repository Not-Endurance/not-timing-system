namespace NTS.Witness.Web;

public class AuthUser
{
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}

public class AuthConfig
{
    public List<AuthUser> Users { get; set; } = [];
}
