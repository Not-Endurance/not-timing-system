using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Passkeys;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>
/// Holds users the way the store does: a change works on a copy, and the copy reaches the store only through an update.
/// A passkey change that is never updated is therefore lost, as it would be in production.
/// </summary>
internal sealed class FakeIdentityUsers : IIdentityUserRepository
{
    readonly Dictionary<int, IdentityUserDocument> _stored = [];

    public FakeIdentityUsers(params IdentityUserDocument[] users)
    {
        foreach (var user in users)
        {
            _stored[user.Id] = Copy(user);
        }
    }

    /// <summary>When true, every update fails as if another request changed the user first.</summary>
    public bool FailUpdates { get; set; }

    public IdentityUserDocument Stored(int userId)
    {
        return _stored[userId];
    }

    public Task<List<IdentityUserDocument>> FindAllByEmail(string normalizedEmail, CancellationToken cancellationToken)
    {
        List<IdentityUserDocument> matches =
        [
            .. _stored
                .Values.Where(x => string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .Select(Copy),
        ];
        return Task.FromResult(matches);
    }

    public Task<IdentityUserDocument> CreateProvedUser(
        string email,
        string givenName,
        string surname,
        string countryRegion,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException();
    }

    public Task UpdateProfile(
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
        throw new NotSupportedException();
    }

    public Task<IdentityUserDocument?> FindByUserId(int userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_stored.TryGetValue(userId, out var user) ? Copy(user) : null);
    }

    public Task<IdentityResult> Change(
        int userId,
        Func<IdentityUserDocument, Task<IdentityResult>> change,
        CancellationToken cancellationToken = default
    )
    {
        return change(Copy(_stored[userId]));
    }

    public Task<IdentityResult> UpdateAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        if (FailUpdates)
        {
            return Task.FromResult(IdentityResult.Failed());
        }

        _stored[user.Id] = Copy(user);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task AddOrUpdatePasskeyAsync(
        IdentityUserDocument user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken
    )
    {
        user.Passkeys ??= [];
        user.Passkeys.RemoveAll(x => x.CredentialId.AsSpan().SequenceEqual(passkey.CredentialId));
        user.Passkeys.Add(
            new IdentityPasskeyDocument
            {
                CredentialId = passkey.CredentialId,
                PublicKey = passkey.PublicKey,
                Name = passkey.Name,
                CreatedAtUtc = passkey.CreatedAt.UtcDateTime,
            }
        );
        return Task.CompletedTask;
    }

    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        IList<UserPasskeyInfo> passkeys = (user.Passkeys ?? [])
            .Select(x => new UserPasskeyInfo(
                x.CredentialId,
                x.PublicKey,
                new DateTimeOffset(x.CreatedAtUtc, TimeSpan.Zero),
                0,
                [],
                true,
                false,
                false,
                [],
                []
            )
            {
                Name = x.Name,
            })
            .ToList();
        return Task.FromResult(passkeys);
    }

    public Task<bool> RemovePasskeyWhileAnotherRemains(
        int userId,
        byte[] credentialId,
        CancellationToken cancellationToken = default
    )
    {
        var user = _stored[userId];
        if (user.Passkeys is not { Count: > 1 })
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            user.Passkeys.RemoveAll(x => x.CredentialId.AsSpan().SequenceEqual(credentialId)) > 0
        );
    }

    static IdentityUserDocument Copy(IdentityUserDocument user)
    {
        return new IdentityUserDocument
        {
            Id = user.Id,
            Email = user.Email,
            ProvedAtUtc = user.ProvedAtUtc,
            UserHandle = user.UserHandle,
            SecurityStamp = user.SecurityStamp,
            ConcurrencyStamp = user.ConcurrencyStamp,
            Name = user.Name,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            MiddleName = user.MiddleName,
            Surname = user.Surname,
            CountryRegion = user.CountryRegion,
            Club = user.Club,
            FeiId = user.FeiId,
            Roles = user.Roles,
            Passkeys = user
                .Passkeys?.Select(x => new IdentityPasskeyDocument
                {
                    CredentialId = x.CredentialId,
                    PublicKey = x.PublicKey,
                    Name = x.Name,
                    CreatedAtUtc = x.CreatedAtUtc,
                })
                .ToList(),
        };
    }
}
