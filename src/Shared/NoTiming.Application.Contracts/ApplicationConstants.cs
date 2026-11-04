namespace NoTiming.Application.Contracts;

public static class ApplicationConstants
{
    public const string VERSION = "1.3.2";
    public const string VERSION_STRING = "NTS v" + VERSION;
    public const int NETWORK_BROADCAST_PORT = 21337;
    public const string EVENT_BUS = "event-bus";
    public const string UI = "ui";
    public const string JUDGE_HUB = "judge";
    public const string WITNESS_HUB = "witness";
    public const int RPC_PORT = 11337;
    public const string NO_TIMING_SYSTEM = "NoTiming";

    // A hub path below the server root, for example "event-bus/judge".
    public static string EventBusHub(string hub)
    {
        return $"{EVENT_BUS}/{hub}";
    }

    public static class Apps
    {
        public const string JUDGE = "Judge";
        public const string WITNESS = "Witness";
    }
}
