using System;
using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface ILoggingHubProcedures
{
	Task Log(string clientId, string message);
	Task LogError(string clientId, Exception exception);
}
