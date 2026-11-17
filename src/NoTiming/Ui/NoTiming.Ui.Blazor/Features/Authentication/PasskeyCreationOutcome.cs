namespace NoTiming.Ui.Blazor.Features.Authentication;

/// <summary>How creating a passkey ended.</summary>
public enum PasskeyCreationOutcome
{
    /// <summary>The passkey is stored for the signed-in person.</summary>
    Created = 1,

    /// <summary>The person did not accept the warning about shared devices, so nothing was asked of the browser.</summary>
    Declined = 2,

    /// <summary>The browser's ceremony was cancelled or failed, so nothing is stored.</summary>
    Cancelled = 3,

    /// <summary>This browser cannot use passkeys.</summary>
    Unsupported = 4,

    /// <summary>The session has ended, so the passkey cannot be stored.</summary>
    NotSignedIn = 5,
}
