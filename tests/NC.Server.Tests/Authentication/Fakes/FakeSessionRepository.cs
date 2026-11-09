using NC.Server.Authentication.Sessions;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>Records the sessions created and removed, so a test can see what a sign-in or sign-out did.</summary>
internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<(int UserId, string TokenHash)> Created { get; } = [];

    public List<string> Removed { get; } = [];

    public Task<SessionDocument?> FindLive(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SessionDocument?>(null);
    }

    public Task Create(
        string tokenHash,
        int userId,
        string securityStamp,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        Created.Add((userId, tokenHash));
        return Task.CompletedTask;
    }

    public Task Extend(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Remove(string tokenHash, CancellationToken cancellationToken = default)
    {
        Removed.Add(tokenHash);
        return Task.CompletedTask;
    }

    public Task RemoveAllForUser(int userId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
