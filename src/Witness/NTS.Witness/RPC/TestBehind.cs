namespace NTS.Witness.RPC;

public class TestBehind : ITestBehind
{
    readonly WitnessRpcClient _rpcClient;

    public TestBehind(WitnessRpcClient rpcClient)
    {
        _rpcClient = rpcClient;
    }

    public void Test()
    {
        _rpcClient.EnsureInitialized();
    }
}
