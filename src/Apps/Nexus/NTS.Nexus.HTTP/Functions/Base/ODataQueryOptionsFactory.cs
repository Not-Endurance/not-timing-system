using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace NTS.Nexus.HTTP.Functions.Base;

internal static class ODataQueryOptionsFactory
{
    static readonly ConcurrentDictionary<Type, IEdmModel> MODELS = new();
    static readonly ODataValidationSettings VALIDATION_SETTINGS = new()
    {
        AllowedQueryOptions = AllowedQueryOptions.Filter,
    };

    public static ODataQueryOptions<T> Create<T>(HttpRequest request)
        where T : class
    {
        var queryRequest = CreateQueryRequest(request);
        var context = new ODataQueryContext(GetModel<T>(), typeof(T), path: null);
        var options = new ODataQueryOptions<T>(context, queryRequest);
        options.Validate(VALIDATION_SETTINGS);
        return options;
    }

    static HttpRequest CreateQueryRequest(HttpRequest source)
    {
        // OData expects a normal ASP.NET Core request; isolate it from Azure Functions host features.
        var context = new DefaultHttpContext { RequestServices = source.HttpContext.RequestServices };
        var request = context.Request;
        request.Method = source.Method;
        request.Scheme = source.Scheme;
        request.Host = source.Host;
        request.PathBase = source.PathBase;
        request.Path = source.Path;
        request.QueryString = source.QueryString;
        return request;
    }

    static IEdmModel GetModel<T>()
        where T : class
    {
        return MODELS.GetOrAdd(typeof(T), _ =>
        {
            var builder = new ODataConventionModelBuilder();
            var entitySet = builder.EntitySet<T>(typeof(T).Name);
            entitySet.EntityType.Filter();
            return builder.GetEdmModel();
        });
    }
}
