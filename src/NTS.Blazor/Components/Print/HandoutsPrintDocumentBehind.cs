using Microsoft.AspNetCore.Components;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Print;

public class HandoutsPrintDocumentBehind : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<HandoutDocument> Documents { get; set; } = [];

    [Parameter]
    public string LeftLogo { get; set; } = PrintLogoPath.Fei;

    [Parameter]
    public string RightLogo { get; set; } = PrintLogoPath.Bfks;

    [Parameter]
    public bool Compact { get; set; }

    protected static ProtocolDocument CreateProtocolDocument(HandoutDocument document)
    {
        var entry = new RankingEntry(document.Participation, null, false, document.ParticipationId);
        var ranking = new Ranking(
            document.Header.Title,
            document.Header.Ruleset,
            document.Participation.Category,
            null,
            null,
            null,
            null,
            null,
            [entry],
            document.Participation.EventId,
            document.Id
        );
        var ranklist = new Ranklist(ranking, [entry]);
        return new ProtocolDocument(ranklist, document.Header);
    }
}
