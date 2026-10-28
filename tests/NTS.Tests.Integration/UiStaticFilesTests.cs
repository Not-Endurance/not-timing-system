using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NoTiming.Api.Hosting;
using NTS.Tests.Integration.Harness;

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

    [Theory]
    [InlineData("/_content/Lib/print.abcdefghij.js", "/_content/Lib/print.js", "/_content/Lib/print.js")] // asked by its fingerprint, there by its name
    [InlineData(
        "/_content/Lib/print.abcdefghij.js",
        "/_content/Lib/print.abcdefghij.js",
        "/_content/Lib/print.abcdefghij.js|/_content/Lib/print.js"
    )] // there as it is asked for
    [InlineData("/_content/Lib/print.abcdefghij.js", "/_content/Lib/print.abcdefghij.js", "/_content/Lib/other.js")] // no file of that name to serve
    [InlineData("/appsettings.abcdefghij.json", "/appsettings.abcdefghij.json", "/appsettings.json")] // outside the libraries
    [InlineData("/_content/Lib/print.abcdefghi.js", "/_content/Lib/print.abcdefghi.js", "/_content/Lib/print.js")] // nine characters are not a fingerprint
    [InlineData("/_content/Lib/print.abcdefghijk.js", "/_content/Lib/print.abcdefghijk.js", "/_content/Lib/print.js")] // nor are eleven
    public async Task A_file_that_is_asked_for_by_its_fingerprint_is_served_under_its_name_only_when_it_is_not_there_as_asked_and_the_name_is(
        string asked,
        string expected,
        string existing
    )
    {
        string? seen = null;
        var middleware = new UiStaticFiles.FingerprintedContentMiddleware(
            context =>
            {
                seen = context.Request.Path.Value;
                return Task.CompletedTask;
            },
            new FilesOf(existing.Split('|'))
        );
        var context = new DefaultHttpContext();
        context.Request.Path = asked;

        await middleware.InvokeAsync(context);

        Assert.Equal(expected, seen);
    }

    sealed class FilesOf : IWebHostEnvironment
    {
        public FilesOf(string[] there)
        {
            WebRootFileProvider = new Provider(there);
        }

        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        sealed class Provider : IFileProvider
        {
            readonly HashSet<string> _there;

            public Provider(string[] there)
            {
                _there = new HashSet<string>(there, StringComparer.Ordinal);
            }

            public IFileInfo GetFileInfo(string subpath)
            {
                return _there.Contains(subpath) ? new Info(subpath) : new NotFoundFileInfo(subpath);
            }

            public IDirectoryContents GetDirectoryContents(string subpath)
            {
                return NotFoundDirectoryContents.Singleton;
            }

            public IChangeToken Watch(string filter)
            {
                return NullChangeToken.Singleton;
            }

            sealed class Info : IFileInfo
            {
                public Info(string path)
                {
                    Name = Path.GetFileName(path);
                }

                public bool Exists => true;
                public long Length => 0;
                public string? PhysicalPath => null;
                public string Name { get; }
                public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
                public bool IsDirectory => false;

                public Stream CreateReadStream()
                {
                    return Stream.Null;
                }
            }
        }
    }
}
