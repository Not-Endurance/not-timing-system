namespace NC.Server.Authentication.Codes;

/// <summary>The two purposes a code challenge can have. The purpose is stored with the challenge.</summary>
public static class CodeChallengePurpose
{
    public const string SIGN_IN = "sign-in";
    public const string REGISTER = "register";
}
