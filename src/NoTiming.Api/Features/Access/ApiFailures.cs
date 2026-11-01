using MongoDB.Driver.Linq;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// What a handler throws that the Api has an answer for, and the answer, said once (ADR-0008, #671): the endpoint filter maps
/// the exception, so that no handler catches it only to build the same answer, and one that is not here is a mistake of the
/// host and stays an exception.
/// </summary>
internal static class ApiFailures
{
    public static JsonApiFailure? Of(Exception exception)
    {
        return exception switch
        {
            GrantLimitReachedException => EventsFailures.GrantLimitReached,
            TenantCountryMissingException => EventsFailures.TenantCountryMissing,
            // A filter that is valid in the grammar and that the data cannot run is for the caller to correct (the rest-api
            // skill), on every list.
            ExpressionNotSupportedException => JsonApiFailures.ExpressionNotSupported,
            UnrunnableExpressionException => JsonApiFailures.ExpressionUnrunnable,
            _ => null,
        };
    }
}
