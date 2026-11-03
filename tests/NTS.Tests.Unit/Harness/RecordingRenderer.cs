using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Print;
using Not.Print;
using NoTiming.Console.Features.Printing;
using NoTiming.Console.Features.Startlist;
using NoTiming.Ui.Components.Results;
using NTS.Contracts;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Harness;

internal sealed class RecordingRenderer : INHtmlComponentRenderer
{
    public List<IReadOnlyDictionary<string, object?>> Rendered { get; } = [];

    public Task<string> Render<TComponent>(IReadOnlyDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        Rendered.Add(parameters);
        return Task.FromResult(
            parameters[nameof(ResultComponent.Document)] switch
            {
                ResultsDocument results => $"<document of {results.Results.Name}>",
                StartlistDocument startlist => $"<startlist of {startlist.Header.Title}>",
                _ => throw new InvalidOperationException("The stub renders the documents of the Console only."),
            }
        );
    }
}
