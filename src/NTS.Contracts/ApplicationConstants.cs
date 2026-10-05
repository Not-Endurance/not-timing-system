namespace NTS.Contracts;

/// <summary>
/// What the hosts and the clients share. The write header is the one a client sends on every write to the Api (#602,
/// ADR-0008): a browser sends one to another origin only after the origin has allowed it, so another site cannot write
/// with a visitor's cookie.
/// </summary>
public static class ApplicationConstants
{
    public const string VERSION = "1.3.3";
    public const string VERSION_STRING = "NTS v" + VERSION;
    public const int NETWORK_BROADCAST_PORT = 21337;
    public const string LIVE_HUB = "live-hub";
    public const int RPC_PORT = 11337;
    public const string NO_TIMING_SYSTEM = "NoTiming";
    public const string WRITE_HEADER = "X-Requested-With";
    public const string WRITE_HEADER_VALUE = NO_TIMING_SYSTEM;

    public static class Apps
    {
        public const string JUDGE = "Judge";
        public const string WITNESS = "Witness";
    }
}
