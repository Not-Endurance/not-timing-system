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

## Readings made when the manual edits were built (#604)

The manual edit is the `PATCH` of a Participation (`PATCH /api/participations/{id}`), and these are the readings.

- **A change is a conditional update of the members it names, not a replace of the document.** It is applied when the document is at the version the change was based on, and the version is one more in the same write, so that of two changes made at one version only one is taken and the other is 409 `participation-changed`; the members it does not name are never overwritten. A document that no write has counted yet is at version 0.
- **The version is a whole number from 0 in the `meta` of the document.** A change without one is 400 `version-required`, one that is not a number is 400 `malformed-request`, and one that was made on another version is 409 `participation-changed` and changes nothing, even when it names no member. A change that names nothing, at the right version, changes nothing and is not announced.
- **A write that was stored is announced once, and one that was not is not.** The Participation that is made, changed or removed through the Api is announced to the clients of its Event after it was stored (`ParticipationChanged`); a refused write, a stale one and a change that named nothing announce nothing. A removal is announced too: the viewers read again and find the Participation gone.
- **Rankings, Officials and Handouts are last-write-wins and are not announced.** They count no writes; what the viewers follow is the Participations.
- **The Functions API does not count its writes.** What it writes of a Participation leaves the version as it was, so a change through the Api against a document it wrote at the same moment is not told to be stale until the Functions API is retired (#647).

## Readings made when the Snapshot POST was built (#644)

The routes are in `.claude/skills/rest-api/SKILL.md` (Snapshots), and these are the readings.

- **The device's Guid is the id of the time event.** `Snapshot` carries it, the Phase records the event under it, and a Snapshot sent again is found among the events of the Participation of its start number: the first outcome comes back (200), and nothing is written. An id that another Participation holds is 409 `id-taken`. Time events are found by id through an index on the Participations (`Phases.Events._id`), beside the one that finds a Participation by its Event and start number.
- **A Snapshot that was recorded is 201 whatever its outcome**: a duplicate arrive or a time that breaks the order is a rejected event, which the response carries with its reason (`outcome`), and which is announced like any stored write. A Snapshot for a start number the Event does not have records nothing (404 `participation-not-found`).
- **The write is a conditional update of the Phases and the elimination** (`UpdateAtVersionAsync`), against the version the Participation was read at. A Snapshot that loses is read again, placed again and written again, up to 25 times, without the Official noticing; none answers 409 for that. A Participation that cannot be written in 25 attempts is 409 `participation-busy` and the Snapshot is sent again, which is safe.
- **The Update is addressed by the id of the Snapshot it corrects.** The id tells the Participation and the kind (an Arrive or a Present); the Phase is the one the new time belongs to, as for a Snapshot. It is recorded as `ArriveUpdated` or `PresentUpdated` and replaces the latest accepted time of its slot there: the Arrive time, or the Present time, or the Represent time when the latest presentation the Phase accepted was a representation, so that it never makes one. A time that breaks the order is a rejected event and the answer says so. A time that is there may be corrected whether or not the Participation is complete, which a Snapshot may not do.
- **An accepted Update is evaluated like any accepted change**: the Participation is eliminated for time or restored when the correction decides it, the Phase completes, and the next one starts when it is out. An elimination for time was told by being the one instance the class held, which a Participation read again before every write never is, so the restore never happened for it; it is told by its codes now, with no reason in the complement, and an elimination that a person gave with a reason, or for another cause (the speed restriction, say, which the evaluation never makes), is not the evaluation's and is left as it is. One that a person gave for time without a reason cannot be told from the evaluation's, which a document does not say the author of.
- **Times are kept to the millisecond** at the edge, both the time the device captured and the instant the server recorded it, so that the answer to the first request and the answer to a resend, which is read from the document, say the same.
- **The Ui makes the ids of a group, and keeps a group that was not answered.** A group of Snapshots is one gesture, with an id of its own, and the id of each Snapshot is made from it and the start number, so the same group sent again is the same Snapshots to the server. The Ui sends again as the same group when the person sends the same Snapshots (the same numbers and times) as the same kind after a request that failed, or one of which the server recorded none: what the server did record and whose answer was lost comes back with its first outcome, and is not recorded twice. Another kind, a time that was changed, or Snapshots that were added are another gesture, with new ids, and so is what a partly recorded group leaves, because what was refused was not recorded. A reload forgets the group: the selection that comes back from the session has no ids, which the queue of the Snapshots a device could not send (#645) is to keep. The group the Ui puts in its history is the group that was sent, with its id, so that an Update of one of its Snapshots can name it (#618 stores the ids with the history).
- **What is gone with the Judge**: the pending-Snapshot store (its model, and its collection in a reset and in the name migration), the UDP handshake and what was left of the relay; the hub declares no method. The bearer and scope checks of the hub, the Judge hub and the primary-connection map went with Warp, and a test looks for them by name so that one that returns is noticed. Documents of the old `event_pending_snapshots` collection that a database still holds are not read by anything: the cutover drops the collection (#648).

Scenarios of the integration suite in which "Judge records a snapshot and both Witnesses update" change meaning; their assertions are edited only with the owner's approval per test (AGENTS.md rule 4). Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
