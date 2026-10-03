using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Not.Identity.Sessions;

namespace Not.Identity.Mongo;

/// <summary>
/// ASP.NET Core Identity over the application's existing user documents (ADR-0002). The store reads and writes only
/// the identity fields of <see cref="NIdentityUser"/>, each by name, so the application's own fields of the document
/// are preserved by construction. An update names the concurrency stamp it was based on and fails when it is stale.
/// Nothing here knows a Tenant: an email lookup happens before any Tenant is known.
/// </summary>
public sealed class MongoUserStore<TUser>
    : IUserStore<TUser>,
        IUserEmailStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IUserLockoutStore<TUser>,
        IUserPasskeyStore<TUser>
    where TUser : NIdentityUser
{
    readonly IMongoCollection<TUser> _users;
    readonly IdentityErrorDescriber _describer;
    readonly ISessionRevoker? _sessions;

    public MongoUserStore(
        IMongoClient client,
        IOptions<NIdentityOptions> options,
        IdentityErrorDescriber? describer = null,
        ISessionRevoker? sessions = null
    )
    {
        _users = client.GetDatabase(options.Value.Database).GetCollection<TUser>(options.Value.UsersCollection);
        _describer = describer ?? new IdentityErrorDescriber();
        _sessions = sessions;
    }

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        user.Email = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.Email = normalizedName ?? user.Email;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        if (user.Id == Guid.Empty)
        {
            user.Id = Guid.NewGuid();
        }

        user.ConcurrencyStamp = NewStamp();
        try
        {
            await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return IdentityResult.Success;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return IdentityResult.Failed(_describer.DuplicateEmail(user.Email ?? string.Empty));
        }
    }

    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        var basedOn = user.ConcurrencyStamp;
        var stamp = NewStamp();
        var update = Builders<TUser>
            .Update.Set(x => x.Email, user.Email)
            .Set(x => x.EmailConfirmed, user.EmailConfirmed)
            .Set(x => x.SecurityStamp, user.SecurityStamp)
            .Set(x => x.ConcurrencyStamp, stamp)
            .Set(x => x.LockoutEnabled, user.LockoutEnabled)
            .Set(x => x.LockoutEnd, user.LockoutEnd)
            .Set(x => x.AccessFailedCount, user.AccessFailedCount)
            .Set(x => x.ExternalProvider, user.ExternalProvider)
            .Set(x => x.ExternalSubject, user.ExternalSubject);

        // The field exists only while there is a passkey, so a user without one stays out of the credential index.
        update = user.Passkeys.Count == 0 ? update.Unset(x => x.Passkeys) : update.Set(x => x.Passkeys, user.Passkeys);

        // A row from before identity has no concurrency stamp: a null matches a missing field.
        var before = await _users.FindOneAndUpdateAsync(
            Builders<TUser>.Filter.And(
                Builders<TUser>.Filter.Eq(x => x.Id, user.Id),
                Builders<TUser>.Filter.Eq(x => x.ConcurrencyStamp, basedOn)
            ),
            update,
            new FindOneAndUpdateOptions<TUser> { ReturnDocument = ReturnDocument.Before },
            cancellationToken
        );
        if (before is null)
        {
            return IdentityResult.Failed(_describer.ConcurrencyFailure());
        }

        user.ConcurrencyStamp = stamp;
        if (_sessions != null && before.SecurityStamp != user.SecurityStamp)
        {
            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        var result = await _users.DeleteOneAsync(
            Builders<TUser>.Filter.And(
                Builders<TUser>.Filter.Eq(x => x.Id, user.Id),
                Builders<TUser>.Filter.Eq(x => x.ConcurrencyStamp, user.ConcurrencyStamp)
            ),
            cancellationToken
        );
        if (result.DeletedCount == 0)
        {
            return IdentityResult.Failed(_describer.ConcurrencyFailure());
        }

        if (_sessions != null)
        {
            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        }

        return IdentityResult.Success;
    }

    public async Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return null;
        }

        return await _users.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return FindByEmailAsync(normalizedUserName, cancellationToken);
    }

    public async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        // Two rows with one address would sign someone in as the wrong person: refuse rather than pick one.
        var matches = await _users.Find(x => x.Email == normalizedEmail).Limit(2).ToListAsync(cancellationToken);
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"More than one user has the email '{normalizedEmail}'. Resolve the duplicates; the email is unique."
            ),
        };
    }

    public Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.Email = normalizedEmail ?? user.Email;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.SecurityStamp);
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnd);
    }

    public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task AddOrUpdatePasskeyAsync(TUser user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
    {
        // Replaced in place when the credential is known, because its sign count moves each time it signs in.
        var stored = ToDocument(passkey);
        var index = user.Passkeys.FindIndex(x => x.CredentialId.AsSpan().SequenceEqual(passkey.CredentialId));
        if (index >= 0)
        {
            user.Passkeys[index] = stored;
        }
        else
        {
            user.Passkeys.Add(stored);
        }

        return Task.CompletedTask;
    }

    public async Task<TUser?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        return await _users
            .Find(Builders<TUser>.Filter.ElemMatch(x => x.Passkeys, x => x.CredentialId == credentialId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserPasskeyInfo?> FindPasskeyAsync(TUser user, byte[] credentialId, CancellationToken cancellationToken)
    {
        var passkey = user.Passkeys.Find(x => x.CredentialId.AsSpan().SequenceEqual(credentialId));
        return Task.FromResult(passkey is null ? null : ToInfo(passkey));
    }

    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<UserPasskeyInfo>>([.. user.Passkeys.Select(ToInfo)]);
    }

    public Task RemovePasskeyAsync(TUser user, byte[] credentialId, CancellationToken cancellationToken)
    {
        user.Passkeys.RemoveAll(x => x.CredentialId.AsSpan().SequenceEqual(credentialId));
        return Task.CompletedTask;
    }

    public void Dispose() { }

    static NIdentityPasskey ToDocument(UserPasskeyInfo passkey)
    {
        return new NIdentityPasskey
        {
            CredentialId = passkey.CredentialId,
            PublicKey = passkey.PublicKey,
            Name = passkey.Name,
            CreatedAt = passkey.CreatedAt,
            SignCount = passkey.SignCount,
            Transports = passkey.Transports ?? [],
            IsUserVerified = passkey.IsUserVerified,
            IsBackupEligible = passkey.IsBackupEligible,
            IsBackedUp = passkey.IsBackedUp,
            AttestationObject = passkey.AttestationObject,
            ClientDataJson = passkey.ClientDataJson,
        };
    }

    static UserPasskeyInfo ToInfo(NIdentityPasskey passkey)
    {
        return new UserPasskeyInfo(
            passkey.CredentialId,
            passkey.PublicKey,
            passkey.CreatedAt,
            passkey.SignCount,
            passkey.Transports,
            passkey.IsUserVerified,
            passkey.IsBackupEligible,
            passkey.IsBackedUp,
            passkey.AttestationObject,
            passkey.ClientDataJson
        )
        {
            Name = passkey.Name,
        };
    }

    static string NewStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
