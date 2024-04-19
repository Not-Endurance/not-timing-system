using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Core.Application.Http;

public class ClientLogRequest
{
    public ClientLogRequest(string? functionality, List<Error> errors)
    {
        Functionality = functionality;
        Errors = errors;
    }

    public string? Functionality { get; private set; }
    public List<Error> Errors { get; private set; } = [];

    private static ClientLogRequest Create(string functionality, Exception ex)
    {
        var result = new ClientLogRequest(functionality, []);
        AddExceptions(result, ex);
        return result;
    }

    private static void AddExceptions(ClientLogRequest request, Exception ex)
    {
        var error = new Error(ex.Message, ex.StackTrace);
        request.Errors.Add(error);
        if (ex.InnerException != null)
        {
            AddExceptions(request, ex.InnerException);
        }
    }
}

public class Error
{
    public Error(string message, string stackTrace)
    {
        Message = message;
        StackTrace = stackTrace;
    }

    public string Message { get; set; }
    public string StackTrace { get; set; }
}
