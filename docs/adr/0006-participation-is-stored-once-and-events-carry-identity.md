# A Participation is stored once, and events carry identity, not entities

Status: proposed

A Participation lived in three places: its own document, once inside every Ranking that counts it, and once inside every Handout. Each of the six Participation events carried the whole Participation too, because that is how the copies were kept current: handlers overwrote their embedded or in-memory copy with the event's instance. The copies drifted (a Ranking refreshed on only three of the six events), every Phase completion rewrote each Ranking that counted the Participation whole, and the payload was Judge's own live aggregate, so every event from one call showed the final state. We now store a Participation once. A Ranking lists Participations by id, a Handout points at one, and Results and Handout documents are composed from the live Participation whenever they are needed. Events say which Participation changed, never what it looks like, and receivers read it again.

## What follows from it

**Rankings and Handouts hold references.** A Ranking is its header plus entries of `{ParticipationId, IsNotRanked, Rank}`, where `Rank` is the entry's final placing: null while the Event is Live, and stored once it is Historic (below). A Handout is `{Id, EventId, ParticipationId}` and is a live view: it prints the times as they stand when it is printed, including a later correction (ADR-0005). `Result` is a plain read model composed from a Ranking or a Handout, the Event's Participations and the Officials. One Participation can count in several Rankings, and `IsNotRanked` and `Rank` belong to the entry.

**Derived values are not stored.** `Total` and the nine per-Phase values (`OutTime`, the intervals and speeds, `IsComplete`, ...) are recomputed by the domain, ignored on load and read by nothing else.

**A Historic Event keeps its final placings.** A placing depends on every Participation of its Ranking, so recomputing one means composing the whole Result, and for an older Event the stored placings are the only record of what was printed. While an Event is Live the ranks are computed from the latest times and nothing is stored. Once it is Historic nothing about it can change (ADR-0007), so its Rankings are finalised: each entry's `Rank` is stored, composed by the same code as the Results. Nothing fires at `EndDay`, so a hosted service in NoTiming.Api finalises every Ranking whose Event has ended and has no stored placings, and until it has run readers compute them in memory. A Ranking with a single entry is finalised like any other, its only entry being rank 1; one with only some ranks stored is reported and left alone. The Results of a final Ranking read the stored ranks. The finalisation is the only write to a Historic Event: a host-internal operation, derived from data that can no longer change, exempt from the write guard of ADR-0007 and not reachable by any client.

**One change notification.** `ParticipationChanged{ParticipationId, Number}` is the only Participation event on the wire. Judge dispatches it once per persisted change, after the write succeeded, for every mutation, including the ones that raised nothing before (turning a request off, editing a non-current Phase, ADR-0005's Updates and Disables). The Event id travels in the envelope. `PhaseCompleted{ParticipationId, Number, PhaseId, IsFinal}` stays in-process, for Handouts. The other five events are deleted.

**One store per process.** The Event's Participations live in one store and every list and page is a view over it. On Judge it is the in-memory list the mutating service already owns; on Witness it re-reads the changed Participation by id, at most once at a time per Participation. It replaces the three Witness services and the private lists of the Startlist, Presentlist and Arrivelist services.

**The data moves once.** `migrate-participation-copies` in `tools/NTS.Tools` (a dry run unless `--apply`) turns Ranking entries and Handouts into references, keeps the stored rank of each entry, and removes the derived values. It runs before ADR-0005's `migrate-phase-times`. Its dry run reports entries whose Participation is missing and entries whose embedded copy differs from the stored Participation. The ranks that Rankings of Historic Events still lack are filled in by the last step of the cutover (ADR-0009), once the documents are Guid-keyed and the domain code that computes them can read them.

## Considered options

**Slim projections inside Rankings and Handouts.** Still two writers to keep in step; the drift returns.

**A frozen Handout.** Needs a stored projection of times and totals (duplication again) and keeps printing a time an Official has since corrected.

**Events that carry the changed values,** so receivers patch their copy without a read. Needs an apply path in every consumer, is order-sensitive (Witness handlers are not awaited) and diverges silently on a missed event.

**Six slim event types.** Every consumer reacts to all of them the same way and each type costs about eight places to change; one notification covers every mutation.

**Placings computed on read for Historic Events too.** A Participation's placing needs its whole Ranking composed, once per row of any list of one Athlete's or Horse's Participations, and the placings an older Event was printed with would be replaced by today's ranker. Kept for Live Events, where the times still change.

**Live references from Participations to Setup data** (Athlete, Horse, Club, Country, Loop lengths), so adjusting a Loop during an Event recomputes every speed and total. Deferred, not rejected: it changes what finished Events show whenever Setup data changes and needs Setup changes of its own, so it is a separate decision.

## Consequences

Past times and totals follow the Participation, which is the source of truth. Copies that had drifted stop being what is shown, and the dry run lists the differences for review before anything is applied. Placings are the exception: a Historic Event keeps the placings it was finalised or printed with, so a later fix to the ranker does not change them, and correcting one needs its own migration.

Each change costs every Witness one read by id where the events used to carry the state. Coalescing keeps bursts to one read per Participation. A revision number on the notification is the first lever if the load ever matters.

The wire no longer depends on the shape of a Phase, so ADR-0005 changes Phase without touching hub contracts, and Staff-only time events exist in one document type only.

Deleting five event types deletes the unit tests that pin them. The notification, and the store's coalescing, are tested instead.

No old Judge or Witness build is supported after the wire change. The first slice (store and notification, no data change) rolls back by reverting. The second slice (references and data) rolls back with the backup taken at cutover. One release with ADR-0004 and ADR-0005, in the order: this, then ADR-0004, then ADR-0005.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).

## Readings made when the placings were built (#640)

- **A Ranking is final when every entry holds a rank**, and so is one with no entries, which has nothing to place. `Ranking.Finalise(participations, rules, stage)` stores the placing of every entry of a Ranking that holds none, composed by the same code as the Results (a Ranking of one is ranked too, its only entry being rank 1), and comes back as the Ranking to keep with what came of it: finalised, already final, or only some ranks stored, which nothing here produces and which is left as it is. It refuses (a mistake of the caller, not a refusal) unless the Event is Historic.
- **The Results of a final Ranking show its stored ranks, in their order**, whatever the ranker would now say, and the Results of any other Ranking are ranked in memory as before. Nothing about the stage reaches the Results: a Ranking is final by what it holds, and only the host's finalisation, which runs after the end, writes ranks.
- **The sweep is a hosted service of the Api** (`RankingFinalisationSweep`): at start and then every hour by the injected `TimeProvider` it finds, across Tenants, the Rankings that have an entry with no rank, asks `EventDataAccess.OpenForHostAsync(HostOperation.RankingFinalisation)` for each of their Events (which gives the Event only when it is Historic by the clock, with its Tenant), and writes in that Tenant. The write is one update that applies only while no entry of the Ranking holds a rank, so a second run, or a second host, changes nothing. An Event whose documents cannot be read is logged and listed in the report of the run, and a Ranking that counts a Participation that is not there or counts one twice, which the domain refuses and whose Results cannot be composed for a reader either, is logged and left as it is: neither keeps the other Events and Rankings from their placings. A run that fails as a whole is logged and tried again an hour later. None of them stops the host. `RankingFinalisation:Enabled` (default on) turns it off; the test host turns it off unless a test asks, so that an Event a test seeds is not finalised behind its back. No route reaches it, and no setting needs to be added to a deployment.
- **The migration's last step (#637) calls `Ranking.Finalise`** for the Rankings that Historic Events already lack, as the sweep does, once their documents are the ones the code reads.
