using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Core.Handouts.Documents;

public class HandoutsListBehind : NComponent
{
    [Parameter]
    public IEnumerable<HandoutDocument>? Documents { get; set; }
}
