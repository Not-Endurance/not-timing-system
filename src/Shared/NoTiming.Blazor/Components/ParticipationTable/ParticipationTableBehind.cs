using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NC.Krud.Blazor;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Blazor.Components.ParticipationTable.Phases;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Objects;

namespace NoTiming.Blazor.Components.ParticipationTable;

public class ParticipationTableBehind : NComponent
{
    protected const string TableClass = "participation-table";

    [Inject]
    KrudDialogService<PhaseUpdateModel, PhaseUpdateShell> Dialog { get; set; } = default!;

    protected IReadOnlyList<ParticipationTableRow> Rows { get; private set; } = [];

    protected IReadOnlyList<Phase> DisplayPhases { get; private set; } = [];

    protected string[] PhaseHeadings { get; private set; } = [];

    protected override bool ObserveBreakpointChanges => true;
    protected bool UseVerticalLayout => IsMdAndDown || Vertical;
    protected string LeadingHeaderText => Number?.ToString() ?? Number_string;

    [Parameter]
    public PhaseCollection? Phases { get; set; }

    [Parameter]
    public int? Number { get; set; }

    [Parameter]
    public SpeedRestrictions? Restrictions { get; set; }

    [Parameter]
    public bool Editable { get; set; }

    [Parameter]
    public bool Vertical { get; set; }

    protected override void OnParametersSet()
    {
        try
        {
            DisplayPhases = Phases?.ToArray() ?? [];
            RebuildRows();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected override void OnBeforeRender()
    {
        RebuildRows();
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

    void RebuildRows()
    {
        var verticalRows = BuildRows(DisplayPhases);
        if (UseVerticalLayout)
        {
            PhaseHeadings = DisplayPhases.Select(x => x.Gate).ToArray();
            Rows = verticalRows;
            return;
        }

        PhaseHeadings = verticalRows.Select(x => x.Label).ToArray();
        Rows = BuildPhaseRows(DisplayPhases, verticalRows);
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
        rows.Add(
            CreateSpeedRow(
                Loop_S_string,
                phases,
                index => phases[index].GetAverageLoopSpeed()?.ToDouble(),
                x => (x.LoopSpeedMin, x.LoopSpeedMax)
            )
        );
        rows.Add(
            CreateSpeedRow(
                Phase_S_string,
                phases,
                index => phases[index].GetAveragePhaseSpeed()?.ToDouble(),
                x => (x.PhaseSpeedMin, x.PhaseSpeedMax)
            )
        );
        rows.Add(
            CreateSpeedRow(
                Loop_average_S_string,
                phases,
                index => SpeedAverages.Loop(phases.Take(index + 1)),
                x => (x.LoopAverageSpeedMin, x.LoopAverageSpeedMax)
            )
        );
        rows.Add(
            CreateSpeedRow(
                Phase_average_S_string,
                phases,
                index => SpeedAverages.Phase(phases.Take(index + 1)),
                x => (x.PhaseAverageSpeedMin, x.PhaseAverageSpeedMax)
            )
        );

        return rows;
    }

    ParticipationTableRow CreateRow(string label, IReadOnlyList<Phase> phases, Func<Phase, object?> selector)
    {
        return new ParticipationTableRow(label, phases.Select(x => FormatValue(selector(x))).ToArray());
    }

    // A speed that is below its minimum is marked with a down arrow, and one above its maximum with an up arrow.
    // The row is highlighted when any of its speeds break a limit.
    ParticipationTableRow CreateSpeedRow(
        string label,
        IReadOnlyList<Phase> phases,
        Func<int, double?> speed,
        Func<SpeedRestrictions, (double? Min, double? Max)> limits
    )
    {
        var (min, max) = limits(Restrictions ?? SpeedRestrictions.None);
        var values = new string[phases.Count];
        var breaches = new bool[phases.Count];
        for (var index = 0; index < phases.Count; index++)
        {
            var value = speed(index);
            values[index] = FormatSpeed(value, min, max);
            breaches[index] = Breaks(value, min, max);
        }
        return new ParticipationTableRow(label, values, breaches);
    }

    static IReadOnlyList<ParticipationTableRow> BuildPhaseRows(
        IReadOnlyList<Phase> phases,
        IReadOnlyList<ParticipationTableRow> rows
    )
    {
        return phases
            .Select(
                (phase, index) => new ParticipationTableRow(
                    phase.Gate,
                    rows.Select(row => row.Values[index]).ToArray(),
                    rows.Select(row => row.Breaches[index]).ToArray()
                )
            )
            .ToArray();
    }

    static string FormatValue(object? value)
    {
        return value?.ToString() ?? "-";
    }

    // Limits are compared at the precision the speed is shown with, so a value that displays as the limit is not marked.
    static bool Breaks(double? value, double? min, double? max)
    {
        var shown = value == null ? (double?)null : Math.Round(value.Value, 2);
        return shown < min || shown > max;
    }

    static string FormatSpeed(double? value, double? min, double? max)
    {
        if (value == null)
        {
            return "-";
        }
        var shown = Math.Round(value.Value, 2);
        if (shown < min)
        {
            return $"{shown:0.00} ▼";
        }
        if (shown > max)
        {
            return $"{shown:0.00} ▲";
        }
        return $"{shown:0.00}";
    }

    protected sealed record ParticipationTableRow
    {
        public ParticipationTableRow(string label, IReadOnlyList<string> values, IReadOnlyList<bool>? breaches = null)
        {
            Label = label;
            Values = values;
            Breaches = breaches ?? new bool[values.Count];
        }

        public string Label { get; }
        public IReadOnlyList<string> Values { get; }
        public IReadOnlyList<bool> Breaches { get; }
        public bool HasBreach => Breaches.Any(x => x);
    }
}
