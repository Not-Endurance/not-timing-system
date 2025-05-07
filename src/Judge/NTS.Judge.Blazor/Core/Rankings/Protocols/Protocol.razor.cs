using Not.Exceptions;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public partial class Protocol
{
    string? _documentSubtitle;
    RanklistDocument? _document;

    [Inject]
    IRankingDocumentBehind Behind { get; set; } = default!;

    [Parameter, EditorRequired]
    public Ranklist Ranklist { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        _document = await Behind.CreateDocument();
        GuardHelper.ThrowIfDefault(_document);
        var city = _document.Header.PopulatedPlace.City;
        var eventSpan = _document.Header.EventSpan.ToString();
        _documentSubtitle =
            city.PadRight(city.Length + 3)
            + eventSpan.PadRight(eventSpan.Length + 3)
            + _document.Header.PopulatedPlace.Country;
    }
}
