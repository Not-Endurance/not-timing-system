using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Store;
using Xunit;

namespace NC.Tests.Authentication.Passkeys;

public sealed class PasskeyServiceTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    static readonly byte[] Credential = [1, 2, 3, 4];

    [Fact]
    public async Task EnrolAsync_WhenAttestationIsAccepted_ShouldStorePasskeyOnTheUser()
    {
        var users = new FakeUsers(
            new IdentityUserDocument
            {
                Id = 7,
                Email = "ana@example.test",
                SecurityStamp = "stamp",
            }
        );
        var service = CreateService(users, attestation: Passkey(Credential));

        var outcome = await service.EnrolAsync(7, "{}", "Phone", "en", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Done, outcome.Kind);
        var stored = Assert.Single(users.Stored(7).Passkeys!);
        Assert.Equal(Credential, stored.CredentialId);
    }

    [Fact]
    public async Task EnrolAsync_WhenAttestationIsRejected_ShouldRefuseAndStoreNothing()
    {
        var users = new FakeUsers(
            new IdentityUserDocument
            {
                Id = 7,
                Email = "ana@example.test",
                SecurityStamp = "stamp",
            }
        );
        var service = CreateService(users, attestation: null);

        var outcome = await service.EnrolAsync(7, "{}", "Phone", "en", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Refused, outcome.Kind);
        Assert.True(users.Stored(7).Passkeys is null or { Count: 0 });
    }

    [Fact]
    public async Task RemoveAsync_WhenItIsTheLastPasskey_ShouldRefuseAndKeepIt()
    {
        var users = new FakeUsers(UserWithPasskeys(7, Credential));
        var service = CreateService(users);

        var outcome = await service.RemoveAsync(7, Credential, CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.LastPasskey, outcome.Kind);
        Assert.Single(users.Stored(7).Passkeys!);
    }

    [Fact]
    public async Task RemoveAsync_WhenAnotherPasskeyRemains_ShouldRemoveIt()
    {
        var other = new byte[] { 9, 9 };
        var users = new FakeUsers(UserWithPasskeys(7, Credential, other));
        var service = CreateService(users);

        var outcome = await service.RemoveAsync(7, Credential, CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Done, outcome.Kind);
        var remaining = Assert.Single(users.Stored(7).Passkeys!);
        Assert.Equal(other, remaining.CredentialId);
    }

    static PasskeyService CreateService(
        FakeUsers users,
        UserPasskeyInfo? attestation = null,
        PasskeyAssertion? assertion = null
    )
    {
        var ceremony = new FakeCeremony { Attestation = attestation, Assertion = assertion };
        return new PasskeyService(
            ceremony,
            users,
            new FakeSessions(),
            new EmailDispatchQueue(),
            new FixedTime(),
            NullLogger<PasskeyService>.Instance
        );
    }

    static UserPasskeyInfo Passkey(byte[] credentialId)
    {
        return new UserPasskeyInfo(credentialId, [5, 6], Now, 0, [], true, false, false, [], []) { Name = "Phone" };
    }

    static IdentityUserDocument UserWithPasskeys(int id, params byte[][] credentialIds)
    {
        return new IdentityUserDocument
        {
            Id = id,
            Email = "ana@example.test",
            SecurityStamp = "stamp",
            Passkeys = credentialIds
                .Select(x => new IdentityPasskeyDocument
                {
                    CredentialId = x,
                    PublicKey = [1],
                    Name = "Phone",
                    CreatedAt = Now.UtcDateTime,
                })
                .ToList(),
        };
    }

    static IdentityUserDocument Copy(IdentityUserDocument user)
    {
        return new IdentityUserDocument
        {
            Id = user.Id,
            Email = user.Email,
            ProvedAt = user.ProvedAt,
            UserHandle = user.UserHandle,
            SecurityStamp = user.SecurityStamp,
            ConcurrencyStamp = user.ConcurrencyStamp,
            Name = user.Name,
            DisplayName = user.DisplayName,
            Roles = user.Roles,
            Passkeys = user
                .Passkeys?.Select(x => new IdentityPasskeyDocument
                {
                    CredentialId = x.CredentialId,
                    PublicKey = x.PublicKey,
                    Name = x.Name,
                    CreatedAt = x.CreatedAt,
                })
                .ToList(),
        };
    }

    sealed class FakeCeremony : IPasskeyCeremony
    {
        public UserPasskeyInfo? Attestation { get; init; }

        public PasskeyAssertion? Assertion { get; init; }

        public Task<string> MakeRequestOptionsAsync()
        {
            return Task.FromResult("{}");
        }

        public Task<string> MakeCreationOptionsAsync(Microsoft.AspNetCore.Identity.PasskeyUserEntity user)
        {
            return Task.FromResult("{}");
        }

        public Task<UserPasskeyInfo?> PerformAttestationAsync(string credentialJson)
        {
            return Task.FromResult(Attestation);
        }

        public Task<PasskeyAssertion?> PerformAssertionAsync(string credentialJson)
        {
            return Task.FromResult(Assertion);
        }
    }

    sealed class FakeSessions : IPasskeySessionStore
    {
        public Task CreateAsync(
            string tokenHash,
            int userId,
            string securityStamp,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            return Task.CompletedTask;
        }
    }

    sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    /// <summary>
    /// Holds users the way the store does: a change works on a copy, and the copy reaches the store only through an update.
    /// A passkey change that is never updated is therefore lost, as it would be in production.
    /// </summary>
    sealed class FakeUsers : IPasskeyUsers
    {
        readonly Dictionary<int, IdentityUserDocument> _stored = [];

        public FakeUsers(params IdentityUserDocument[] users)
        {
            foreach (var user in users)
            {
                _stored[user.Id] = Copy(user);
            }
        }

        public IdentityUserDocument Stored(int userId)
        {
            return _stored[userId];
        }

        public Task<IdentityUserDocument?> FindByUserIdAsync(int userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_stored.TryGetValue(userId, out var user) ? Copy(user) : null);
        }

        public Task<IdentityResult> ChangeAsync(
            int userId,
            Func<IdentityUserDocument, Task<IdentityResult>> change,
            CancellationToken cancellationToken = default
        )
        {
            return change(Copy(_stored[userId]));
        }

        public Task<IdentityResult> UpdateAsync(IdentityUserDocument user, CancellationToken cancellationToken)
        {
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
                    CreatedAt = passkey.CreatedAt.UtcDateTime,
                }
            );
            return Task.CompletedTask;
        }

        public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
            IdentityUserDocument user,
            CancellationToken cancellationToken
        )
        {
            IList<UserPasskeyInfo> passkeys = (user.Passkeys ?? [])
                .Select(x => new UserPasskeyInfo(
                    x.CredentialId,
                    x.PublicKey,
                    new DateTimeOffset(x.CreatedAt, TimeSpan.Zero),
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

        public Task<bool> RemovePasskeyWhileAnotherRemainsAsync(
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
    }
}
