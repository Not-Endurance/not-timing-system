namespace NTS.Witness.Contracts.API;

public record RegisterUserPaload
{
    public RegisterUserPaload(string email)
    {
        Email = email;
    }

    public string Email { get; }
}
