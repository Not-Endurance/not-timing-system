namespace NC.Application.Identity;

public sealed class ClientIdentity : IClientIdentity
{
    public ClientIdentity()
    {
        Id = Guid.NewGuid().ToString("N");
    }

    public string Id { get; }
}
