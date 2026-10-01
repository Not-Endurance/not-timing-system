# A Participation is stored once, and events carry identity, not entities

Status: proposed

A Participation lived in three places: its own document, once inside every Ranking that counts it, and once inside every Handout. Each of the six Participation events carried the whole Participation too, because that is how the copies were kept current: handlers overwrote their embedded or in-memory copy with the event's instance. The copies drifted (a Ranking refreshed on only three of the six events), every Phase completion rewrote each Ranking that counted the Participation whole, and the payload was Judge's own live aggregate, so every event from one call showed the final state. We now store a Participation once. A Ranking lists Participations by id, a Handout points at one, and Results and Handout documents are composed from the live Participation whenever they are needed. Events say which Participation changed, never what it looks like, and receivers read it again.

## What follows from it

**Rankings and Handouts hold references.** A Ranking is its header plus entries of `{ParticipationId, IsNotRanked}`; the stored `Rank` (always null) goes, because Results compute ranks. A Handout is `{Id, EventId, ParticipationId}` and is a live view: it prints the times as they stand when it is printed, including a later correction (ADR-0005). `Result` is a plain read model composed from a Ranking or a Handout, the Event's Participations and the Officials. One Participation can count in several Rankings, and `IsNotRanked` belongs to the entry.

**Derived values are not stored.** `Total` and the nine per-Phase values (`OutTime`, the intervals and speeds, `IsComplete`, ...) are recomputed by the domain, ignored on load and read by nothing else.

**One change notification.** `ParticipationChanged{ParticipationId, Number}` is the only Participation event on the wire. Judge dispatches it once per persisted change, after the write succeeded, for every mutation, including the ones that raised nothing before (turning a request off, editing a non-current Phase, ADR-0005's Updates and Disables). The Event id travels in the envelope. `PhaseCompleted{ParticipationId, Number, PhaseId, IsFinal}` stays in-process, for Handouts. The other five events are deleted.

**One store per process.** The Event's Participations live in one store and every list and page is a view over it. On Judge it is the in-memory list the mutating service already owns; on Witness it re-reads the changed Participation by id, at most once at a time per Participation. It replaces the three Witness services and the private lists of the Startlist, Presentlist and Arrivelist services.

**The data moves once.** `migrate-participation-copies` in `tools/NTS.Tools` (a dry run unless `--apply`) turns Ranking entries and Handouts into references and removes the derived values. It runs before ADR-0005's `migrate-phase-times`. Its dry run reports entries whose Participation is missing and entries whose embedded copy differs from the stored Participation.

## Considered options

**Slim projections inside Rankings and Handouts.** Still two writers to keep in step; the drift returns.

**A frozen Handout.** Needs a stored projection of times and totals (duplication again) and keeps printing a time an Official has since corrected.

**Events that carry the changed values,** so receivers patch their copy without a read. Needs an apply path in every consumer, is order-sensitive (Witness handlers are not awaited) and diverges silently on a missed event.

**Six slim event types.** Every consumer reacts to all of them the same way and each type costs about eight places to change; one notification covers every mutation.

**Live references from Participations to Setup data** (Athlete, Horse, Club, Country, Loop lengths), so adjusting a Loop during an Event recomputes every speed and total. Deferred, not rejected: it changes what finished Events show whenever Setup data changes and needs Setup changes of its own, so it is a separate decision.

## Consequences

Past Results follow the Participation, which is the source of truth. Copies that had drifted stop being what is shown, so an old Ranking can differ from what was last printed; the dry run lists the differences for review before anything is applied.

Each change costs every Witness one read by id where the events used to carry the state. Coalescing keeps bursts to one read per Participation. A revision number on the notification is the first lever if the load ever matters.

The wire no longer depends on the shape of a Phase, so ADR-0005 changes Phase without touching hub contracts, and Staff-only time events exist in one document type only.

Deleting five event types deletes the unit tests that pin them. The notification, and the store's coalescing, are tested instead.

No old Judge or Witness build is supported after the wire change. The first slice (store and notification, no data change) rolls back by reverting. The second slice (references and data) rolls back with the backup taken at cutover. One release with ADR-0004 and ADR-0005, in the order: this, then ADR-0004, then ADR-0005.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
