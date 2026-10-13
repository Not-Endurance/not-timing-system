# Phase times are event-sourced, and Officials Update sent Snapshots through acknowledged requests

Status: proposed

Amended by ADR-0013: the server, not Judge, answers an Update, in the HTTP response, with nothing queued; writes check a version instead of waiting their turn behind one writer.

A Phase stored its Arrive, Present and Represent times as flat properties overwritten in place, every received Snapshot left only a side record (`SnapshotResult`), and an Official could not correct a Snapshot once it was sent. A Phase now stores every time event it receives, accepted or rejected, each with its outcome, and its times are the latest accepted event of each kind. An Official Updates a sent Snapshot's time from the Witness History, the server answers the request with the result, and the Main Operator sees every event in a timeline and can disable the ones that were wrong. Time events are for Staff only.

## What follows from it

**The events live inside the Phase**, in the participation document. Arrive, Present and Represent are projected from them. Start and the request flags stay flat properties, and Start moves in place whenever the previous Phase's Out time changes. The kinds are `Arrived`, `Presented` (with `IsRepresent`, set from the request flag: a re-presentation is still a presentation), `ArriveUpdated`, `PresentUpdated` (carrying `IsRepresent`) and `PhaseUpdated` (the Phase form, listing only the times the Main Operator changed). An Update is its own event rather than another `Arrived` or `Presented`, because it records a different intent.

**The outcome is on the event**: accepted, rejected with a reason (duplicate arrive, duplicate present, participation complete, the two separate-finish reasons, invalid time), or manually rejected. `SnapshotResult` is deleted with its model, REST functions and repositories. Invalid times, which `Phase` throws on today and nothing records, become rejected events.

**An Update is an acknowledged request, not a fire-and-forget message.** The server places it with the rule of ADR-0004 using the new time, replaces the latest accepted time of that kind (last write wins), enforces the ordering rules, and answers in the HTTP response with what changed or why it was rejected (ADR-0013). The Official waits for the answer and can cancel. Nothing is queued: the server records an Update whether or not a Console is open. Every write to a participation is a conditional replace on its version, so concurrent writers retry or conflict instead of overwriting each other.

**The data moves once.** `migrate-phase-times` in `tools/NTS.Tools` (a dry run unless `--apply`) turns flat times into accepted events in the participations, and drops the snapshot-results collection. It runs after ADR-0006's `migrate-participation-copies`, which leaves no participation copies in handouts and rankings to migrate. No old Judge is supported afterwards.

## Considered options

**A change log beside the flat state.** The smallest change, but two sources of truth that can drift and nothing to revert from.

**Event-sourcing the whole Participation.** Nothing here needs it: eliminations and the request flags stay flat.

**Events in their own staff-only collection, the participation keeping flat projected times.** Would enforce "Staff only" on the wire today and make rollback a collection drop, but every change becomes two writes across REST calls with no transaction, and Witness would need a second, flat Phase representation.

**Addressing an Update by snapshot id or old time, with a stale guard.** Protects against Updating a previous Phase or overwriting someone else's time. Rejected: updating previous Phases is not an expected use, only the latest History entry per number and type is editable, the ack shows exactly what changed, and the rare timeout is covered by a double check with the Main Operator.

**Fire-and-forget Updates like Publish, or a pushed outcome.** Rejected: the failure that matters is an Official believing a rejected Update landed.

**A derived Start.** Rejected in favour of keeping Start a flat property updated in place.

## Consequences

Participation documents change shape. Rollback is a restore of the backup taken at cutover, so cutover is: stop the API, back up, apply the migrations (ADR-0006's first, then this one), deploy the Api and the Ui together (ADR-0011).

Time events are for Staff only (ADR-0012). Only the Console shows them, and the Api serves a Participation by caller, so that only Staff read them (#619, below); until it did they travelled in the anonymous participation document, and only there (ADR-0006 leaves no copies of it and its change notification carries no domain content), with the Official's user id on Official-originated events.

An Update can't rescue a snapshot that was rejected in a later Phase while an earlier Phase holds an accepted time. It is rejected, and the Phase form is the remedy. Delivery is at-least-once when a response is lost: a resent Snapshot is answered with its original outcome (ADR-0013), and a replayed Update is harmless because it sets an absolute value.

The copies of the participation in handouts and rankings no longer exist (ADR-0006), so this change only ever touches the participation documents.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).

## Readings made when the timeline was built (#619)

The timeline is a dialog of the dashboard of the Console, opened for the Participation the Main Operator works on, and these are the readings.

- **The caller decides who reads how a time was recorded, on the one resource.** A Participation tells everybody the times each of its Phases shows (`arriveTime`, `presentTime`, `representTime`, left out when the Phase shows none) and tells its time events, with what became of each, how it was acquired, who recorded it and when, to Staff alone: an Official or an Operator linked to the Event, its Main Operator and the Developer, which is the policy's `SeeTimeEvents`, asked on every read with the grants read at that moment, so that a grant that is pending, or one that is of another Event, makes nobody Staff. A visitor and an account that is not Staff read the same Participation with an empty list of events. A client makes a Phase from its events when it has them and from the times it is told when it has none, so a viewer's pages show what they showed, and a Phase that is made from times has the same times as the one that was made from the events. The times are not stored; the Api tells them with the events or in their place. What a write answers is the Participation as Staff read it, because only the Main Operator writes.
- **Disabling and enabling is one change of the Participation** (`Participation.ChangeTimeEvents`). An event that was accepted is disabled: its outcome becomes the manual reject and it keeps the instant it was disabled, so that the latest accepted event before it takes over the time again; an event that was disabled is enabled and accepted again. What the rules rejected is neither, and neither is an event that is not as it was when it was chosen (it changed, or it is gone): the change is refused, and it is made whole or not at all. The same instant is kept when the Phase form clears a time, since that disables the events that feed it too.
- **The change is refused when it would take a Phase that was complete and leave it incomplete while a later Phase has times.** The Start of the later Phase is the Out time of the earlier one, so its times would stand on a Start that no longer matches. It is asked of the state the change leaves, so a change that takes the later times away too is allowed, and of a Phase the change makes incomplete, so a Phase that was incomplete already does not block what the person does to it. The refusal names the events that were chosen to be disabled in that Phase, and nothing is applied. Within a Phase no order of the times is asked for: the Presentation of a Phase may stand without its Arrival, as a delayed Arrival looks exactly like that.
- **The change is evaluated once, over the Phases it touched.** The Participation is eliminated for time when a Phase that changed is over the limit, and the elimination the evaluation made is lifted only when no Phase at all is, so that a Phase that is fine does not undo what another one calls for. An elimination that a person gave is left as it is. A Phase that changed and is complete starts the one after it when its Out time moved, and is announced once. A Phase that is left incomplete announces nothing and leaves the Start of the next one where it is, as a form that clears a time does. The elimination of a Snapshot or of an Update of one (`Process`, `UpdateSnapshot`) is decided on the Phase it touches alone, so it can lift an elimination for time that another Phase still calls for; that is not changed here.
- **The Console writes it as a manual edit** (ADR-0013): the change is made on the Participation the store holds and sent with the version it was read at, so a Participation that changed meanwhile is refused (409 `participation-changed`), it is read again, and the dialog keeps what was chosen, as far as it can still be done, to be saved over what is there now. A view that may not write refuses before the Api is asked.
- **The people are named by a route of their own** (`GET /api/time-event-actors`, `.claude/skills/rest-api/SKILL.md`), because an event carries the id of the account and a name belongs to the account. It names the accounts that recorded a time of the Event to the people who may see how times were recorded, so that the timeline can say who it was, and it is the only place a name appears. An account that is not known is left unnamed.
- **What the dialog offers.** Every event of every Phase in the order it was recorded, with its kind, time, outcome, source, who recorded it and when; the one the Phase shows now is marked. An accepted event is grayed out when it is pointed at and says "Click to disable", a disabled one says "Click to enable", and one the rules rejected says why and is not clicked. A click stages the change and a second one takes it back; Save is off while nothing is staged and writes the whole choice at once; closing leaves the choice behind.
