# NTS.Tools

Command-line tools for the data. Run one with:

```powershell
dotnet run --project tools/NTS.Tools -- <command> [options]
```

| Command | What it does |
| --- | --- |
| `watcher` | A placeholder. |
| `migrate-names` | Moves the names of Athletes, Horses, Officials and snapshots to `Name` and `NameEnglish`. |
| `migrate-participation-copies` | Turns the copy of a Participation inside every Ranking entry and Handout into a reference, and drops the values the domain derives (below). |
| `migrate-phase-times` | Turns the flat Arrive, Present and Represent times of every Phase into time events, renames the Representation request, and drops the snapshot-results collection (below). |
| `seed-tenant-root` | Makes an account a Tenant Root of a Tenant, which makes the Tenant operational (below). |
| `grant-developer` | Makes an account the Developer (below). |

A migration is a dry run unless it is given `--apply`. Pass the connection string with `--connection-string`; never paste it into an issue, a log or a file in the repository.

## migrate-participation-copies

```powershell
dotnet run --project tools/NTS.Tools -- migrate-participation-copies --connection-string <mongo> [--database nts] [--apply]
```

ADR-0006 stores a Participation once. Before it, a Participation lived in its own document, once inside every Ranking entry that counts it and once inside every Handout, and each stored the values the domain derives from the times beside the times. This command moves the data to the new shape:

- **A Ranking entry** becomes `{ParticipationId, IsNotRanked, Rank}`: the id of the copy it held, the not-ranked mark and the stored rank, both kept as they were. A mark that is false and a rank that is null are not written, as the application does not write a default.
- **A Handout** holds the id of its Participation in `ParticipationId` where the copy was, and everything else of it as it was.
- **A Participation** loses its `Total` and the nine derived values of each Phase: `RequiredInspectionTime`, `OutTime`, `LoopInterval`, `PhaseInterval`, `RecoveryInterval`, `AverageLoopSpeed`, `AveragePhaseSpeed`, `AverageSpeed` and `IsComplete`.

It works on the documents as they are, whatever the type of their ids: the integers of before as well as Guids. It is idempotent: a document already in the new shape is left alone, so running it again changes nothing, and a run that stopped halfway is finished by running it again.

### The dry run

A dry run changes nothing. It prints, per collection, the documents read and the documents it would change (and, for Rankings, the entries it would convert), then three lists to review:

```text
Database: nts
Dry-run participation-copies migration.
event_rankings: 7 documents read, 7 to change (26 entries)
event_handouts: 3 documents read, 3 to change
event_participations: 26 documents read, 26 to change
Participations missing: 0
Participations listed twice in one Ranking: 0
Entries whose copy differs from the stored Participation: 1
  event_rankings 1042029701 Entries[3]: 1926599985: Phases[0].PresentTime
Nothing was changed. Run again with --apply to persist.
```

- **Participations missing.** An entry or a Handout that names a Participation which is not there: it names none, the id is not in `event_participations`, or the Participation belongs to another Event. Each is listed with its collection, document and place. `--apply` refuses to run while this list is not empty, prints it, writes nothing and exits with 1. What to do about each is for the runbook of #648; run the dry run again after it.
- **Participations listed twice in one Ranking.** The application does not load a Ranking that lists a Participation in two entries, so converting one would leave a Ranking nothing can open. `--apply` refuses for this too, like it does for a missing Participation.
- **Entries whose copy differs.** The copy inside the entry is not what the stored Participation is now, and after the migration the entry shows the stored one, so the Results of a Ranking shown again for a Historic Event carry the stored times, not the ones it was printed with. The fields that differ are paths in the Participation document (`Phases[0].PresentTime`). The derived values do not count, as they follow from the times. Review the list before applying; it is the one place the migration changes what is shown.

### The cutover

The order of the migrations and the steps of the cutover are owned by the runbook of [#648](https://github.com/Not-Endurance/not-timing-system/issues/648); this is what concerns this command.

- It runs first of the data migrations, before `migrate-phase-times` (ADR-0005), and so before `migrate-identities` (ADR-0009), which converts the ids last; the references it writes are converted with the other ids.
- Staging first, rehearsed on a restored copy of production data, with the dry run reviewed before the apply.
- Run it with the legacy hosts stopped: it reads every document first and replaces the changed ones afterwards, so a write by another process in between would be overwritten.
- Rollback is a restore of the backup taken before the cutover. There is no way back from the new shape other than that.
- The placings that Historic Events still lack are not filled in here. The last step of the cutover (#637) does it, because the code that computes the placings reads only Guid-keyed documents.
- There is no dual-read, no lazy upgrade and no support for old builds: the new code reads only the new shape, and a Ranking entry or a Handout that names no Participation does not load.

## migrate-phase-times

```powershell
dotnet run --project tools/NTS.Tools -- migrate-phase-times --connection-string <mongo> [--database nts] [--apply]
```

ADR-0005 makes a Phase store every time it receives as a time event and show its Arrive, Present and Represent times from the latest accepted event of each, and the code that does it reads no flat time. Before it, a Phase stored the three times flat beside its Start, and every Snapshot left a snapshot result in a collection of its own. This command moves the data to the new shape:

- **A flat time** that is a date becomes one event of the Phase, in the order Arrive (`Arrived`), Present (`Presented`) and Represent (`Presented`, marked as a representation): accepted, of the manual method, with a new id, the time as it was, and no recorded-at time and no actor, which is how the domain tells a time that was there before events were kept. The flat members are removed. A time that is null is removed and makes no event. A Phase with no times is left as it is.
- **The Representation request** `IsReinspectionRequested` is renamed `IsRepresentRequested`. The Start and the other flags are not touched.
- **The snapshot-results collection** (`event-snapshotResults`) is dropped, on apply only. Nothing reads it any more, and its documents are discarded, not migrated.

It works on the documents as they are, whatever the type of their ids. It is idempotent: a Phase that already has events is not given events again, only its leftover flat times and old flag are removed, so running it again changes nothing, and a run that stopped halfway is finished by running it again.

### The dry run

A dry run changes nothing. It prints the Participations read, the ones it would change with the events it would make, the snapshot results it would drop, and two lists that stop an apply:

```text
Database: nts
Dry-run phase-times migration.
event_participations: 26 documents read, 22 to change (118 events)
event-snapshotResults: 340 documents, to drop
Times that are not a date: 0
Rankings and Handouts that still hold a copy of a Participation: 0
Nothing was changed. Run again with --apply to persist.
```

- **Times that are not a date.** A flat time whose value is something other than a BSON date (a string, a document) cannot be turned into an event without guessing, so it is listed with its Participation and place (`Phases[0].PresentTime`) and the type it has. `--apply` refuses to run while this list is not empty, prints it, writes nothing and exits with 1.
- **Rankings and Handouts that still hold a copy of a Participation.** `migrate-participation-copies` has not run, or has not run on them. This command runs after it (ADR-0005), so `--apply` refuses for this too, like it does for a time that is not a date.

### The cutover

The order of the migrations and the steps of the cutover are owned by the runbook of [#648](https://github.com/Not-Endurance/not-timing-system/issues/648); this is what concerns this command.

1. Stop the legacy hosts, the Functions API and the Api. The command reads every Participation first and replaces the changed ones afterwards, so a write by another process in between would be overwritten, and the code that is deployed reads only the new shape.
2. Take a backup. Rollback is a restore of it; there is no way back from the new shape other than that.
3. Run `migrate-participation-copies --apply`, then this command with `--apply`, then the other data migrations of the runbook. Review each dry run before its apply.
4. Deploy the Api and the Ui together, and follow the runbook of #648 for old Judge builds, which are not supported afterwards.

Staging first, rehearsed on a restored copy of production data. There is no lazy upgrade and no dual-read: a Participation in the old shape that reaches the new code loads with no times, and the next write of it loses them for good, which is why the code is never deployed before this command has run.

## seed-tenant-root and grant-developer

```powershell
dotnet run --project tools/NTS.Tools -- seed-tenant-root --connection-string <mongo> --tenant country-bg --email <exact email> [--database nts] [--apply]
dotnet run --project tools/NTS.Tools -- grant-developer --connection-string <mongo> --email <exact email> [--database nts] [--apply]
```

What only the Developer does, and only by command (ADR-0012): no route of the Api gives a role, so there is no way for anyone who is signed in to become a Tenant Root or the Developer except by being named here. Like the migrations, they are a dry run unless given `--apply`, and what they print names neither the connection string nor anything an account keeps.

- **`seed-tenant-root`** adds the role `tenant-root` to the Membership of the account in the Tenant (an id such as `country-bg`), or a Membership that has it when the account has none there. A Tenant becomes operational, able to hold Events, when it has a Tenant Root, and not before: nobody can create an Event in a Tenant that has none, the Developer included. The Tenant is made when the first person of its country registers, so it has to exist already, and so does the account, which is found by its exact email in any case: the person registers first, and the command is run after. Another Tenant Root of the same Tenant is another run with another email; a Tenant Root cannot make one.
- **`grant-developer`** sets `IsDeveloper` on the account. The Developer holds every right across Tenants except acting as the Main Operator of a Live Event, and belongs to no Tenant.

Both are idempotent: they change one thing of the user document in one atomic update, so running one twice changes nothing and nothing else of the account is touched. Running either on a hosted database is the owner's step, with the connection string supplied on the command line and never kept in a file; the exact commands for staging are in the hand-off of the checkpoint that needs them.

