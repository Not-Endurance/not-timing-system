namespace NTS.Witness.Contracts.API;

public record RegisterUserPaload
{
    public RegisterUserPaload(string email, string? name = null)
    {
        Email = email;
        Name = name;
    }

    public string Email { get; }
    public string? Name { get; }
}
