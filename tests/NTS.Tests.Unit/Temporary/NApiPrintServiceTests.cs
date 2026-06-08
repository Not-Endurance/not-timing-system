using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Application.Print;
using Not.Files;
using Not.Print;

namespace NTS.Tests.Unit.Temporary;

public sealed class NApiPrintServiceTests
{
    [Fact]
    public async Task CreatePdf_PostsToPrintEndpointAndReturnsResponseFile()
    {
        var content = Encoding.ASCII.GetBytes("%PDF-test");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(NFileContentTypes.Pdf);
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = "\"generated.pdf\"",
        };
        var handler = new TestHttpMessageHandler(response);
        var service = CreateService(handler);

        var file = await service.CreatePdf(new NPrintDocumentRequest { FileName = "fallback", Html = "<main />" });

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("https://example.test/api/print/pdf"), handler.RequestUri);
        Assert.Equal("application/json", handler.RequestContentType);
        Assert.Equal("generated.pdf", file.Name);
        Assert.Equal(NFileContentTypes.Pdf, file.ContentType);
        Assert.Equal(content, file.Content);
    }

    [Fact]
    public async Task CreateZip_UsesFallbackNameAndContentTypeWhenHeadersAreMissing()
    {
        var content = Encoding.ASCII.GetBytes("PK-test");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        var handler = new TestHttpMessageHandler(response);
        var service = CreateService(handler);

        var file = await service.CreateZip(new NPrintBatchRequest { FileName = "results" });

        Assert.Equal(new Uri("https://example.test/api/print/zip"), handler.RequestUri);
        Assert.Equal("results.zip", file.Name);
        Assert.Equal(NFileContentTypes.Zip, file.ContentType);
        Assert.Equal(content, file.Content);
    }

    static NApiPrintService CreateService(TestHttpMessageHandler handler)
    {
        var http = new NHttpClient(
            new TestHttpClientFactory(handler),
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = "https://example.test/api/" })
        );
        return new NApiPrintService(http);
    }

    sealed class TestHttpClientFactory : IHttpClientFactory
    {
        readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, false);
        }
    }

    sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        readonly HttpResponseMessage _response;

        public TestHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;
            if (request.Content != null)
            {
                _ = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _response;
        }
    }
}
