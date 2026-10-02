# The server records every time, and nothing needs an Operator connection

Status: proposed

ADR-0004 and ADR-0005 made the Main Operator's Judge app the one writer: it placed every Snapshot, serialized every mutation of a Participation and answered an Official's Update over the hub within ten seconds, and while no Judge was connected Warp parked messages in an ordered pending store and answered "queued". One Judge per live Event was enforced by an in-memory map in Warp. That makes one browser tab a correctness dependency (no time is recorded while it sleeps), needs a lease and heartbeat in a web app, and lets a client decide what is written. Neither ADR is built yet. We decided that the server records. An Official's Snapshot is a request to the API, which applies the placement rule of ADR-0004, records the time event, saves it, answers in the HTTP response with the outcome and sends the Change notification (ADR-0006). The Console is one more viewer of what the server stores. There is no exclusive connection, no lease and no pending store.

## What follows from it

**A Snapshot is a resource.** The Official's device creates it with `POST`, and the response is the recorded time event with its outcome: accepted, or rejected with a reason, which stays in the Phase's history as in ADR-0005. A request from someone who is not Staff is refused, and a write to an Event that is no longer Live answers 409 `event-ended` (ADR-0007). The exact payload belongs to the spec and the `rest-api` skill.

**Idempotent by id.** The device mints the Snapshot's Guid (ADR-0009). A resend returns the original outcome, so a phone with a poor connection keeps unsent Snapshots and sends them again until it gets an answer. An Update of a sent Snapshot (ADR-0005) sets an absolute value, last write wins, and is answered the same way.

**The server places and compares.** ADR-0004's rule runs on the server. A Snapshot carries the time its Official captured; requests and toggles compare against the server's clock, the only clock involved, which removes the risk that Judge's clock disagrees with the Officials' devices.

**Optimistic concurrency, not a lock.** A Participation document carries a `version` that every write increments, and every write is a conditional replace on it. A Snapshot that loses a race is reloaded and retried without the Official noticing. A manual edit (the Phase form, disabling or enabling a time event, an elimination, a request flag) sends the version it was based on in the resource's `meta`; a stale one is refused with 409 `participation-changed` and the Console reloads the Participation and keeps what was typed; a missing one gets 400 `version-required`. The version is per Participation, which is the unit of write (ADR-0005, ADR-0006), so it also makes writes safe across several instances. Setup resources have human editors only and stay last-write-wins.

**The hub says only that something changed.** `ParticipationChanged` (ADR-0006) is the one message, sent by the API after a persisted write and carrying the Event and Participation ids. Anyone may join an Event's group (ADR-0001), nothing on the hub is data, and receivers read through REST, where authorization applies. The Judge hub, its procedures and relay, `JudgeConnectionsContext`, the pending-snapshot store, the hub write path and the UDP handshake are deleted.

**The Console is a viewer.** The Main Operator may open it in several tabs or on several devices. A client that has lost its role, after a hand-over, finds out from its next request (403) and no push is added.

## Considered options

**The Console processes (ADR-0004 and ADR-0005 as written).** Keeps the specs, but makes one tab the writer, needs an exclusive connection with a lease and heartbeat, an acknowledgement protocol and a pending store, and trusts the client's computation.

**The server processes behind a per-Event in-process lock.** Simpler to write and single-instance for good. The version check gives the same correctness and keeps scale-out open.

**Edit locks ("someone is editing this").** Needs the heartbeats this decision deletes.

**Last-write-wins for manual edits.** A form built from a stale view can act on a state that has moved; a staged disable could negate the wrong event.

**A field-level merge, or a version per Phase.** More machinery than the problem needs. A version per Event would conflict constantly.

## Consequences

ADR-0004 and ADR-0005 are amended in place (they are only proposed): the server, not Judge, places Snapshots and answers Updates, over HTTP, with no queue. ADR-0006's "one store per process" paragraph (Judge's in-memory list) and ADR-0007's remark that a running Judge learns of an end from its next rejected write follow, and are edited when those files are free of other changes: the Console reads like any other viewer and learns of an end from a 409 `event-ended`.

A manual save that succeeded but whose response was lost gets 409 on the retry; the Console reloads and finds its change applied. Edits to two Phases of one Participation conflict although they are unrelated, which is rare and cheap to redo. Times are recorded while no Console is open, and an Official sees the outcome at once.

Scenarios of the integration suite in which "Judge records a snapshot and both Witnesses update" change meaning; their assertions are edited only with the owner's approval per test (AGENTS.md rule 4). Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
