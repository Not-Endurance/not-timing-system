namespace Not.Application.Authentication.User;

public record NUserRegistration
{
    public NUserRegistration(string email, string? name = null)
    {
        Email = email;
        Name = name;
    }

    public string Email { get; }
    public string? Name { get; }
}
