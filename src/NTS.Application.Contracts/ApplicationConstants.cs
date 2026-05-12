namespace NTS.Application.Contracts;

public static class ApplicationConstants
{
    public const string VERSION = "1.2.1";
    public const string VERSION_STRING = "NTS v" + VERSION;
    public const int NETWORK_BROADCAST_PORT = 21337;
    public const string JUDGE_HUB = "judge-hub";
    public const string WITNESS_HUB = "witness-hub";
    public const int RPC_PORT = 11337;

    public static class Apps
    {
        public const string JUDGE = "Judge";
        public const string WITNESS = "Witness";
    }
}
