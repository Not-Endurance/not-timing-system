using MudBlazor;
using Not.Blazor.Components;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;

public partial class CompetitionForm
{
    MudNumericField<int?> _requiredInspectionCompulsoryThreshold = default!;
    MudTextField<string?> _nameField = default!;
    NSelect<CompetitionType> _typeField = default!;
    NSelect<CompetitionRuleset> _rulesetField = default!;
    MudPicker<DateTime?> _dayField = default!;
    MudPicker<TimeSpan?> _timeField = default!;
    List<NotListModel<CompetitionType>> _competitionTypes = NotListModel.FromEnum<CompetitionType>().ToList();
    List<NotListModel<CompetitionRuleset>> _competitionRules = NotListModel.FromEnum<CompetitionRuleset>().ToList();

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Competition.Name), () => _nameField);
        RegisterInjector(nameof(Competition.Start), () => _dayField);
        RegisterInjector(nameof(Competition.Start), () => _timeField);
        RegisterInjector(nameof(Competition.Type), () => _typeField);
        RegisterInjector(nameof(Competition.Ruleset), () => _rulesetField);
        RegisterInjector(nameof(Competition.CompulsoryThresholdSpan), () => _requiredInspectionCompulsoryThreshold);
    }
}
