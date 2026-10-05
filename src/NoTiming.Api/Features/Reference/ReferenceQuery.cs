using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// What the query string of a list of reference data asks for (ADR-0008, the rest-api skill): <c>filter</c>, one
/// expression of the OData grammar that the OData package applies to MongoDB, <c>sort</c>, members with a <c>-</c> for
/// descending, and <c>page[size]</c> with <c>page[number]</c>. Any other parameter is refused, and so is an expression
/// that is not valid or names a member the resource does not have. The members are the ones the resource shows, in
/// camelCase, and a member the resource keeps to itself cannot be filtered or sorted on, which would tell what it holds.
/// </summary>
internal sealed partial class ReferenceQuery<T>
    where T : class
{
    const string FILTER = "filter";
    const string SORT = "sort";
    const string PAGE_SIZE = "page[size]";
    const string PAGE_NUMBER = "page[number]";
    static readonly ConcurrentDictionary<string, IEdmModel> MODELS = new();
    public const int DEFAULT_PAGE_SIZE = 100;
    public const int MAX_PAGE_SIZE = 500;

    public static ReferenceQuery<T> Read(HttpRequest request, IReadOnlyCollection<string> hiddenMembers)
    {
        foreach (var parameter in request.Query.Keys)
        {
            if (parameter is not (FILTER or SORT or PAGE_SIZE or PAGE_NUMBER))
            {
                return Refused(
                    StatusCodes.Status400BadRequest,
                    "unsupported-parameter",
                    "The parameter is not supported.",
                    parameter
                );
            }
        }

        var size = DEFAULT_PAGE_SIZE;
        var number = 1;
        if (
            !TryPage(request, PAGE_SIZE, MAX_PAGE_SIZE, ref size)
            || !TryPage(request, PAGE_NUMBER, int.MaxValue / MAX_PAGE_SIZE, ref number)
        )
        {
            return Refused(
                StatusCodes.Status400BadRequest,
                "invalid-page",
                "A page is a number of at least 1 and a size of 1 to 500."
            );
        }

        var filter = request.Query.TryGetValue(FILTER, out var filterValues) ? filterValues.ToString().Trim() : null;
        var orderBy = ToOrderBy(request.Query.TryGetValue(SORT, out var sortValues) ? sortValues.ToString() : null);
        if (orderBy == "")
        {
            return Refused(
                StatusCodes.Status400BadRequest,
                "invalid-sort",
                "Sort by members, with a - for descending: sort=-name,id."
            );
        }

        var model = ModelOf(hiddenMembers);
        if (filter == "")
        {
            return Refused(StatusCodes.Status400BadRequest, "invalid-filter", "The filter is empty.");
        }

        if (filter != null && !TryBuild(request, model, "$filter", filter, out _))
        {
            return Refused(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "The filter is not valid.",
                "Use an expression of the OData filter grammar over the members of the resource."
            );
        }

        if (orderBy != null && !TryBuild(request, model, "$orderby", orderBy, out _))
        {
            return Refused(
                StatusCodes.Status400BadRequest,
                "invalid-sort",
                "The sort is not valid.",
                "Sort by the members of the resource, with a - for descending: sort=-name,id."
            );
        }

        ODataQueryOptions<T>? options = null;
        if (filter != null || orderBy != null)
        {
            TryBuild(request, model, filter != null ? "$filter" : "$orderby", filter ?? orderBy!, out options, orderBy);
        }

        return new ReferenceQuery<T>(options, size, number, null);
    }

    ReferenceQuery(ODataQueryOptions<T>? options, int size, int number, IResult? refusal)
    {
        Options = options;
        Size = size;
        Number = number;
        Refusal = refusal;
    }

    /// <summary>The filter and the sort, or none when neither was asked for.</summary>
    public ODataQueryOptions<T>? Options { get; }

    public int Size { get; }
    public int Number { get; }
    public IResult? Refusal { get; }

    /// <summary>How many rows to leave out and how many to read: one more than a page, to know whether another follows.</summary>
    public int Skip => (Number - 1) * Size;

    public int Take => Size + 1;

    static ReferenceQuery<T> Refused(int status, string code, string title, string? detail = null)
    {
        return new ReferenceQuery<T>(null, DEFAULT_PAGE_SIZE, 1, JsonApiResults.Error(status, code, title, detail));
    }

    static bool TryPage(HttpRequest request, string parameter, int max, ref int value)
    {
        if (!request.Query.TryGetValue(parameter, out var values))
        {
            return true;
        }

        if (!int.TryParse(values.ToString(), out var parsed) || parsed < 1 || parsed > max)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>
    /// The sort as OData writes it, null when there is none, and an empty text when it is not a list of members.
    /// </summary>
    static string? ToOrderBy(string? sort)
    {
        if (sort == null)
        {
            return null;
        }

        var terms = new List<string>();
        foreach (var raw in sort.Split(','))
        {
            var descending = raw.StartsWith('-');
            var member = (descending ? raw[1..] : raw).Trim();
            if (!MemberPath().IsMatch(member))
            {
                return "";
            }

            terms.Add(descending ? $"{member} desc" : member);
        }

        return string.Join(',', terms);
    }

    /// <summary>
    /// Builds the options the OData package applies from a query string of its own, over the model of the resource. The
    /// package throws when the expression is not valid, which is told by the false.
    /// </summary>
    static bool TryBuild(
        HttpRequest source,
        IEdmModel model,
        string option,
        string value,
        out ODataQueryOptions<T> options,
        string? orderBy = null
    )
    {
        try
        {
            var context = new DefaultHttpContext { RequestServices = source.HttpContext.RequestServices };
            var query = new StringBuilder("?").Append(option).Append('=').Append(Uri.EscapeDataString(value));
            if (orderBy != null && option != "$orderby")
            {
                query.Append("&$orderby=").Append(Uri.EscapeDataString(orderBy));
            }

            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/";
            context.Request.QueryString = new QueryString(query.ToString());
            var queryContext = new ODataQueryContext(model, typeof(T), path: null);

            // Filtering and sorting are allowed on every member the model has, the members of what a resource embeds
            // included, and on nothing else: the model is where a member is left out.
            queryContext.DefaultQueryConfigurations.EnableFilter = true;
            queryContext.DefaultQueryConfigurations.EnableOrderBy = true;
            options = new ODataQueryOptions<T>(queryContext, context.Request);
            options.Validate(
                new ODataValidationSettings
                {
                    AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy,
                }
            );
            return true;
        }
        catch (Exception ex) when (ex is ODataException or ArgumentException or InvalidOperationException)
        {
            options = default!;
            return false;
        }
    }

    /// <summary>The model of the resource: its members in camelCase, with the ones it keeps to itself left out.</summary>
    static IEdmModel ModelOf(IReadOnlyCollection<string> hiddenMembers)
    {
        return MODELS.GetOrAdd(
            string.Join(',', hiddenMembers.Order(StringComparer.Ordinal)),
            _ =>
            {
                var builder = new ODataConventionModelBuilder();
                builder.EnableLowerCamelCase();
                builder.EntitySet<T>(typeof(T).Name);
                var type = builder.StructuralTypes.Single(x => x.ClrType == typeof(T));
                foreach (var hidden in hiddenMembers)
                {
                    var property = typeof(T).GetProperty(hidden);
                    if (property != null)
                    {
                        type.RemoveProperty(property);
                    }
                }

                return builder.GetEdmModel();
            }
        );
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*(/[A-Za-z][A-Za-z0-9]*)*$")]
    private static partial Regex MemberPath();
}
