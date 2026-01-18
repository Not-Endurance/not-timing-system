namespace NTS.Witness.Blazor;

internal class WitnessBlazorConstants
{
    public static class Pages
    {
        public const string WITNESS_HOME = "/";
        public const string PROFILE = "/profile";
        public const string SIGNIN = "/signin";
        public const string SIGNOUT = "/signout";
        public const string ACCESS_DENIED = "/access-denied";
        public const string EVENTS = "/events";
        public const string EMERGENCY_CONTACTS = "/emergency-contacts";
        public const string PERFORMANCE = "/performance";
        public const string STARTLIST = "/startlist";
        public const string SNAPSHOT = "/snapshot";
    }

    public static class UserRoles
    {
        public const string ADMIN = "admin";
        public const string OFFICIAL = "official";
        public const string VET = "vet";
        public const string COMPETITOR = "competitor";
    }
}
