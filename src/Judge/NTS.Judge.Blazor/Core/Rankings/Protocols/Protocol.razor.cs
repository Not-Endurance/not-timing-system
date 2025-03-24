using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public partial class Protocol
{
    string? _documentSubtitle;

    [Parameter, EditorRequired]
    public Ranklist Ranklist { get; set; } = default!;

    [Parameter, EditorRequired]
    public RanklistDocument Document { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _documentSubtitle = Document.Header.PopulatedPlace.City + "   " +
        Document.Header.EventSpan + "   " +
        Document.Header.PopulatedPlace.Country;
    }
}
