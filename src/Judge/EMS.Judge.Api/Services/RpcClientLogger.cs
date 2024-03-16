using Core.Application.Rpc;
using Core.ConventionalServices;
using Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EMS.Judge.Api.Services;

public class RpcClientLogger : IRpcClientLogger
{
	private readonly IFileService _fileService;

	public RpcClientLogger(IFileService fileService)
    {
		_fileService = fileService;
	}

	public void Log(IEnumerable<RpcLog> logs)
	{
		var sb = new StringBuilder();
		foreach (var log in logs.OrderBy(x => x.DateTime))
		{
			sb.AppendLine($"{log.DateTime}_{log.ClientId}: {log.Message}");
		}

		var dir = $"{Directory.GetCurrentDirectory()}/logs-clients";
		var path = Path.Combine(dir, "rpc-log.txt");
		_fileService.Append(path, sb.ToString());
	}
}

public interface IRpcClientLogger : ITransientService
{
	void Log(IEnumerable<RpcLog> logs);
}
