using Not.Application.CRUD.Ports;

namespace Not.Application.HTTP;

/// <summary>
/// The error of a request that the Api answered and refused (ADR-0017). It is a <see cref="RepositoryException"/>, so a caller
/// that does not need the answer catches the base type, and a caller that does, such as one that reads <c>Retry-After</c>,
/// reads <see cref="Response"/>: the status, the headers, the document and the error. The kind comes from the status, and
/// from the code for a conflict that the Api says to try again later.
/// </summary>
public class JsonApiException : RepositoryException
{
    public JsonApiException(JsonApiResponse response, IReadOnlySet<string> unavailableCodes)
        : base(
            KindOf(response, unavailableCodes),
            response.Error?.Message ?? response.Status.ToString(),
            response.Error?.Code
        )
    {
        Response = response;
    }

    public JsonApiResponse Response { get; }

    static RepositoryErrorKind KindOf(JsonApiResponse response, IReadOnlySet<string> unavailableCodes)
    {
        var status = (int)response.Status;
        if (status is 401 or 403)
        {
            return RepositoryErrorKind.NotAllowed;
        }

        if (status is 408 or 429 or >= 500)
        {
            return RepositoryErrorKind.Unavailable;
        }

        if (response.Error?.Code is { } code && unavailableCodes.Contains(code))
        {
            return RepositoryErrorKind.Unavailable;
        }

        return RepositoryErrorKind.Refused;
    }
}
