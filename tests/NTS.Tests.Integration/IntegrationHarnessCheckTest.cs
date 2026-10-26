using Microsoft.Extensions.Localization;
using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using NTS.Contracts.API;
using NTS.Contracts.Arrivelists;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Profile;
using NTS.Contracts.Presentlists;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Localization;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
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
    public async Task Witness_contexts_pick_up_the_user_when_signin_completes_after_startup()
    {
        var signingInUser = new IntegrationUser(
            "late-signin.witness@integration.test",
            "late-signin-witness-user",
            "Late Signin Witness"
        );
        await using var witness = new ViewerDriver(_fixture, "IntegrationLateSigninWitness");
        var profileContext = witness.GetRequiredService<IWitnessProfileContext>();
        var accessContext = witness.GetRequiredService<IWitnessAccessContext>();

        // The app starts as a visitor, so both contexts initialize before anyone is signed in.
        await profileContext.Load();
        await accessContext.Load();

        Assert.Null(profileContext.Profile);
        Assert.Equal(WitnessSays.Visitor, WitnessSays.Of(accessContext));

        await witness.SignIn(signingInUser);

        var profile = await WaitForProfile(profileContext);
        Assert.Equal("Late", profile.GivenName);
        Assert.Equal("Late", profileContext.WelcomeName);
        Assert.Equal(signingInUser.Email, witness.GetRequiredService<IAccountSession>().Current!.Email);
        Assert.Equal(WitnessSays.Registered, await WaitForWitnessToSay(accessContext, WitnessSays.Registered));
    }

    static async Task<AccountProfile> WaitForProfile(IWitnessProfileContext profileContext)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (profileContext.Profile != null)
            {
                return profileContext.Profile;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "Witness profile context did not pick up the signed-in user, so the drawer would keep "
                + "rendering without its profile header until a full page reload."
        );
    }

    static async Task<WitnessSays> WaitForWitnessToSay(IWitnessAccessContext accessContext, WitnessSays expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (WitnessSays.Of(accessContext) == expected)
            {
                return expected;
            }

            await Task.Delay(50);
        }

        return WitnessSays.Of(accessContext);
    }
}
