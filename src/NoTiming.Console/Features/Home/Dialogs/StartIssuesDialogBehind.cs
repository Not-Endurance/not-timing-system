using Not.Blazor.Dialogs.Abstractions;
using NTS.Domain.Setup.Services.StartValidation;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>What is missing from a Setup for its Event to start, as the domain's start validation says it.</summary>
public class StartIssuesDialogBehind : NDialog
{
    [Parameter, EditorRequired]
    public IReadOnlyList<StartValidationIssue> Issues { get; set; } = [];
}
