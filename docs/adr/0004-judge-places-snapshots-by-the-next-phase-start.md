# Snapshots are placed by the next Phase's Start, and no untyped input is accepted

Status: proposed

Amended by ADR-0013: the server, not Judge, places and records Snapshots. The rule below is unchanged.

Judge used to accept a time typed in by the Operator with no type and guess Arrive, Present (or Represent) or Finish from what the Phase already held (`SnapshotType.Automatic`). It also treated a snapshot stamped within 30 minutes after a completed Phase's Out time as belonging to that Phase. That input predates the mobile Witness and nothing else sends it, yet it shaped `Participation.Process` into a resolve-then-re-process routine around a `Current` Phase that is rebuilt on every load as the last complete Phase. We remove the input and place every snapshot with one comparison: at or after the next Phase's Start it belongs to the next Phase, otherwise to the current one. Representation and Inspection requests apply the same comparison to the time of the request, and anything the flow can't place is corrected by the Main Operator in the Phase form.

## Considered options

**Keep `Automatic` and only replace the 30-minute window with the next Start.** Keeps the one code path that needs type resolution, for an input nobody uses.

**Compute the Phase for a time statelessly** (walk the Phases, the caller choosing the reference time, toggles using the latest recorded time so no clock is involved). Rejected as more machinery than the problem needs: the existing `Current` plus one comparison is enough, and toggles simply use the server's clock.

**Move on only from a *complete* Phase, as today.** Dropped. Once Representation is requested Phase 1 is incomplete again while Phase 2's stored Start still holds the old Out, but a Represent stamped after that Out is never valid, so the guard protects nothing.

## Consequences

A new capture stamped after Out now lands in the next Phase. Before, the grace absorbed it in the previous one: a stray Arrive was rejected as a duplicate, but a stray Present silently overwrote the presentation and moved Out. Such strays are now fixed in the Phase form, and become visible and disableable once the timeline of ADR-0005 ships.

Requests compare against the server's clock, the only clock involved, and a Snapshot carries the time its Official captured. Timestamp comparisons stay time-of-day, so rides crossing midnight remain unsupported, as before.

`SnapshotType.Final` and the separate-finish plumbing are untouched. Nothing produces `Final` today, but it belongs to the planned separate-finish setting.

Nothing persisted changes, so this rolls back by reverting. Two integration helpers still build `Automatic` snapshots and need explicit Arrive and Present ones.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
