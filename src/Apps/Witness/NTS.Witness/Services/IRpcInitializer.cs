using Not.Injection;

namespace NTS.Witness.Services;
public interface IRpcInitializer : ISingleton
{
    Task StartConnection();
    Task Disconnect();
}
