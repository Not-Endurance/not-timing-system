using Core.ConventionalServices;
using Core.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Services;

public class RpcClientLogger : IRpcClientLogger
{
	private readonly IFileService _fileService;

	public RpcClientLogger(IFileService fileService)
    {
		_fileService = fileService;
	}

    public Task Log(string clientId, string message)
	{
		InternalLog(clientId, message);
		return Task.CompletedTask;
	}

	public Task Log(string clientId, Exception exception)
	{
		var message = exception.Message + Environment.NewLine + exception.StackTrace;
		InternalLog(clientId, message);
		return Task.CompletedTask;
	}

	private void InternalLog(string clientId, string message)
	{
		message = $"{DateTimeOffset.Now}_{clientId}: {message}";

		var dir = $"{Directory.GetCurrentDirectory()}/logs-clients";
		var path = Path.Combine(dir, "rpc-log.txt");
		_fileService.Append(path, message);
	}
}

public interface IRpcClientLogger : ITransientService
{
	Task Log(string clientId, string message);
	Task Log(string clientId, Exception exception);
}
