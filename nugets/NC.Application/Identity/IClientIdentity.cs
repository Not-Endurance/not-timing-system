namespace NC.Application.Identity;

/// <summary>Identifies one running client scope. Writes carry it so the client can tell its own changes from others'.</summary>
public interface IClientIdentity
{
    string Id { get; }
}
