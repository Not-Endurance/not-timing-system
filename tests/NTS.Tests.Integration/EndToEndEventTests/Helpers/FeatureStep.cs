using NTS.Domain.Objects;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal static class FeatureStep
{
    public static async Task Run(string feature, string step, EndToEndPhaseSnapshot entry, Func<Task> action)
    {
        await Run(feature, step, [entry], action);
    }

    public static async Task Run(
        string feature,
        string step,
        IReadOnlyList<EndToEndPhaseSnapshot> entries,
        Func<Task> action
    )
    {
        await Run(
            feature,
            step,
            entries,
            async () =>
            {
                await action();
                return true;
            }
        );
    }

    public static async Task<T> Run<T>(
        string feature,
        string step,
        IReadOnlyList<EndToEndPhaseSnapshot> entries,
        Func<Task<T>> action
    )
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not FeatureStepException)
        {
            throw new FeatureStepException(BuildMessage(feature, step, entries, ex), ex);
        }
    }

    static string BuildMessage(
        string feature,
        string step,
        IReadOnlyList<EndToEndPhaseSnapshot> entries,
        Exception exception
    )
    {
        var lines = new List<string> { $"Feature: {feature}", $"Step: {step}", "Snapshot data:" };

        if (entries.Count == 0)
        {
            lines.Add("  <none>");
        }
        else
        {
            lines.AddRange(entries.Select(entry => $"  {Describe(entry)}"));
        }

        lines.Add("Original failure:");
        lines.Add($"  {exception.GetType().Name}: {exception.Message}");
        return string.Join(Environment.NewLine, lines);
    }

    static string Describe(EndToEndPhaseSnapshot entry)
    {
        var phase = entry.Phase;
        return string.Join(
            ", ",
            $"#{entry.Number}",
            $"phase {entry.PhaseNumber}",
            $"start={Format(phase.StartTime)}",
            $"arrive={Format(entry.ArriveTime)}",
            $"present={Format(entry.PresentTime)}",
            $"represent={Format(entry.RepresentTime)}",
            $"requestedInspection={phase.IsRequiredInspectionRequested}",
            $"compulsoryInspection={phase.IsRequiredInspectionCompulsory}",
            $"reinspection={phase.IsReinspectionRequested}",
            $"complete={phase.IsComplete()}",
            $"eliminated={entry.ExpectedEliminated?.ToString() ?? "<none>"}"
        );
    }

    static string Format(Timestamp? timestamp)
    {
        return Format(timestamp?.ToDateTimeOffset());
    }

    static string Format(DateTimeOffset? timestamp)
    {
        return timestamp?.ToString("HH:mm:ss") ?? "<none>";
    }
}

internal sealed class FeatureStepException : Exception
{
    public FeatureStepException(string message, Exception innerException)
        : base(message, innerException) { }
}
