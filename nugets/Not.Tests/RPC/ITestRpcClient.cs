using Not.Application.RPC.Clients;

namespace Not.Tests.RPC;

public interface ITestRpcClient : IRpcClient, IDisposable
{
    List<string> InvokedMethods { get; }
}
