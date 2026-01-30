using Not.Injection;

namespace NTS.Witness.Services;

public interface IConnectionStatus : ISingleton
{
    bool IsConnected();
}
