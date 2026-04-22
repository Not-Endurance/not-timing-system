using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Krud.Blazor;
using NTS.Application.Core;
using NTS.Blazor.Components.ParticipationTable.Phases;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Blazor.Components.ParticipationTable;

public class ParticipationTableBehind : NComponent
{
    [Inject]
    KrudDialogService<PhaseUpdateModel, PhaseUpdateShell> Dialog { get; set; } = default!;

    protected IReadOnlyList<ParticipationTableRow> Rows { get; private set; } = [];

    protected IReadOnlyList<Phase> DisplayPhases { get; private set; } = [];

    protected string[] PhaseHeadings { get; private set; } = [];

    protected string LeadingHeaderText => Number?.ToString() ?? Number_string;

    protected string TableClass => Compact ? "participation-table participation-table-compact" : "participation-table";

    [Parameter]
    public PhaseCollection? Phases { get; set; }

    [Parameter]
    public int? Number { get; set; }

    [Parameter]
    public bool Editable { get; set; }

    [Parameter]
    public bool AlignVertically { get; set; }

    [Parameter]
    public bool Compact { get; set; }

    protected override void OnParametersSet()
    {
        try
        {
            DisplayPhases = Phases?.ToArray() ?? [];
            PhaseHeadings = DisplayPhases.Select(x => x.Gate).ToArray();
            Rows = BuildRows(DisplayPhases);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ShowUpdate(Phase phase)
    {
        try
        {
            var model = new PhaseUpdateModel(phase);
            await Dialog.ShowUpdateForm(model);
            await InvokeRender();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected IEnumerable<string> GetRowValues(object item)
    {
        return ((ParticipationTableRow)item).Values;
    }

    IReadOnlyList<ParticipationTableRow> BuildRows(IReadOnlyList<Phase> phases)
    {
        if (phases.Count == 0)
        {
            return [];
        }

        var anyRepresentation = phases.Any(x => x.IsReinspectionRequested);
        var anyRequiredInspection = phases.Any(x => x.IsRequiredInspectionRequested);
        var anyCompulsoryRequiredInspection = phases.Any(x => x.IsRequiredInspectionCompulsory);

        var rows = new List<ParticipationTableRow>
        {
            CreateRow(Start_Time_string, phases, x => x.StartTime),
            CreateRow(Arrive_string, phases, x => x.ArriveTime),
            CreateRow(Presentation_string, phases, x => x.PresentTime),
        };

        if (anyRepresentation)
        {
            rows.Add(CreateRow(Represent_string, phases, x => x.RepresentTime));
        }

        if (anyRequiredInspection)
        {
            rows.Add(
                CreateRow(
                    RI_Time_string,
                    phases,
                    x => x.IsRequiredInspectionRequested ? x.GetRequiredInspectionTime() : null
                )
            );
        }

        if (anyCompulsoryRequiredInspection)
        {
            rows.Add(
                CreateRow(
                    CRI_Time_string,
                    phases,
                    x => x.IsRequiredInspectionCompulsory ? x.GetRequiredInspectionTime() : null
                )
            );
        }

        rows.Add(CreateRow(Recovery_string, phases, x => x.GetRecoveryInterval()));
        rows.Add(CreateRow(Loop_T_string, phases, x => x.GetLoopInterval()));
        rows.Add(CreateRow(Phase_T_string, phases, x => x.GetPhaseInterval()));
        rows.Add(CreateRow(Loop_S_string, phases, x => x.GetAverageLoopSpeed()));
        rows.Add(CreateRow(Phase_S_string, phases, x => x.GetAveragePhaseSpeed()));

        return rows;
    }

    ParticipationTableRow CreateRow(string label, IReadOnlyList<Phase> phases, Func<Phase, object?> selector)
    {
        return new ParticipationTableRow(label, phases.Select(x => FormatValue(selector(x))).ToArray());
    }

    static string FormatValue(object? value)
    {
        return value?.ToString() ?? "-";
    }

    protected sealed record ParticipationTableRow
    {
        public ParticipationTableRow(string label, IReadOnlyList<string> values)
        {
            Label = label;
            Values = values;
        }

        public string Label { get; }
        public IReadOnlyList<string> Values {get;}
    }
}
