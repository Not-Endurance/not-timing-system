using Core.Application.Rpc;
using Core.ConventionalServices;
using Core.Services;
using System.IO;

namespace EMS.Judge.Api.Services;

public class RpcClientLogger : IRpcClientLogger
{
	private readonly IFileService _fileService;

	public RpcClientLogger(IFileService fileService)
    {
		_fileService = fileService;
	}

	public void Log(RpcLog log)
	{
		var dir = $"{Directory.GetCurrentDirectory()}/logs-clients";
		var path = Path.Combine(dir, $"{log.ClientId}.txt");
		_fileService.Append(path, $"{log.DateTime}: {log.Message}");
	}
}

public interface IRpcClientLogger : ITransientService
{
	void Log(RpcLog log);
}
