using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// How the host serves the files of the Ui (#646): a script of a library that the import map of the page names by its
/// fingerprint (the way a module is imported by the app, such as the one that prints) is served, whether the host serves the
/// build of the Ui, which names its files as they are, or the publish of it, which names them by their fingerprint.
/// </summary>
public sealed class UiStaticFilesTests : IClassFixture<ApiHostFixture>
{
    readonly ApiHostFixture _host;

    public UiStaticFilesTests(ApiHostFixture host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_script_of_a_library_that_the_import_map_names_by_its_fingerprint_is_served()
    {
        using var http = new HttpClient { BaseAddress = _host.BaseAddress };
        var index = await http.GetStringAsync("/");
        var map = Regex
            .Match(index, @"<script type=""importmap"">(?<json>.*?)</script>", RegexOptions.Singleline)
            .Groups["json"]
            .Value;
        using var document = JsonDocument.Parse(map);
        var print = document
            .RootElement.GetProperty("imports")
            .EnumerateObject()
            .Single(x => x.Name.EndsWith("_content/Not.Blazor.Client/print-scripts.js", StringComparison.Ordinal));

        var response = await http.GetAsync(print.Value.GetString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("printHtml", await response.Content.ReadAsStringAsync());
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }
}
