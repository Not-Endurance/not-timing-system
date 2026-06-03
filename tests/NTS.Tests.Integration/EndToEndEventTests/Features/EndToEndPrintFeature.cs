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
using NTS.Application.Contracts.Pdf;
using NTS.Blazor.Components.Print;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal sealed class EndToEndPrintFeature : IDisposable
{
    const string FEATURE = "Print";

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

    public async Task PrintPendingHandouts(
        EventInformation eventInformation,
        IReadOnlyCollection<int> participationNumbers
    )
    {
        var pendingHandouts = await _api.ReadHandouts(eventInformation.Id);
        var selectedHandouts = pendingHandouts
            .Where(handout => participationNumbers.Contains(handout.Participation.Combination.Number))
            .OrderBy(handout => handout.Participation.Combination.Number)
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
        var documents = handouts.Select(handout => new HandoutDocument(handout, eventInformation, officials)).ToArray();
        var html = await _renderer.Render<HandoutsPrintDocument>(
            new Dictionary<string, object?>
            {
                [nameof(HandoutsPrintDocumentBehind.Documents)] = documents,
                [nameof(HandoutsPrintDocumentBehind.Compact)] = true,
                [nameof(HandoutsPrintDocumentBehind.LeftLogo)] = string.Empty,
                [nameof(HandoutsPrintDocumentBehind.RightLogo)] = string.Empty,
            }
        );

        return new NPrintDocumentRequest
        {
            Title = "Handouts",
            FileName = BatchFileName(PdfFileNameHelper.HandoutsPdf(eventInformation.Id), batchNumber),
            Html = html,
            Page = new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A5,
                Orientation = NPrintOrientation.Landscape,
                Scale = 0.85m,
                Margin = "6mm",
            },
        };
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
            var document = new ProtocolDocument(new Ranklist(ranking), eventInformation, officials);
            var html = await _renderer.Render<RanklistPrintDocument>(
                new Dictionary<string, object?>
                {
                    [nameof(RanklistPrintDocumentBehind.Document)] = document,
                    [nameof(RanklistPrintDocumentBehind.Compact)] = true,
                    [nameof(RanklistPrintDocumentBehind.PhasesAsRows)] = true,
                    [nameof(RanklistPrintDocumentBehind.LeftLogo)] = string.Empty,
                    [nameof(RanklistPrintDocumentBehind.RightLogo)] = string.Empty,
                }
            );

            requests.Add(
                new NPrintDocumentRequest
                {
                    Title = ranking.Name,
                    FileName = entryName,
                    Html = html,
                    Page = new NPrintPageOptions
                    {
                        PaperFormat = NPrintPaperFormat.A4,
                        Orientation = NPrintOrientation.Portrait,
                        Scale = 0.85m,
                        Margin = "10mm",
                    },
                }
            );
        }

        return requests;
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

    static void AssertPdf(NFileContent file)
    {
        Assert.Equal(NFileContentTypes.Pdf, file.ContentType);
        Assert.True(file.Content.Length > 4, $"{file.FileName} should contain PDF bytes.");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    static void AssertRanklistZip(NFileContent file, IReadOnlyList<NPrintDocumentRequest> documents)
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
