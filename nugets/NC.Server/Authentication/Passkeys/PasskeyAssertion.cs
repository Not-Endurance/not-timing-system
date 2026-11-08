using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.Passkeys;

public sealed class PasskeyAssertion
{
    public IdentityUserDocument User { get; init; } = new();

    public UserPasskeyInfo Passkey { get; init; } =
        new([], [], DateTimeOffset.UnixEpoch, 0, [], false, false, false, [], []);
}
