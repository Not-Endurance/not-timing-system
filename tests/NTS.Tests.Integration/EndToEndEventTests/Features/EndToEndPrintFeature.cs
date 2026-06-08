using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Not.Blazor;
using Not.Blazor.Components.Print;
using Not.Files;
using Not.Krud.ServiceRegistration;
using Not.Localization;
using Not.Print;
using NTS.Application.Contracts;
using NTS.Application.Contracts.Pdf;
using NTS.Blazor.Components.Results;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal sealed class EndToEndPrintFeature : IDisposable
{
    const string FEATURE = "Print";
    const string HANDOUT_PAGE_MARGIN = "6mm 6mm 10mm 6mm";
    const string RANKLIST_PAGE_MARGIN = "10mm 10mm 12mm 10mm";

    readonly NexusApiDriver _api;
    readonly IStringLocalizer _localizer;
    readonly INHtmlComponentRenderer _renderer;
    readonly ServiceProvider _services;

    public EndToEndPrintFeature(NexusApiDriver api)
    {
        _api = api;
        _services = CreateRendererServices();
        _renderer = _services.GetRequiredService<INHtmlComponentRenderer>();
        _localizer = _services.GetRequiredService<IStringLocalizer>();
        LocalizationHelper.Configure(_localizer);
    }

    static string GeneratedByText => $"{Generated_by_NoTiming_System_v_string}{ApplicationConstants.VERSION}";

    public async Task PrintPendingHandouts(
        EventInformation eventInformation,
        IReadOnlyCollection<int> participationNumbers
    )
    {
        var pendingHandouts = await _api.ReadHandouts(eventInformation.Id);
        var selectedHandouts = pendingHandouts
            .Where(handout => participationNumbers.Contains(GetParticipation(handout).Combination.Number))
            .OrderBy(handout => GetParticipation(handout).Combination.Number)
            .ToArray();
        if (selectedHandouts.Length == 0)
        {
            return;
        }

        var officials = await _api.ReadOfficials(eventInformation.Id);
        var batchIndex = 0;
        foreach (var batch in selectedHandouts.Chunk(5))
        {
            var batchNumber = ++batchIndex;
            await FeatureStep.Run(
                FEATURE,
                $"print handout batch {batchNumber}",
                [],
                async () =>
                {
                    var request = await CreateHandoutsRequest(eventInformation, officials, batch, batchNumber);
                    var pdf = await _api.CreatePrintPdf(request);
                    AssertPdf(pdf);

                    foreach (var handout in batch)
                    {
                        await _api.DeleteHandout(handout.Id);
                    }
                }
            );
        }
    }

    public async Task PrintFinalRanklists(EventInformation eventInformation)
    {
        await FeatureStep.Run(
            FEATURE,
            "print final ranklists",
            [],
            async () =>
            {
                var rankings = (await _api.ReadRankings(eventInformation.Id))
                    .OrderBy(ranking => ranking.Name)
                    .ThenBy(ranking => ranking.Category)
                    .ToArray();
                Assert.NotEmpty(rankings);

                var officials = await _api.ReadOfficials(eventInformation.Id);
                var documents = await CreateRanklistRequests(eventInformation, officials, rankings);
                var request = new NPrintBatchRequest
                {
                    FileName = PdfFileNameHelper.ResultsZip(eventInformation.Id),
                    Documents = documents,
                };

                var zip = await _api.CreatePrintZip(request);
                AssertRanklistZip(zip, documents);
            }
        );
    }

    public void Dispose()
    {
        LocalizationHelper.Clear(_localizer);
        _services.Dispose();
    }

    async Task<NPrintDocumentRequest> CreateHandoutsRequest(
        EventInformation eventInformation,
        IReadOnlyList<Official> officials,
        IReadOnlyList<Handout> handouts,
        int batchNumber
    )
    {
        var documents = handouts.Select(handout => new ResultsDocument(handout, eventInformation, officials)).ToArray();
        var html = await RenderResults(documents);

        var request = new NPrintDocumentRequest
        {
            Title = "Handouts",
            FileName = BatchFileName(PdfFileNameHelper.HandoutsPdf(eventInformation.Id), batchNumber),
            Html = html,
            FooterText = GeneratedByText,
            BackdropImage = LogoConstants.Nts,
            Page = new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A5,
                Orientation = NPrintOrientation.Landscape,
                Scale = 0.85m,
                Margin = HANDOUT_PAGE_MARGIN,
            },
        };
        AssertPrintChrome(request);
        return request;
    }

    async Task<IReadOnlyList<NPrintDocumentRequest>> CreateRanklistRequests(
        EventInformation eventInformation,
        IReadOnlyList<Official> officials,
        IReadOnlyList<Ranking> rankings
    )
    {
        var entries = PdfFileNameHelper.ResultPdfEntries(rankings.Select(x => new PdfNamedResult(x.Id, x.Name)));
        var requests = new List<NPrintDocumentRequest>(entries.Count);

        foreach (var (result, entryName) in entries)
        {
            var ranking = rankings.Single(x => x.Id == result.Id);
            var document = new ResultsDocument(new Result(ranking), eventInformation, officials);
            var html = await RenderResult(document);

            var request = new NPrintDocumentRequest
            {
                Title = ranking.Name,
                FileName = entryName,
                Html = html,
                FooterText = GeneratedByText,
                BackdropImage = LogoConstants.Nts,
                Page = new NPrintPageOptions
                {
                    PaperFormat = NPrintPaperFormat.A4,
                    Orientation = NPrintOrientation.Portrait,
                    Scale = 0.85m,
                    Margin = RANKLIST_PAGE_MARGIN,
                },
            };
            AssertPrintChrome(request);
            requests.Add(request);
        }

        return requests;
    }

    async Task<string> RenderResults(IReadOnlyList<ResultsDocument> documents)
    {
        var html = new StringBuilder();
        foreach (var document in documents)
        {
            if (html.Length > 0)
            {
                html.AppendLine();
            }

            html.Append(await RenderResult(document));
        }

        return html.ToString();
    }

    async Task<string> RenderResult(ResultsDocument document)
    {
        return await _renderer.Render<ResultComponent>(
            new Dictionary<string, object?>
            {
                [nameof(ResultComponent.Document)] = document,
                [nameof(ResultComponent.LeftLogo)] = string.Empty,
                [nameof(ResultComponent.RightLogo)] = string.Empty,
            }
        );
    }

    static ServiceProvider CreateRendererServices()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IJSRuntime, StaticHtmlJsRuntime>();
        services.AddNBlazor(configuration);
        services.AddDummyLocalizer();
        services.ConfigureKrud();
        services.AddTransient<INHtmlComponentRenderer, NHtmlComponentRenderer>();
        return services.BuildServiceProvider();
    }

    static void AssertPdf(NFile file)
    {
        Assert.Equal(NFileContentTypes.Pdf, file.ContentType);
        Assert.True(file.Content.Length > 4, $"{file.Name} should contain PDF bytes.");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    static void AssertPrintChrome(NPrintDocumentRequest request)
    {
        Assert.Equal(LogoConstants.Nts, request.BackdropImage);
        Assert.Equal(GeneratedByText, request.FooterText);
    }

    static void AssertRanklistZip(NFile file, IReadOnlyList<NPrintDocumentRequest> documents)
    {
        Assert.Equal(NFileContentTypes.Zip, file.ContentType);
        using var stream = new MemoryStream(file.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.Equal(documents.Count, archive.Entries.Count);
        foreach (var document in documents)
        {
            var entry = archive.GetEntry(document.FileName);
            Assert.NotNull(entry);
            Assert.True(entry.Length > 4, $"{document.FileName} should contain PDF bytes.");

            using var entryStream = entry.Open();
            var header = new byte[4];
            Assert.Equal(header.Length, entryStream.Read(header, 0, header.Length));
            Assert.Equal("%PDF", Encoding.ASCII.GetString(header));
        }
    }

    static string BatchFileName(string fileName, int batchNumber)
    {
        if (batchNumber == 1)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);
        return $"{name}-{batchNumber}{extension}";
    }

    static Participation GetParticipation(Handout handout)
    {
        return handout.Entries.Single().Participation;
    }

    sealed class StaticHtmlJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        )
        {
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
