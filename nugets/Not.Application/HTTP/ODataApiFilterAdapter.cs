using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

namespace Not.Application.HTTP;

public static class ODataApiFilterAdapter
{
    const string FILTER_QUERY = "$filter";

    /// <summary>
    /// The OData query the filters stand for, <c>$filter</c> and nothing else. The members are written as the type names
    /// them, or in camelCase as a JSON:API resource does (ADR-0008), and a filter that cannot be written is refused.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseFilters<T>(
        IEnumerable<Expression<Func<T, bool>>> filters,
        bool camelCase = false
    )
    {
        if (TryParseFilters(filters, out var queryParameters, camelCase))
        {
            return queryParameters;
        }

        throw new NotSupportedException("Unable to parse Filter expressions into OData query filter.");
    }

    public static bool TryParseFilters<T>(
        IEnumerable<Expression<Func<T, bool>>> filters,
        out IReadOnlyDictionary<string, string> queryParameters,
        bool camelCase = false
    )
    {
        Func<string, string> naming = camelCase ? CamelCase : Unchanged;
        var oDataFilters = new List<string>();
        foreach (var filter in filters)
        {
            if (!TryCreateFilter(filter, naming, out var oDataFilter))
            {
                queryParameters = new Dictionary<string, string>();
                return false;
            }

            oDataFilters.Add(oDataFilter);
        }

        var combinedFilter = CombineFilterValues(oDataFilters);
        if (string.IsNullOrWhiteSpace(combinedFilter))
        {
            queryParameters = new Dictionary<string, string>();
            return true;
        }

        queryParameters = new Dictionary<string, string> { [FILTER_QUERY] = combinedFilter };
        return true;
    }

    /// <summary>
    /// Whether the member is an enum. The server does not take an enum literal from this adapter, so such a filter is not
    /// written and the repository filters what it reads by itself, which is right and slower, not wrong.
    /// </summary>
    static bool IsEnum(Expression expression)
    {
        var type = StripConversion(expression).Type;
        return (Nullable.GetUnderlyingType(type) ?? type).IsEnum;
    }

    static string CamelCase(string name)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(name);
    }

    static string Unchanged(string name)
    {
        return name;
    }

    static bool TryCreateFilter<T>(
        Expression<Func<T, bool>> filter,
        Func<string, string> naming,
        out string oDataFilter
    )
    {
        if (TryCreateFilter(filter.Body, naming, out oDataFilter))
        {
            return true;
        }

        oDataFilter = "";
        return false;
    }

    static string CombineFilterValues(IEnumerable<string?> filters)
    {
        var values = filters.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToArray();
        return values.Length == 1 ? values[0] : string.Join(" and ", values.Select(x => $"({x})"));
    }

    static string CombineInlineFilterValues(IEnumerable<string?> filters)
    {
        var values = filters.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim());
        return string.Join(" and ", values);
    }

    static bool TryCreateFilter(Expression expression, Func<string, string> naming, out string oDataFilter)
    {
        expression = StripConversion(expression);
        if (expression is BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.And } binary)
        {
            if (
                TryCreateFilter(binary.Left, naming, out var left)
                && TryCreateFilter(binary.Right, naming, out var right)
            )
            {
                oDataFilter = CombineInlineFilterValues([left, right]);
                return true;
            }

            oDataFilter = "";
            return false;
        }

        if (expression is ConstantExpression { Value: bool constant })
        {
            oDataFilter = constant ? "" : "false";
            return true;
        }

        if (expression is BinaryExpression comparison && TryGetOperator(comparison.NodeType, out var oDataOperator))
        {
            return TryCreateComparison(comparison.Left, comparison.Right, oDataOperator, naming, out oDataFilter)
                || TryCreateComparison(
                    comparison.Right,
                    comparison.Left,
                    ReverseOperator(oDataOperator),
                    naming,
                    out oDataFilter
                );
        }

        // A member that is a boolean is a condition by itself, and so is the negation of one.
        if (expression.Type == typeof(bool) && TryGetPropertyName(expression, naming, out var flag))
        {
            oDataFilter = $"{flag} eq true";
            return true;
        }

        if (
            expression is UnaryExpression { NodeType: ExpressionType.Not } negation
            && negation.Operand.Type == typeof(bool)
            && TryGetPropertyName(negation.Operand, naming, out var negated)
        )
        {
            oDataFilter = $"{negated} eq false";
            return true;
        }

        if (expression is MethodCallExpression call)
        {
            return TryCreateContains(call, naming, out oDataFilter);
        }

        oDataFilter = "";
        return false;
    }

    static bool TryCreateComparison(
        Expression propertyExpression,
        Expression valueExpression,
        string oDataOperator,
        Func<string, string> naming,
        out string oDataFilter
    )
    {
        if (!TryGetPropertyName(propertyExpression, naming, out var propertyName) || IsEnum(propertyExpression))
        {
            oDataFilter = "";
            return false;
        }

        if (!TryEvaluate(valueExpression, out var value))
        {
            oDataFilter = "";
            return false;
        }

        oDataFilter = $"{propertyName} {oDataOperator} {FormatValue(value)}";
        return true;
    }

    static bool TryCreateContains(MethodCallExpression call, Func<string, string> naming, out string oDataFilter)
    {
        if (
            !TryGetContainsArguments(call, out var valuesExpression, out var propertyExpression)
            || !TryGetPropertyName(propertyExpression, naming, out var propertyName)
            || IsEnum(propertyExpression)
            || !TryEvaluate(valuesExpression, out var values)
            || values is string
            || values is not IEnumerable enumerable
        )
        {
            oDataFilter = "";
            return false;
        }

        var filters = enumerable.Cast<object?>().Select(value => $"{propertyName} eq {FormatValue(value)}").ToArray();

        oDataFilter = filters.Length switch
        {
            0 => "false",
            1 => filters[0],
            _ => $"({string.Join(" or ", filters)})",
        };
        return true;
    }

    static bool TryGetContainsArguments(
        MethodCallExpression call,
        out Expression valuesExpression,
        out Expression propertyExpression
    )
    {
        if (call.Method.Name == nameof(IList.Contains))
        {
            if (call is { Object: not null, Arguments.Count: 1 })
            {
                valuesExpression = call.Object;
                propertyExpression = call.Arguments[0];
                return true;
            }

            if (call is { Object: null, Arguments.Count: 2 } && call.Method.DeclaringType == typeof(Enumerable))
            {
                valuesExpression = call.Arguments[0];
                propertyExpression = call.Arguments[1];
                return true;
            }
        }

        valuesExpression = default!;
        propertyExpression = default!;
        return false;
    }

    static bool TryGetPropertyName(Expression expression, Func<string, string> naming, out string propertyName)
    {
        expression = StripConversion(expression);
        if (
            expression is MemberExpression member
            && member.Expression != null
            && StripConversion(member.Expression) is ParameterExpression
        )
        {
            propertyName = naming(member.Member.Name);
            return true;
        }

        propertyName = "";
        return false;
    }

    static bool TryGetOperator(ExpressionType nodeType, out string oDataOperator)
    {
        oDataOperator = nodeType switch
        {
            ExpressionType.Equal => "eq",
            ExpressionType.NotEqual => "ne",
            ExpressionType.GreaterThan => "gt",
            ExpressionType.GreaterThanOrEqual => "ge",
            ExpressionType.LessThan => "lt",
            ExpressionType.LessThanOrEqual => "le",
            _ => "",
        };

        return oDataOperator != "";
    }

    static string ReverseOperator(string oDataOperator)
    {
        return oDataOperator switch
        {
            "gt" => "lt",
            "ge" => "le",
            "lt" => "gt",
            "le" => "ge",
            _ => oDataOperator,
        };
    }

    static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        return value switch
        {
            string text => $"'{EscapeString(text)}'",
            char character => $"'{EscapeString(character.ToString())}'",
            bool flag => flag ? "true" : "false",
            Enum enumValue => $"'{EscapeString(enumValue.ToString())}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => $"'{EscapeString(value.ToString() ?? "")}'",
        };
    }

    static string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    static bool TryEvaluate(Expression expression, out object? value)
    {
        try
        {
            expression = StripConversion(expression);
            if (expression is ConstantExpression constant)
            {
                value = constant.Value;
                return true;
            }

            value = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile().Invoke();
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    static Expression StripConversion(Expression expression)
    {
        while (
            expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
        )
        {
            expression = unary.Operand;
        }

        return expression;
    }
}
