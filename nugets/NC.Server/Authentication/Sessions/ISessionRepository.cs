namespace NC.Server.Authentication.Sessions;

/// <summary>The sessions of signed-in users. A session is stored by the hash of its token, never by the token.</summary>
internal interface ISessionRepository
{
    Task<SessionDocument?> FindLive(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    );

    Task Create(
        string tokenHash,
        int userId,
        string securityStamp,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    );

    Task Extend(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task Remove(string tokenHash, CancellationToken cancellationToken = default);

    Task RemoveAllForUser(int userId, CancellationToken cancellationToken = default);
}
