namespace NC.Server.Authentication.Passkeys;

/// <summary>Starts the session of a user who signed in with a passkey. The session store implements it.</summary>
public interface IPasskeySessionStore
{
    Task CreateAsync(
        string tokenHash,
        int userId,
        string securityStamp,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );
}
