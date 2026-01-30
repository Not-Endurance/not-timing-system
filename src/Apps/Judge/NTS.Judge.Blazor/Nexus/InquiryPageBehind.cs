using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Nexus;

public class InquiryPageBehind : NStatefulComponent<IInquiryBehind>
{
    protected InquiryType? Type { get; set; }
    protected int? SearchInt { get; set; }
    protected string? SearchTerm { get; set; }
}
