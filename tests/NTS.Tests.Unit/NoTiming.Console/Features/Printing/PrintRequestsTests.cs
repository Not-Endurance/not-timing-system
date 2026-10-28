using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Print;
using Not.Print;
using NoTiming.Console.Features.Printing;
using NoTiming.Console.Features.Startlist;
using NoTiming.Ui.Components.Results;
using NTS.Contracts;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.NoTiming.Console.Features.Printing;

/// <summary>
/// What the Console hands the browser to print (#646): the Results of a Ranking and the Handouts of an Event as the HTML of
/// their documents, in the page the person chose, with the logos of the Tenant. The documents are rendered by what the page
/// uses to render a component, which is stood in for here, so that what is tried is what the requests say and how they
/// are made.
/// </summary>
public sealed class PrintRequestsTests
{
    [Fact]
    public async Task The_Results_of_a_Ranking_are_one_A4_page_in_portrait_at_the_scale_the_person_chose_with_the_logos_of_the_Tenant()
    {
        var renderer = new RecordingRenderer();
        var document = DocumentOf("CEI 1*");

        var request = await new PrintRequests(renderer).Ranklist(
            document,
            new NPrintPanelContext(0.7m, NPrintPaperFormat.A3, NPrintOrientation.Landscape),
            "left.png",
            "right.png"
        );

        Assert.Equal(NPrintPaperFormat.A4, request.Page.PaperFormat); // the choice of the paper is for Handouts
        Assert.Equal(NPrintOrientation.Portrait, request.Page.Orientation);
        Assert.Equal(0.7m, request.Page.Scale);
        Assert.Equal("10mm 10mm 12mm 10mm", request.Page.Margin);
        Assert.Equal(Ranklist_string, request.Title);
        Assert.Equal($"{document.Header.Title}.pdf", request.FileName);
        Assert.Equal("appicon.svg", request.BackdropImage);
        Assert.StartsWith(Generated_by_NoTiming_System_v_string, request.FooterText);
        Assert.EndsWith(ApplicationConstants.VERSION, request.FooterText);
        Assert.Equal("<document of CEI 1*>", request.Html);
        var rendered = Assert.Single(renderer.Rendered);
        Assert.Same(document, rendered[nameof(ResultComponent.Document)]);
        Assert.Equal("left.png", rendered[nameof(ResultComponent.LeftLogo)]);
        Assert.Equal("right.png", rendered[nameof(ResultComponent.RightLogo)]);
    }

    [Fact]
    public async Task The_Handouts_of_an_Event_are_the_documents_one_after_another_in_the_paper_the_person_chose()
    {
        var renderer = new RecordingRenderer();

        var request = await new PrintRequests(renderer).Handouts(
            [DocumentOf("One"), DocumentOf("Two"), DocumentOf("Three")],
            new NPrintPanelContext(0.8m, NPrintPaperFormat.A5, NPrintOrientation.Landscape),
            null,
            null
        );

        Assert.Equal(NPrintPaperFormat.A5, request.Page.PaperFormat);
        Assert.Equal(NPrintOrientation.Landscape, request.Page.Orientation);
        Assert.Equal(0.8m, request.Page.Scale);
        Assert.Equal("6mm 6mm 10mm 6mm", request.Page.Margin);
        Assert.Equal(Handouts_string, request.Title);
        Assert.Equal("handouts.pdf", request.FileName);
        Assert.Equal("appicon.svg", request.BackdropImage);
        Assert.StartsWith(Generated_by_NoTiming_System_v_string, request.FooterText);
        Assert.EndsWith(ApplicationConstants.VERSION, request.FooterText);
        Assert.Equal(
            ["<document of One>", "<document of Two>", "<document of Three>"],
            request.Html.Split(Environment.NewLine)
        );
        Assert.Equal(3, renderer.Rendered.Count);
        Assert.All(renderer.Rendered, x => Assert.Null(x[nameof(ResultComponent.LeftLogo)]));
    }

    [Fact]
    public async Task The_Startlist_is_one_A4_page_in_portrait_at_the_scale_the_person_chose_with_the_logos_of_the_Tenant()
    {
        var renderer = new RecordingRenderer();
        var document = StartlistDocument.Compose([], EventOf(), []);

        var request = await new PrintRequests(renderer).Startlist(
            document,
            new NPrintPanelContext(0.7m, NPrintPaperFormat.A3, NPrintOrientation.Landscape),
            "left.png",
            "right.png"
        );

        Assert.Equal(NPrintPaperFormat.A4, request.Page.PaperFormat); // the choice of the paper is for Handouts
        Assert.Equal(NPrintOrientation.Portrait, request.Page.Orientation);
        Assert.Equal(0.7m, request.Page.Scale);
        Assert.Equal("10mm 10mm 12mm 10mm", request.Page.Margin);
        Assert.Equal(Startlist_string, request.Title);
        Assert.Equal("startlist.pdf", request.FileName);
        Assert.Equal("appicon.svg", request.BackdropImage);
        Assert.StartsWith(Generated_by_NoTiming_System_v_string, request.FooterText, StringComparison.Ordinal);
        Assert.EndsWith(ApplicationConstants.VERSION, request.FooterText);
        Assert.Equal("<startlist of Event>", request.Html);
        var rendered = Assert.Single(renderer.Rendered);
        Assert.Same(document, rendered[nameof(StartlistDocumentComponent.Document)]);
        Assert.Equal("left.png", rendered[nameof(StartlistDocumentComponent.LeftLogo)]);
        Assert.Equal("right.png", rendered[nameof(StartlistDocumentComponent.RightLogo)]);
    }

    [Fact]
    public async Task No_Handout_is_an_empty_document()
    {
        var request = await new PrintRequests(new RecordingRenderer()).Handouts(
            [],
            new NPrintPanelContext(1m, NPrintPaperFormat.A5, NPrintOrientation.Landscape),
            null,
            null
        );

        Assert.Equal(string.Empty, request.Html);
    }

    static ResultsDocument DocumentOf(string name)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(2));
        var horse = new Horse("Horse", null, null, TestId.Of(3));
        var combination = new Combination(1, athlete, horse, null, "20", null, null, TestId.Of(4));
        var participation = new Participation(
            ParticipationCategory.Senior,
            new Competition(name, CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(
                [
                    new Phase(
                        "GATE1",
                        20,
                        40,
                        null,
                        CompetitionRuleset.Regional,
                        true,
                        null,
                        Timestamp.Create(DateTimeOffset.Now.AddHours(-2)),
                        null,
                        null,
                        null,
                        false,
                        false,
                        false
                    ),
                ]
            ),
            null,
            TestId.Of(10),
            TestId.Of(5)
        );
        var ranking = new Ranking(
            name,
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(participation.Id, false)],
            TestId.Of(10),
            Guid.NewGuid()
        );
        return new ResultsDocument(new Result(ranking, [participation]), EventOf(), []);
    }

    static EventInformation EventOf()
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var span = new EventSpan(DateTimeOffset.Now.Date, DateTimeOffset.Now.Date.AddDays(1));
        return new EventInformation(country, "Event", "Location", span, null, id: TestId.Of(10));
    }

    sealed class RecordingRenderer : INHtmlComponentRenderer
    {
        public List<IReadOnlyDictionary<string, object?>> Rendered { get; } = [];

        public Task<string> Render<TComponent>(IReadOnlyDictionary<string, object?> parameters)
            where TComponent : IComponent
        {
            Rendered.Add(parameters);
            return Task.FromResult(
                parameters[nameof(ResultComponent.Document)] switch
                {
                    ResultsDocument results => $"<document of {results.Results.Name}>",
                    StartlistDocument startlist => $"<startlist of {startlist.Header.Title}>",
                    _ => throw new InvalidOperationException("The stub renders the documents of the Console only."),
                }
            );
        }
    }
}
