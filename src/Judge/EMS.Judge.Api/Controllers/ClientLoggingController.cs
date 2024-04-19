using Core.Application.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Controllers;

[ApiController]
[Route("client-logging")]
public class ClientLoggingController : ControllerBase
{
    private readonly ILogger _logger;

    public ClientLoggingController(ILogger logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public Task<IActionResult> Log([FromBody] ClientLogRequest request)
    {
        _logger.LogClientError(request.Functionality, request.Errors);
        return Task.FromResult((IActionResult)Ok());
    }
}
