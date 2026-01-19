using Not.Injection;

namespace NTS.Witness.Services;
public interface IRpcInitializer : ISingleton
{
    bool IsConnected();
    Task StartConnection();
    Task Disconnect();
}
