namespace Not.Identity.Sessions;

public interface ISessionRevoker
{
    /// <summary>Ends every session of the user: the next request of any of them is anonymous.</summary>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
