using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Passkeys;

internal sealed class PasskeyAssertion
{
    public IdentityUserDocument User { get; init; } = new();

    public UserPasskeyInfo Passkey { get; init; } =
        new([], [], DateTimeOffset.UnixEpoch, 0, [], false, false, false, [], []);
}
