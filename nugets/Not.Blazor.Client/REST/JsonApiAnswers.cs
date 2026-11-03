using System.Net.Http;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Localization;

namespace Not.Blazor.Client.REST;

/// <summary>
/// What a request to the Api answers, as every repository of the client reads it (ADR-0017): an answer the Api took, a refusal
/// as a <see cref="JsonApiException"/> with what the Api said, and a request that got no answer as an unavailable one. The
/// repositories of a resource and the classes of a non-CRUD route share it, so that a refusal reads the same wherever it is.
/// </summary>
public static class JsonApiAnswers
{
    /// <summary>The answer of a request, whatever it says; a request that got no answer is unavailable.</summary>
    public static Task<JsonApiResponse> Reached(Task<JsonApiResponse> request)
    {
        return Reached(() => request);
    }

    /// <summary>The answer of a request, whatever it says: a request that got no answer is unavailable, with the transport exception inside it.</summary>
    public static async Task<T> Reached<T>(Func<Task<T>> request)
    {
        try
        {
            return await request();
        }
        catch (HttpRequestException ex)
        {
            throw new RepositoryException(
                RepositoryErrorKind.Unavailable,
                NStrings.Cannot_connect_to_server_string,
                null,
                ex
            );
        }
    }

    /// <summary>The answer of a request the Api took; a refusal is thrown with what the Api said, and its code.</summary>
    public static async Task<JsonApiResponse> Taken(
        Task<JsonApiResponse> request,
        IReadOnlySet<string> unavailableCodes
    )
    {
        var response = await Reached(request);
        return response.IsSuccess ? response : throw new JsonApiException(response, unavailableCodes);
    }
}
