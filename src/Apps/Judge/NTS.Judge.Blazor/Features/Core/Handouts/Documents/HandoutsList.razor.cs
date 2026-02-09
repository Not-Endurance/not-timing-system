using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Core.Handouts.Documents;

public partial class HandoutsList
{
    [Parameter]
    public IEnumerable<HandoutDocument>? Documents { get; set; } = null;
}
