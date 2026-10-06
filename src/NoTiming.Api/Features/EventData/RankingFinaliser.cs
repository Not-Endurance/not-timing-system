using MongoDB.Bson;
using Not.Exceptions;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Shared;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>What one run of the finalisation found and did.</summary>
internal sealed class RankingFinalisationReport
{
    /// <summary>The Events that keep a Ranking some entry of which holds no rank: the ones the run looked at.</summary>
    public int Events { get; set; }

    /// <summary>The Rankings whose ranks were stored.</summary>
    public int Finalised { get; set; }

    /// <summary>The Events that hold a Ranking without placings and are not Historic, or are not there: nothing is written to them.</summary>
    public int Skipped { get; set; }

    /// <summary>The Rankings that hold only some ranks, which nothing here produces: they are left alone.</summary>
    public List<Guid> SomePlacingsStored { get; } = [];

    /// <summary>
    /// The Rankings that the domain refused to read or to finalise, one that counts a Participation that is not there or
    /// counts one twice: they are left alone.
    /// </summary>
    public List<Guid> Refused { get; } = [];

    /// <summary>The Events whose Rankings could not be read or written: they are tried again at the next run.</summary>
    public List<Guid> Failed { get; } = [];
}

/// <summary>
/// Stores the final placings of the Rankings of the Events that have ended (#640, ADR-0006, ADR-0007). It is the host's own
/// write, the only one an Event that has ended takes, and it is let through by one named rule and no other: the Event is
/// opened for <see cref="HostOperation.RankingFinalisation"/>, which gives it only when the clock of the host says it is
/// Historic, and what is written is written in the Tenant of the Event. What it stores is composed by the code that composes
/// the Results (<see cref="Ranking.Finalise"/>), from data that can no longer change, and it writes a Ranking only while no
/// entry of it holds a rank, so that a second run, or a second host, changes nothing. No route reaches it.
/// </summary>
internal sealed class RankingFinaliser
{
    const int PAGE = 500;

    readonly CrossTenantReads _reads;
    readonly EventDataAccess _access;
    readonly ILogger<RankingFinaliser> _log;

    public RankingFinaliser(CrossTenantReads reads, EventDataAccess access, ILogger<RankingFinaliser> log)
    {
        _reads = reads;
        _access = access;
        _log = log;
    }

    /// <summary>A Ranking none of whose entries holds a rank: the one state the finalisation writes to.</summary>
    internal static BsonDocument NoEntryPlaced()
    {
        return BsonDocument.Parse("""{ "Entries": { "$not": { "$elemMatch": { "Rank": { "$ne": null } } } } }""");
    }

    /// <summary>Finalises every Ranking whose Event has ended and that has no stored placings, in any Tenant.</summary>
    public async Task<RankingFinalisationReport> RunAsync(CancellationToken cancellationToken)
    {
        var report = new RankingFinalisationReport();
        foreach (var eventId in await _reads.ReadEventsWithUnplacedRankingsAsync(cancellationToken))
        {
            report.Events++;
            try
            {
                var facts = await _access.OpenForHostAsync(
                    HostOperation.RankingFinalisation,
                    eventId,
                    cancellationToken
                );
                if (facts is null)
                {
                    report.Skipped++;
                    continue;
                }

                await FinaliseAsync(facts, report, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // one Event whose documents cannot be read must not keep the others from their placings
                report.Failed.Add(eventId);
                _log.LogError(
                    ex,
                    "The Rankings of Event {EventId} could not be finalised; the next run tries again.",
                    eventId
                );
            }
        }

        return report;
    }

    async Task FinaliseAsync(EventFacts facts, RankingFinalisationReport report, CancellationToken cancellationToken)
    {
        var participations = (
            await ReadAllAsync(
                _access.Collection(EventDataFamilies.Participations(), facts),
                facts.Id,
                cancellationToken
            )
        )
            .Select(x => x.MapToEntity())
            .ToList();
        var collection = _access.Collection(EventDataFamilies.Rankings(), facts);
        var rankings = await ReadAllAsync(collection, facts.Id, cancellationToken);
        var rules = (await _reads.FindPublicEventAsync(facts.Id, cancellationToken))?.RegionalRules?.MapToEntity();
        foreach (var model in rankings)
        {
            RankingFinalisation finalisation;
            try
            {
                finalisation = model.MapToEntity().Finalise(participations, rules, facts.Stage);
            }
            catch (Exception ex) when (ex is GuardException or ValidationException)
            {
                // a Ranking that is not one, or whose Results cannot be composed, has no placings to keep, for the host as for a reader
                report.Refused.Add(model.Id);
                _log.LogWarning(
                    ex,
                    "Ranking {RankingId} of Event {EventId} cannot be finalised and is left as it is.",
                    model.Id,
                    facts.Id
                );
                continue;
            }

            switch (finalisation.Outcome)
            {
                case RankingFinalisationOutcome.Finalised:
                    var placed = new BsonArray(
                        RankingModel.From(finalisation.Ranking).Entries.Select(x => x.ToBsonDocument())
                    );
                    var written = await collection.UpdateWhenAsync(
                        model.Id,
                        NoEntryPlaced(),
                        new BsonDocument("$set", new BsonDocument("Entries", placed)),
                        cancellationToken
                    );
                    if (written)
                    {
                        report.Finalised++;
                    }

                    break;
                case RankingFinalisationOutcome.SomePlacingsStored:
                    report.SomePlacingsStored.Add(model.Id);
                    _log.LogWarning(
                        "Ranking {RankingId} of Event {EventId} holds only some of its ranks and is left as it is.",
                        model.Id,
                        facts.Id
                    );
                    break;
            }
        }
    }

    static async Task<List<T>> ReadAllAsync<T>(
        TypedTenantCollection<T> collection,
        Guid eventId,
        CancellationToken cancellationToken
    )
        where T : class, IDocument, IEventScoped
    {
        var rows = new List<T>();
        while (true)
        {
            var page = await collection.ReadAsync(x => x.EventId == eventId, null, rows.Count, PAGE, cancellationToken);
            rows.AddRange(page);
            if (page.Count < PAGE)
            {
                return rows;
            }
        }
    }
}
