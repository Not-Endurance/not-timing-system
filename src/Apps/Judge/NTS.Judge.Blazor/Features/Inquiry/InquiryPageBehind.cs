using Not.Blazor.Components.Abstractions;

namespace NTS.Judge.Blazor.Features.Inquiry;

public class InquiryPageBehind : NStatefulComponent
{
    protected InquiryType? Type { get; set; }
    protected int? SearchInt { get; set; }
    protected string? SearchTerm { get; set; }

    [Inject]
    protected IInquiryBehind Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
