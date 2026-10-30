using Not.Application.Behinds.Adapters;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The Handouts of a Live Event in the Console (#646, ADR-0006): the sheet of a Participation that is handed out when one of
/// its Phases is complete, made by the Main Operator or by the Console while a page that watches is open, printed and, once
/// the person confirms it was printed, removed. A Handout names its Participation and holds none of its data, so what its
/// document shows is the Participation as it is when it is composed, a later correction included. A Participation has one
/// Handout: the id of the Handout is the id of the Participation, so that a Handout made again, by another tab or by hand,
/// is the one that is there.
/// </summary>
public interface IConsoleHandouts : IStatefulService
{
    /// <summary>The documents of the Handouts that are stored, by start number.</summary>
    IReadOnlyList<ResultsDocument> Documents { get; }

    /// <summary>The Combinations of the Event, by start number: a Handout can be made for each.</summary>
    IReadOnlyList<Combination> Combinations { get; }

    /// <summary>Reads the Handouts of the Event the view shows, which the page lists, and composes their documents from its Participations.</summary>
    Task Show(IViewedEvent view);

    /// <summary>
    /// Makes a Handout whenever a Phase that is not the last one completes in the Event of the view, until the handle is
    /// disposed: the Participations are those that were complete when it began, so that what was handed out before is not
    /// handed out again. A Handout that the Api does not take is told to the person, and the Event goes on.
    /// </summary>
    IDisposable Watch(IViewedEvent view);

    /// <summary>Makes the Handout of the Participation with the start number, or finds the one it has.</summary>
    Task Create(int number);

    /// <summary>Removes the Handouts the documents are of, which is what a person confirms once they were printed.</summary>
    Task Delete(IEnumerable<ResultsDocument> documents);
}
