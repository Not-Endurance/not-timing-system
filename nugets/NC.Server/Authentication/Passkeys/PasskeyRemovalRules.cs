namespace NC.Server.Authentication.Passkeys;

public static class PasskeyRemovalRules
{
    /// <summary>A user keeps at least one passkey, so removing one is only possible while another remains.</summary>
    public static bool CanRemove(int passkeyCount)
    {
        return passkeyCount > 1;
    }
}
