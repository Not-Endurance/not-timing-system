using Not.Blazor.Components.Abstractions;

namespace NoTiming.Console.Features.Registry;

/// <summary>
/// What a registry form says when the Athlete or Horse about to be registered looks like one that is there (#646, ADR-0010): the
/// rows, with why they look alike and whose they are. It warns and decides nothing, so the form can still be saved.
/// </summary>
public class SameRowsAlertBehind : NComponent
{
    [Parameter]
    public IReadOnlyList<SameRow> Rows { get; set; } = [];

    protected static string TextOf(SameRow row)
    {
        var name = string.IsNullOrWhiteSpace(row.NameEnglish) ? row.Name : $"{row.Name} ({row.NameEnglish})";
        var because = row.Reason switch
        {
            SameRowReason.FeiId => Same_FEI_ID_string,
            SameRowReason.SimilarName => Similar_name_string,
            _ => Same_name_string,
        };
        var feiId = string.IsNullOrWhiteSpace(row.FeiId) ? string.Empty : $", {FEI_ID_string} {row.FeiId}";
        var whose = row.IsOwn ? string.Empty : $", {Of_another_federation_string}";
        return $"{name}: {because}{feiId}{whose}";
    }
}
