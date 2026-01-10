using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Nexus;

public partial class InquiryPage : NComponent
{
    [Inject]
    protected IInquiryBehind Behind { get; set; } = default!;

    protected InquiryType? Type { get; set; }
    protected int? SearchInt { get; set; }
    protected string? SearchTerm { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }
}
