using MudBlazor;
using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Athletes;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

public class AthleteFormBehind : KrudFormShell<AthleteFormModel>
{
    [Inject]
    protected ISeeker<Country> Countries { get; set; } = default!;
}
