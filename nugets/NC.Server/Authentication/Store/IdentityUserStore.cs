using System.Buffers.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Random;
using NC.Server.Authentication.Passkeys;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Store;

/// <summary>
/// ASP.NET Core Identity over the <c>users</c> collection. It reads and writes only the identity fields of
/// <see cref="IdentityUserDocument"/>, so the application's own fields of a user document are preserved. An update
/// names the concurrency stamp it was based on and fails when another request changed the user in between.
/// </summary>
public sealed class IdentityUserStore
    : IUserStore<IdentityUserDocument>,
        IUserEmailStore<IdentityUserDocument>,
        IUserSecurityStampStore<IdentityUserDocument>,
        IUserLockoutStore<IdentityUserDocument>,
        IUserPasskeyStore<IdentityUserDocument>,
        IPasskeyUsers
{
    const int ChangeAttempts = 4;

    readonly IMongoCollection<IdentityUserDocument> _users;
    readonly IdentityErrorDescriber _describer = new();

    public IdentityUserStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _users = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<IdentityUserDocument>(settings.UsersCollection);
    }

    /// <summary>The WebAuthn user handle, base64url. The integer id is never exposed to an authenticator.</summary>
    public Task<string> GetUserIdAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        if (user.UserHandle is null)
        {
            throw new InvalidOperationException("The user has no passkey user handle yet.");
        }

        return Task.FromResult(Base64Url.EncodeToString(user.UserHandle));
    }

    public Task<string?> GetUserNameAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetUserNameAsync(IdentityUserDocument user, string? userName, CancellationToken cancellationToken)
    {
        user.Email = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetNormalizedUserNameAsync(
        IdentityUserDocument user,
        string? normalizedName,
        CancellationToken cancellationToken
    )
    {
        user.Email = normalizedName ?? user.Email;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
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

    public async Task<IdentityResult> UpdateAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        var basedOn = user.ConcurrencyStamp;
        var stamp = NewStamp();
        var update = Builders<IdentityUserDocument>
            .Update.Set(x => x.Email, user.Email)
            .Set(x => x.ConcurrencyStamp, stamp)
            .Set(x => x.LockoutEnabled, user.LockoutEnabled)
            .Set(x => x.AccessFailedCount, user.AccessFailedCount);

        // Absent fields are removed rather than set to null: the partial unique index counts a null as present.
        update = user.ProvedAt is null ? update.Unset(x => x.ProvedAt) : update.Set(x => x.ProvedAt, user.ProvedAt);
        update = user.LockoutEnd is null
            ? update.Unset(x => x.LockoutEnd)
            : update.Set(x => x.LockoutEnd, user.LockoutEnd);
        update = user.SecurityStamp is null
            ? update.Unset(x => x.SecurityStamp)
            : update.Set(x => x.SecurityStamp, user.SecurityStamp);
        update = user.UserHandle is null ? update : update.Set(x => x.UserHandle, user.UserHandle);

        // The field exists only while there is a passkey, so a user without one stays out of the credential index.
        update =
            Passkeys(user).Count == 0 ? update.Unset(x => x.Passkeys) : update.Set(x => x.Passkeys, Passkeys(user));

        try
        {
            var result = await _users.UpdateOneAsync(
                Builders<IdentityUserDocument>.Filter.And(
                    Builders<IdentityUserDocument>.Filter.Eq(x => x.Id, user.Id),
                    Builders<IdentityUserDocument>.Filter.Eq(x => x.ConcurrencyStamp, basedOn)
                ),
                update,
                cancellationToken: cancellationToken
            );
            if (result.MatchedCount == 0)
            {
                return IdentityResult.Failed(_describer.ConcurrencyFailure());
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return IdentityResult.Failed(_describer.DuplicateEmail(user.Email ?? string.Empty));
        }

        user.ConcurrencyStamp = stamp;
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        var result = await _users.DeleteOneAsync(
            Builders<IdentityUserDocument>.Filter.And(
                Builders<IdentityUserDocument>.Filter.Eq(x => x.Id, user.Id),
                Builders<IdentityUserDocument>.Filter.Eq(x => x.ConcurrencyStamp, user.ConcurrencyStamp)
            ),
            cancellationToken
        );

        return result.DeletedCount == 0
            ? IdentityResult.Failed(_describer.ConcurrencyFailure())
            : IdentityResult.Success;
    }

    /// <summary>Finds the user whose WebAuthn user handle is the given base64url value.</summary>
    public async Task<IdentityUserDocument?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        byte[] handle;
        try
        {
            handle = Base64Url.DecodeFromChars(userId);
        }
        catch (FormatException)
        {
            return null;
        }

        return await _users.Find(x => x.UserHandle == handle).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IdentityUserDocument?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return FindByEmailAsync(normalizedUserName, cancellationToken);
    }

    /// <summary>Case-insensitive, because a legacy row may still hold a mixed-case address.</summary>
    public async Task<IdentityUserDocument?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        var matches = await FindAllByEmailAsync(normalizedEmail, cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    public Task SetEmailAsync(IdentityUserDocument user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.IsProved);
    }

    public Task SetEmailConfirmedAsync(IdentityUserDocument user, bool confirmed, CancellationToken cancellationToken)
    {
        user.ProvedAt = confirmed ? user.ProvedAt ?? DateTime.UtcNow : null;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetNormalizedEmailAsync(
        IdentityUserDocument user,
        string? normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        user.Email = normalizedEmail ?? user.Email;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(IdentityUserDocument user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.SecurityStamp);
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            user.LockoutEnd.HasValue ? new DateTimeOffset(user.LockoutEnd.Value, TimeSpan.Zero) : (DateTimeOffset?)null
        );
    }

    public Task SetLockoutEndDateAsync(
        IdentityUserDocument user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken
    )
    {
        user.LockoutEnd = lockoutEnd?.UtcDateTime;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task<bool> GetLockoutEnabledAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task SetLockoutEnabledAsync(IdentityUserDocument user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task AddOrUpdatePasskeyAsync(
        IdentityUserDocument user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken
    )
    {
        // Replaced in place when the credential is known, because its sign count moves each time it signs in.
        var stored = ToDocument(passkey);
        var passkeys = Passkeys(user);
        var index = passkeys.FindIndex(x => x.CredentialId.AsSpan().SequenceEqual(passkey.CredentialId));
        if (index >= 0)
        {
            passkeys[index] = stored;
        }
        else
        {
            passkeys.Add(stored);
        }

        user.Passkeys = passkeys;
        return Task.CompletedTask;
    }

    public async Task<IdentityUserDocument?> FindByPasskeyIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        return await _users
            .Find(Builders<IdentityUserDocument>.Filter.ElemMatch(x => x.Passkeys, x => x.CredentialId == credentialId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserPasskeyInfo?> FindPasskeyAsync(
        IdentityUserDocument user,
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        var passkey = Passkeys(user).Find(x => x.CredentialId.AsSpan().SequenceEqual(credentialId));
        return Task.FromResult(passkey is null ? null : ToInfo(passkey));
    }

    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<UserPasskeyInfo>>([.. Passkeys(user).Select(ToInfo)]);
    }

    public Task RemovePasskeyAsync(IdentityUserDocument user, byte[] credentialId, CancellationToken cancellationToken)
    {
        var passkeys = Passkeys(user);
        passkeys.RemoveAll(x => x.CredentialId.AsSpan().SequenceEqual(credentialId));
        user.Passkeys = passkeys;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Changes a user by id, starting again from what is stored when another request changed the same user in between.
    /// Two passkeys enrolled at once, for example, both end up stored.
    /// </summary>
    public async Task<IdentityResult> ChangeAsync(
        int userId,
        Func<IdentityUserDocument, Task<IdentityResult>> change,
        CancellationToken cancellationToken = default
    )
    {
        var result = IdentityResult.Failed(_describer.ConcurrencyFailure());
        for (var attempt = 0; attempt < ChangeAttempts; attempt++)
        {
            var user = await FindByUserIdAsync(userId, cancellationToken);
            if (user is null)
            {
                return IdentityResult.Failed(
                    new IdentityError { Code = "UserNotFound", Description = "There is no such user." }
                );
            }

            result = await change(user);
            if (result.Succeeded || result.Errors.All(x => x.Code != nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Removes one passkey as a single update, and only while another passkey remains. The condition is part of the
    /// update, so two removals at once cannot leave a user with none.
    /// </summary>
    public async Task<bool> RemovePasskeyWhileAnotherRemainsAsync(
        int userId,
        byte[] credentialId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _users.UpdateOneAsync(
            Builders<IdentityUserDocument>.Filter.And(
                Builders<IdentityUserDocument>.Filter.Eq(x => x.Id, userId),
                Builders<IdentityUserDocument>.Filter.ElemMatch(x => x.Passkeys, x => x.CredentialId == credentialId),
                Builders<IdentityUserDocument>.Filter.Exists("Passkeys.1")
            ),
            Builders<IdentityUserDocument>.Update.PullFilter(x => x.Passkeys, x => x.CredentialId == credentialId),
            cancellationToken: cancellationToken
        );

        return result.ModifiedCount == 1;
    }

    /// <summary>Every row with the address, case-insensitively, up to two. Two is enough to tell a duplicate apart.</summary>
    public async Task<List<IdentityUserDocument>> FindAllByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        return await _users
            .Find(
                Builders<IdentityUserDocument>.Filter.Eq(x => x.Email, normalizedEmail),
                new FindOptions { Collation = EmailCollation.Value }
            )
            .Limit(2)
            .ToListAsync(cancellationToken);
    }

    public Task<IdentityUserDocument?> FindByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _users.Find(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken)!;
    }

    /// <summary>Creates a user whose address was proved by a code. Its registration details are kept on the row.</summary>
    public async Task<IdentityUserDocument> CreateProvedUserAsync(
        string email,
        string givenName,
        string surname,
        string countryRegion,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var user = new IdentityUserDocument
        {
            Id = RandomHelper.GenerateUniqueInteger(),
            Email = email,
            ProvedAt = now.UtcDateTime,
            SecurityStamp = NewStamp(),
            ConcurrencyStamp = NewStamp(),
            GivenName = givenName,
            Surname = surname,
            CountryRegion = countryRegion,
            Name = BuildName(givenName, null, surname),
            TenantId = "nts",
            Roles = [],
        };

        await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
        return user;
    }

    /// <summary>Changes the profile fields only; the rest of the document, its roles included, is left as it is.</summary>
    public Task UpdateProfileAsync(
        int userId,
        string givenName,
        string? middleName,
        string surname,
        string countryRegion,
        string? club,
        string? feiId,
        CancellationToken cancellationToken
    )
    {
        var update = Builders<IdentityUserDocument>
            .Update.Set(x => x.GivenName, givenName)
            .Set(x => x.MiddleName, middleName)
            .Set(x => x.Surname, surname)
            .Set(x => x.Name, BuildName(givenName, middleName, surname))
            .Set(x => x.CountryRegion, countryRegion)
            .Set(x => x.Club, club)
            .Set(x => x.FeiId, feiId);

        return _users.UpdateOneAsync(x => x.Id == userId, update, cancellationToken: cancellationToken);
    }

    public void Dispose() { }

    static string BuildName(string givenName, string? middleName, string surname)
    {
        return string.Join(
            " ",
            new[] { givenName, middleName, surname }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())
        );
    }

    static List<IdentityPasskeyDocument> Passkeys(IdentityUserDocument user)
    {
        user.Passkeys ??= [];
        return user.Passkeys;
    }

    static IdentityPasskeyDocument ToDocument(UserPasskeyInfo passkey)
    {
        return new IdentityPasskeyDocument
        {
            CredentialId = passkey.CredentialId,
            PublicKey = passkey.PublicKey,
            Name = passkey.Name,
            CreatedAt = passkey.CreatedAt.UtcDateTime,
            SignCount = passkey.SignCount,
            Transports = passkey.Transports ?? [],
            IsUserVerified = passkey.IsUserVerified,
            IsBackupEligible = passkey.IsBackupEligible,
            IsBackedUp = passkey.IsBackedUp,
            AttestationObject = passkey.AttestationObject,
            ClientDataJson = passkey.ClientDataJson,
        };
    }

    static UserPasskeyInfo ToInfo(IdentityPasskeyDocument passkey)
    {
        return new UserPasskeyInfo(
            passkey.CredentialId,
            passkey.PublicKey,
            new DateTimeOffset(passkey.CreatedAt, TimeSpan.Zero),
            (uint)passkey.SignCount,
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
