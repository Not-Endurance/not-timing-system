using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public class ProtocolBehind : NComponent
{
    [Inject]
    IRanklistDocumentService Service { get; set; } = default!;

    public DocumentHeader? Header => Service.Document?.Header;
    public Ranklist? Ranklist => Service.Document?.Ranklist;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
