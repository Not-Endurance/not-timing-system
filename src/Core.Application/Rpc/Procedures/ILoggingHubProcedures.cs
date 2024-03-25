using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface ILoggingHubProcedures
{
	Task ReceiveLog(RpcLog log);
}
 