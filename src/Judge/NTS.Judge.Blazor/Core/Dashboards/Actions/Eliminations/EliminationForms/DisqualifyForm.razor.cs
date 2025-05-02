using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms;

public partial class DisqualifyForm
{
    string? Reason { get; set; }

    IEnumerable<DqCode> Codes { get; set; } = [];

    [Parameter]
    public Disqualified? Disqualified { get; set; }

    protected override void OnParametersSet()
    {
        if (Disqualified != null)
        {
            Codes = Disqualified.DqCodes.ToList();
            Reason = Disqualified?.Complement;
        }
    }

    internal override async Task Eliminate()
    {
        if (Reason == null && !Codes.Any())
        {
            NotifyHelper.Warn("Reason is required");
            return;
        }
        var dqCodes = Codes.ToArray();
        await Eliminations.Disqualify(dqCodes, Reason);
    }
}
