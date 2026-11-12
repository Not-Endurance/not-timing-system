using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NC.Localization;
using NC.Localization.Localizers;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Passkeys;
using NC.Server.Tests.Authentication.Fakes;
using Xunit;

namespace NC.Server.Tests.Authentication.Passkeys;

// Shares a collection with the email tests, because both set the localizer, which is static.
[Collection("Localization")]
public sealed class PasskeyServiceTests : IDisposable
{
    static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    static readonly byte[] Credential = [1, 2, 3, 4];

    public PasskeyServiceTests()
    {
        LocalizationHelper.Configure(new DummyLocalizer());
    }

    public void Dispose()
    {
        LocalizationHelper.Clear();
    }

    [Fact]
    public async Task Enrol_WhenAttestationIsAccepted_ShouldStorePasskeyOnTheUser()
    {
        var users = new FakeIdentityUsers(
            new IdentityUserDocument
            {
                Id = 7,
                Email = "ana@example.test",
                SecurityStamp = "stamp",
            }
        );
        var service = CreateService(users, attestation: Passkey(Credential));

        var outcome = await service.Enrol(new DefaultHttpContext(), 7, "{}", "Phone", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Done, outcome.Kind);
        var stored = Assert.Single(users.Stored(7).Passkeys!);
        Assert.Equal(Credential, stored.CredentialId);
    }

    [Fact]
    public async Task Enrol_WhenAttestationIsRejected_ShouldRefuseAndStoreNothing()
    {
        var users = new FakeIdentityUsers(
            new IdentityUserDocument
            {
                Id = 7,
                Email = "ana@example.test",
                SecurityStamp = "stamp",
            }
        );
        var service = CreateService(users, attestation: null);

        var outcome = await service.Enrol(new DefaultHttpContext(), 7, "{}", "Phone", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Refused, outcome.Kind);
        Assert.True(users.Stored(7).Passkeys is null or { Count: 0 });
    }

    [Fact]
    public async Task Remove_WhenItIsTheLastPasskey_ShouldRefuseAndKeepIt()
    {
        var users = new FakeIdentityUsers(UserWithPasskeys(7, Credential));
        var service = CreateService(users);

        var outcome = await service.Remove(7, Credential, CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.LastPasskey, outcome.Kind);
        Assert.Single(users.Stored(7).Passkeys!);
    }

    [Fact]
    public async Task Remove_WhenAnotherPasskeyRemains_ShouldRemoveIt()
    {
        var other = new byte[] { 9, 9 };
        var users = new FakeIdentityUsers(UserWithPasskeys(7, Credential, other));
        var service = CreateService(users);

        var outcome = await service.Remove(7, Credential, CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Done, outcome.Kind);
        var remaining = Assert.Single(users.Stored(7).Passkeys!);
        Assert.Equal(other, remaining.CredentialId);
    }

    [Fact]
    public async Task Remove_WhenTheUserHasNoSuchPasskey_ShouldAnswerNotFound()
    {
        var users = new FakeIdentityUsers(UserWithPasskeys(7, Credential, new byte[] { 9, 9 }));
        var service = CreateService(users);

        var outcome = await service.Remove(7, new byte[] { 0, 0 }, CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.NotFound, outcome.Kind);
        Assert.Equal(2, users.Stored(7).Passkeys!.Count);
    }

    [Fact]
    public async Task Rename_WhenThePasskeyExists_ShouldRenameIt()
    {
        var users = new FakeIdentityUsers(UserWithPasskeys(7, Credential));
        var service = CreateService(users);

        var outcome = await service.Rename(7, Credential, "  Work phone  ", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.Done, outcome.Kind);
        Assert.Equal("Work phone", outcome.Name);
        Assert.Equal("Work phone", Assert.Single(users.Stored(7).Passkeys!).Name);
    }

    [Fact]
    public async Task Rename_WhenTheUserHasNoSuchPasskey_ShouldAnswerNotFound()
    {
        var users = new FakeIdentityUsers(UserWithPasskeys(7, Credential));
        var service = CreateService(users);

        var outcome = await service.Rename(7, new byte[] { 0, 0 }, "Work phone", CancellationToken.None);

        Assert.Equal(PasskeyOutcomeKind.NotFound, outcome.Kind);
        Assert.Equal("Phone", Assert.Single(users.Stored(7).Passkeys!).Name);
    }

    static PasskeyService CreateService(FakeIdentityUsers users, UserPasskeyInfo? attestation = null)
    {
        var ceremony = new FakePasskeyCeremony { Attestation = attestation };
        return new PasskeyService(
            ceremony,
            users,
            new EmailDispatchQueue(),
            new EmailLanguage(["en"]),
            new FixedTime(Now),
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
                    CreatedAtUtc = Now.UtcDateTime,
                })
                .ToList(),
        };
    }
}
