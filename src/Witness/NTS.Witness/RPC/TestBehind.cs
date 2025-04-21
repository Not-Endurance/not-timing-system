using Not.Application.RPC.SignalR;

namespace NTS.Witness.RPC;

    public class TestBehind : ITestBehind
    {
        readonly IRpcSocket _rpcSocket;

        public TestBehind(IRpcSocket rpcSocket)
        {
            _rpcSocket = rpcSocket;
        }

        public void Test()
        {
        }
}
