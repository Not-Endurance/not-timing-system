using Not.Authentication.User;

namespace Not.Authentication;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public List<NUser> Users { get; set; } = [];
}
