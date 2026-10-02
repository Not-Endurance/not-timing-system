using System.Security.Claims;
using Microsoft.Extensions.Localization;
using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using NTS.Contracts.Arrivelists;
using NTS.Contracts.Core;
using NTS.Contracts.Presentlists;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Localization;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using NTS.Contracts.API;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Performance;
using NTS.Contracts.Features.Profile;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupOfficial = NTS.Domain.Setup.Aggregates.ConfigureEvents.Official;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;
using SetupUser = NTS.Domain.Setup.Aggregates.User;
using WitnessSnapshot = NTS.Domain.Core.Objects.Snapshots.Snapshot;
using WitnessSnapshotService = NTS.Contracts.Features.Snapshots.ISnapshotService;

namespace NTS.Tests.Integration;

public sealed partial class IntegrationHarnessCheckTest : IClassFixture<NtsIntegrationFixture>
{
    static readonly IntegrationUser OFFICIAL_USER = new(
        "official.witness@integration.test",
        "official-witness-user",
        "Official Witness"
    );
    static readonly IntegrationUser REGISTERED_USER = new(
        "registered.witness@integration.test",
        "registered-witness-user",
        "Registered Witness"
    );

    readonly NtsIntegrationFixture _fixture;

    public IntegrationHarnessCheckTest(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Witness_registration_resolution_creates_missing_nexus_user()
    {
        var registeringUser = new IntegrationUser(
            "registering.witness@integration.test",
            "registering-witness-user",
            "Rosa Maria Register",
            "Rosa",
            "Maria",
            "Register",
            "Bulgaria",
            "Konarche",
            "10101010",
            "Rosa Display"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);
        await using var witness = new WitnessDriver(
            _fixture.ApiBaseUrl,
            _fixture.NexusBaseUrl,
            registeringUser,
            "IntegrationRegisteringWitness"
        );
        var resolver = witness.GetRequiredService<NUserResolver>();
        var principal = CreatePrincipal(registeringUser);
        var profile = new NUserRegistrationProfile(
            registeringUser.Name,
            registeringUser.GivenName,
            registeringUser.MiddleName,
            registeringUser.Surname,
            registeringUser.Club,
            registeringUser.FeiId,
            registeringUser.DisplayName
        );

        Assert.Null(await api.ReadUser(registeringUser.Email));

        var result = await resolver.ResolvePrincipal(principal, profile);

        Assert.True(result.IsSuccess, result.Error);
        var created = await api.ReadUser(registeringUser.Email);
        Assert.NotNull(created);
        Assert.Equal(registeringUser.Email, created!.Email);
        Assert.Equal(registeringUser.Name, created.Name);
        Assert.Equal(registeringUser.DisplayName, created.DisplayName);
        Assert.Equal(registeringUser.GivenName, created.GivenName);
        Assert.Equal(registeringUser.MiddleName, created.MiddleName);
        Assert.Equal(registeringUser.Surname, created.Surname);
        Assert.Equal(registeringUser.CountryRegion, created.CountryRegion);
        Assert.Equal(registeringUser.Club, created.Club);
        Assert.Equal(registeringUser.FeiId, created.FeiId);
    }

    [Fact]
    public async Task Witness_profile_update_completes_existing_email_only_user()
    {
        var profileUser = new IntegrationUser(
            "profile-completion.witness@integration.test",
            "profile-completion-witness-user",
            "Profile Completion"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var registered = await api.RegisterUser(profileUser);

        Assert.Equal(profileUser.Email, registered.Email);
        Assert.Null(registered.GivenName);
        Assert.Null(registered.Surname);
        Assert.Null(registered.CountryRegion);

        var updated = await api.UpdateUserProfile(
            profileUser.Email,
            new UpdateUserProfilePayload("Petra", "Profile", "Bulgaria", club: "Konarche", feiId: "20202020")
        );
        var persisted = await api.ReadUser(profileUser.Email);

        Assert.Equal(registered.Id, updated.Id);
        Assert.Equal(profileUser.Email, updated.Email);
        Assert.Equal("Petra Profile", updated.Name);
        Assert.Equal("Petra", updated.GivenName);
        Assert.Equal("Profile", updated.Surname);
        Assert.Equal("Bulgaria", updated.CountryRegion);
        Assert.Equal("Konarche", updated.Club);
        Assert.Equal("20202020", updated.FeiId);
        Assert.NotNull(persisted);
        Assert.Equal(updated.Id, persisted!.Id);
        Assert.Equal(updated.Name, persisted.Name);
        Assert.Equal(updated.CountryRegion, persisted.CountryRegion);
    }

    [Fact]
    public async Task Witness_contexts_pick_up_the_user_when_signin_completes_after_startup()
    {
        var signingInUser = new IntegrationUser(
            "late-signin.witness@integration.test",
            "late-signin-witness-user",
            "Late Signin Witness"
        );
        await using var witness = new WitnessDriver(
            _fixture.ApiBaseUrl,
            _fixture.NexusBaseUrl,
            user: null,
            "IntegrationLateSigninWitness"
        );
        var profileContext = witness.GetRequiredService<IWitnessProfileContext>();
        var accessContext = witness.GetRequiredService<IWitnessAccessContext>();

        // The sign-in round trip hands the browser back to a freshly booted app that is still
        // anonymous, so both contexts initialize before anyone is signed in.
        await profileContext.Load();
        await accessContext.Load();

        Assert.Null(profileContext.User);
        Assert.Equal(WitnessAccessLevel.Anonymous, accessContext.AccessLevel);

        witness.SignIn(signingInUser);

        var user = await WaitForProfileUser(profileContext);
        Assert.Equal(signingInUser.Email, user.Email);
        Assert.Equal(
            WitnessAccessLevel.Registered,
            await WaitForAccessLevel(accessContext, WitnessAccessLevel.Registered)
        );
    }

    static async Task<NUserModel> WaitForProfileUser(IWitnessProfileContext profileContext)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (profileContext.User != null)
            {
                return profileContext.User;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "Witness profile context did not pick up the signed-in user, so the drawer would keep "
                + "rendering without its profile header until a full page reload."
        );
    }

    static async Task<WitnessAccessLevel> WaitForAccessLevel(
        IWitnessAccessContext accessContext,
        WitnessAccessLevel expected
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (accessContext.AccessLevel == expected)
            {
                return accessContext.AccessLevel;
            }

            await Task.Delay(50);
        }

        return accessContext.AccessLevel;
    }

    static ClaimsPrincipal CreatePrincipal(IntegrationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new("oid", user.UserIdentifier),
            new("name", user.DisplayName ?? user.Name),
        };

        AddClaim(claims, ClaimTypes.GivenName, user.GivenName);
        AddClaim(claims, "middle_name", user.MiddleName);
        AddClaim(claims, ClaimTypes.Surname, user.Surname);
        AddClaim(claims, ClaimTypes.Country, user.CountryRegion);

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "IntegrationTest"));
    }

    static void AddClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}
