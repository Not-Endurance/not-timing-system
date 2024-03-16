using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface ILoggingClientProcedures
{
	Task<IEnumerable<RpcLog>> SendLogs();
}
 