# Live and Historic Events share the Core views, and an Event is Live until the end of its last day

Status: proposed

Past events were a second flow: their own service and pages, read-only by convention, while every Core view was bound to the one live connection and to in-memory state that its handlers keep current. Whether an Event was live was a stored flag, set at Start, cleared lazily when a list of active events was read or by a "Deactivate Event" action, and nothing on the server stopped a write to an Event that had ended. We now treat an Event as Live until the end of its last day, derived from `EventSpan` and an injected clock, and the server refuses writes to it from then on. Every Core view takes its Event from the route and reads it through a viewed-event provider. The live provider wraps the existing live services and the connection; the historic provider reads by Event id with no connection and reports that nothing can be written. The same views render both, and the separate past-event pages and service retire.

## What follows from it

**Liveness is a rule, not a field.** The domain decides it from `EndDay` (23:59:59 on the last day, in the Event's own offset) and a clock injected in Nexus and Judge (`TimeProvider`). `IsActive` and the Deactivate action are removed. ADR-0008 exposes the rule as `/events/live`, `/events/historic` and a read-only `isLive` attribute. There is no grace after `EndDay`.

**The server guards every write.** Writes to Event-scoped resources (Participations, Rankings, Handouts, Officials and Operators) are refused once the Event is no longer Live, with 409 and the error code `event-ended`. The rule is the domain's, so it survives the retirement of the Functions project. A client treats that rejection as the Event having ended: the view becomes a Historic Event in place, the live connection closes, controls disable and a banner says why. No end notification is added to the wire.

**Viewing never disturbs the live connection.** A Judge or Witness holds at most one live connection. Opening a Historic Event uses the historic provider and leaves that connection, and everything its handlers hold, untouched. Picking a different Live Event replaces the connection after a confirmation. The view's capabilities come from its provider: a Historic Event shows the Rankings and Results (printable, FEI export) and the Participation detail with recorded times only. It hides the Startlist, which is about who starts next and is of no use as a record (the owner's call, #630), Handouts, the Arrivelist and Presentlist, Snapshot capture and Performance, and every operation that changes the database. Printing takes the provider as a parameter, because the print renderer builds its own dependency scope.

**Where the user picks.** Judge lists Historic Events in an expandable section under the configure-events list. Witness keeps a separate tab, renamed History. Access stays anonymous (ADR-0001, ADR-0003).

**The data moves once.** `migrate-event-liveness` in `tools/NTS.Tools` (a dry run unless `--apply`) removes `IsActive`. It lists every Event that is inactive with an `EndDay` still ahead, because those would become Live again, and refuses to apply while any exist. It runs between scheduled Events, in the same release as the clients.

**The words follow the glossary.** `IsActive` and `PastEvent*` become the terms in `CONTEXT.md` (Live Event, Historic Event), and the routes are renamed with them.

This is written against ADR-0005 and ADR-0006: Results are composed from the stored Participation, and only recorded times are shown.

## Considered options

**Keep the stored flag, renamed `IsLive`.** It is a cached copy of `EndDay` that lags (it is cleared only when a list of active events is read) and is written during reads. Once Deactivate is gone, expiry is the only thing that clears it.

**Swap the live context's Event while a Historic Event is viewed.** The handlers hold per-Event state keyed by start number and `EventConnected` wipes it, so another Event's #12 overwrites the live #12. Connecting elsewhere also drops the live connection, and Witness users start to time out.

**A dependency scope per route.** Large and risky: injection does not reach descendants, framework services are uninitialised in a child scope, and a scoped socket opens a second connection.

**Keep the past-event pages and service.** Every new Core view would be built twice and the two would drift.

**Read-only in the UI and the client services only.** The API still accepts the write from any client.

**A grace period after `EndDay`.** Needs a new setting. The organiser can set `EndDay` later instead.

## Consequences

A ride that finishes after 23:59:59 on the last day is refused, and so is the Snapshot that would record it. Rides crossing midnight are already unsupported (ADR-0004), and `EndDay` has to be configured with the last ride in mind. An Event can no longer be ended early.

A running Judge learns that its Event has ended only from its next rejected write, and until then it keeps showing live controls.

Historic Events show recorded times only. Whether Staff see the time-event history there waits for Staff identity in the passkey work (umbrella issue 597).

Integration tests make an Event historic by advancing the injected clock past `EndDay`. The tickets that remove Deactivate and rename the past-event types edit those tests under an explicit exception to rule 4 of `AGENTS.md`.

Reverting the views is a revert. The migration rolls back with the backup taken at cutover.

## Readings made when the guard was built (#629)

- **The guard is the access policy, row by row.** Every write route of what an Event keeps asks `AccessPolicy` about the Event the row belongs to, and the rows are Live only (`EditEventData`, the Main Operator's manual edits; `SendSnapshot`, the Snapshot of #644) or before the end (`LinkAccounts`, which is how an Operator is linked, as grants replaced the Operator documents). A Historic Event answers 409 `event-ended` and an Event that has not started 409 `event-not-started`, and a role is checked before the stage. The instant `EndDay` is Historic: an Event is Live while the clock is before it (`EventStageRule`), tested with the clock at the last second of the Event and at its end. A delete by id names no Event, so the Event is found from the row first.
- **The host's own writes are one named rule.** `HostPolicy` names the operations the host does by itself to an Event, with nobody to ask, and lets one through to an Event that has ended and to no other stage: the finalisation of the placings of its Rankings (#640). The host asks `EventDataAccess.OpenForHostAsync`, which gives the Event only at that stage, and no route of the Api reaches it; the same Ranking written by a client is refused like any other write.
- **A client is told the code.** The JSON:API repository keeps what the Api answered with its code (`LastError`), so a caller reads `event-ended` where it would have read any other refusal, and the others keep their own codes (`participation-changed`, `not-main-operator`). What the views do about it (#631) is not decided here.
- **The writes of the Functions API are not guarded** until it is retired (#647); nothing of the platform calls them.

## Readings made when the viewed Event was built (#630)

- **The Api's list says which provider.** `IViewedEventProvider.Open(eventId)` asks the Api's list of Live Events: an Event on it is opened as Live, any other Event the Api has is Historic, and an id the Api does not have opens nothing. Whether an Event is Live is the Api's rule and its clock (#628), so the Ui applies it to no clock of its own. The stage of a view is fixed when it is opened; the flip when the Event ends is #631's.
- **One view per route Event id, never shared.** `ViewedEventScope` opens the Event of the route, cascades it to the Core views under it and disposes it when it is replaced or the page leaves. The address of a page that names no Event (`/startlist`, `/snapshot`, the bookmarks of the viewers of old) is the same page for the Live Event the app follows; the pages of an Event are `/events/{eventId}/...`, and the record of an Event, Live or Historic, is `/historic-events/{eventId}`.
- **The live view wraps, the historic view reads.** `LiveEventView` wraps the store the live views already share, which the change notification keeps current (ADR-0006), and what the Api says the person may do; `HistoricEventView` reads the Participations, Rankings and Officials by the Event's id through repositories that belong to no connection, and is not a handler or a singleton of anything. Opening a Historic Event never touches the connection (a test holds the live connection and its store while one is opened). Opening a Live Event that the app does not follow connects to it: the address is the pick, so there is no confirmation.
- **What a stage shows and whether a view may write are domain rules.** `EventViewPolicy.Shows(view, stage)` names the Core views a stage shows (a Historic Event: Rankings, Results and the Participation detail) and `CanWrite(stage, permitted)` is the Live stage and the Api's answer together. A page that the Event does not show says so and leads to the record of the Event; it is not reachable.
- **Services refuse as well.** `ISnapshotService.Publish` and `RePublish` take the view and throw a `DomainException` before anything is sent when it cannot write; the controls are off for the same reason. The Console's services (#646) take the view the same way.
- **Printing and the FEI export come back with the Console (#646).** The provider gives what they take: the Event, its Rankings and the Results document of a Ranking. So does the picker of Historic Events under the configure-events list, which is the Console's list.

## Readings made when the migration was built (#632)

- **The flag is read as an old shape.** An Event with no `IsActive` counts as inactive only while some Event still stores one. Once the flag is gone from every document, because the command has run or because the Events were all made after #628, a Live Event has none and is not asked about, which is what lets a second run change nothing.
- **Deleted Events are counted and not asked about, and they lose the flag with the others.** An Event whose `EndDay` is not a date cannot be judged, so it is listed and stops an apply, like an Event that would be Live again.
- **The apply waits for the new hosts.** The code before #628 reads the flag, so an Event that lost it is no longer active to that code and would drop out of the old hosts: the dry run is run before the release and the owner resolves its list, and `--apply` runs with the release or with the hosts stopped (`tools/NTS.Tools/README.md`).
