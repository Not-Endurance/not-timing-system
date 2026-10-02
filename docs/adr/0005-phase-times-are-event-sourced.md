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

Time events are for Staff only (ADR-0012). Only the Console shows them for now, and once the Ui serves a Participation by caller only Staff will. Until then they travel in the anonymous participation document, and only there (ADR-0006 leaves no copies of it and its change notification carries no domain content), with the Official's user id on Official-originated events (events recorded from the Console are labelled "Main operator"). That is accepted as harmless; there is simply no reason to show them to anyone else.

An Update can't rescue a snapshot that was rejected in a later Phase while an earlier Phase holds an accepted time. It is rejected, and the Phase form is the remedy. Delivery is at-least-once when a response is lost: a resent Snapshot is answered with its original outcome (ADR-0013), and a replayed Update is harmless because it sets an absolute value.

The copies of the participation in handouts and rankings no longer exist (ADR-0006), so this change only ever touches the participation documents.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
