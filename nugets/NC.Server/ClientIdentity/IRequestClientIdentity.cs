namespace NC.Server.ClientIdentity;

/// <summary>The client scope that made the current request, or null when the request carries none.</summary>
public interface IRequestClientIdentity
{
    string? ClientId { get; }
}
