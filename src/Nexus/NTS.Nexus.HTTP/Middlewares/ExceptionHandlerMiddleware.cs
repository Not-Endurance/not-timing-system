using System.Dynamic;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using MongoDB.Bson;

namespace NTS.Nexus.HTTP.Middlewares;

public class ExceptionHandlerMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {

            if (request != null)
            {
                HttpResponseData? response;
                if (ex is ApplicationException)
                {
                    response = request.CreateResponse(HttpStatusCode.BadRequest);
                    var errorMessage = new { Message = "Invalid request", Exception = ex.Message };
                    var responseBody = errorMessage.ToJson();
                    await response.WriteStringAsync(responseBody);
                }
                else
                {
                    response = request.CreateResponse(HttpStatusCode.InternalServerError);
                    var errorMessage = new { Message = "An unexpected error occurred.", Exception = ex.Message };
                    var responseBody = errorMessage.ToJson();
                    await response.WriteStringAsync(responseBody);
                }
                context.GetInvocationResult().Value = response;
                // context.Items["HttpResponseData"] = response;
            }
        }
    }
}
