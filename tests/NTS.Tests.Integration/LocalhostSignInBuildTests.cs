namespace NTS.Tests.Integration;

/// <summary>
/// The local sign in as exists in a Debug build and in no other (#607): CI builds and tests in Release, so the host that is
/// deployed is the one these assertions are made over. The type is compiled out of a Release build, and a Release host has no
/// route or page even when it is in Development with an allow-list (the tests of a Release build, in LocalhostSignInReleaseTests).
/// </summary>
public sealed class LocalhostSignInBuildTests
{
    [Fact]
    public void The_local_sign_in_is_in_the_Api_of_a_Debug_build_and_in_no_other()
    {
        var type = typeof(Program).Assembly.GetType("NoTiming.Api.Features.Localhost.LocalhostSignIn");
        var publicEndpoints = typeof(Program).Assembly.GetType(
            "NoTiming.Api.Features.Localhost.LocalhostPublicEndpoints"
        );

#if DEBUG
        Assert.NotNull(type);
        Assert.NotNull(publicEndpoints);
#else
        Assert.Null(type);
        Assert.Null(publicEndpoints);
#endif
    }
}
