using NoTiming.Api.Features.Events;
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
            GrantLimitReachedException => new JsonApiFailure(
                StatusCodes.Status422UnprocessableEntity,
                "grant-limit-reached",
                $"An Event has at most {EventGrantStore.MAX_GRANTS_PER_EVENT} grants: remove one that is not needed."
            ),
            TenantCountryMissingException => new JsonApiFailure(
                StatusCodes.Status409Conflict,
                "tenant-country-missing",
                "The country of the Tenant is not among the countries, so no Event can be made in it."
            ),
            _ => null,
        };
    }
}
