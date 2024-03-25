using Core.Utilities;
using System.Reflection;

namespace Core.Application;

public static class CoreApplicationConstants
{
    public const int NETWORK_API_PORT = 11337;
    public const int NETWORK_BROADCAST_PORT = 21337;

    public static class Apps
    {
        public const string JUDGE = "Judge";
        public const string WITNESS = "Witness";
    }

    public static class NetworkPorts
    {
        public const int JUDGE_SERVER = 11337;
    }

    public static class RpcEndpoints
    {
        public const string STARTLIST = "startlist-hub";
        public const string WITNESS_EVENTS = "witness-events-hub";
        public const string PARTICIPANTS = "participants-hub";
        public const string LOGGING = "logging-hub";
    }

    public static class Api
    {
        public const string WITNESS = "witness";
        public const string STARTLIST = "startlist";
        public const string STARTLIST_ENTRY = "ReceiveStartlistEntry";
    }

    public static Assembly[] Assemblies
        => ReflectionUtilities.GetAssemblies("Core.Application");
}
