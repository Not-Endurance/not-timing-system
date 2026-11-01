using System.Text.Json;
using Not.Application.HTTP;
using Not.Domain.Exceptions;

namespace NoTiming.Ui.Features.Console;

/// <summary>What the services of the Console make of what the Api answers (#646): the answer that took the request or, when it did not, the Api's words as a domain error, and the members of a resource's attributes.</summary>
public static class ApiAnswers
{
    public const int MAX_PAGE_SIZE = 500; // the most a page of a list holds, which the Api does not go beyond
    public const int MAX_PAGES = 20; // the most pages of one list that are read: a list beyond them is not one that can be shown whole

    /// <summary>The answer when the Api took the request; the Api's own words, as a domain error, when it did not.</summary>
    public static JsonApiResponse Accepted(JsonApiResponse response)
    {
        return response.IsSuccess
            ? response
            : throw new DomainException(response.Error?.Message ?? response.Status.ToString());
    }

    /// <summary>
    /// Every resource of a list that the Api cuts into pages, each made into what <paramref name="select"/> makes of it, read
    /// page after page until the Api says that no other follows. The path has its filter and no pages, which are asked for here.
    /// </summary>
    public static async Task<IReadOnlyList<T>> AcceptedPages<T>(
        JsonApiClient api,
        string path,
        Func<JsonElement, T> select,
        int pageSize = MAX_PAGE_SIZE
    )
    {
        var rows = new List<T>();
        var separator = path.Contains('?') ? '&' : '?';
        for (var number = 1; number <= MAX_PAGES; number++)
        {
            var page = Accepted(
                await api.Send(HttpMethod.Get, $"{path}{separator}page%5Bsize%5D={pageSize}&page%5Bnumber%5D={number}")
            );
            rows.AddRange(page.Document!.Value.GetProperty("data").EnumerateArray().Select(select));
            if (!page.HasNextPage)
            {
                return rows;
            }
        }

        throw new InvalidOperationException($"The list at {path} goes on beyond {MAX_PAGES} pages.");
    }

    public static bool Flag(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.True;
    }

    public static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
