using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace NTS.Nexus.HTTP.Functions;

public class CorsFunctions
{
    [Function("cors-preflight")]
    public IActionResult Preflight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*path}")] HttpRequest _
    )
    {
        return new NoContentResult();
    }
}
