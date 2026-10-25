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
| `migrate-event-liveness` | Removes the `IsActive` flag from every Event, after listing the Events that were inactive and whose last day is still ahead, which are Live again (below). |
| `migrate-tenants` | Turns the data of before Tenants into the data of Tenants, and marks the database with the environment it is (below). |
| `migrate-identities` | A dry run that groups the copies of every Athlete and Horse into the identities they are, and writes a report and a file of decisions for the owner; with `--apply` it converts every id to a Guid, merges the identities, finalises the Rankings of the Events that have ended and verifies (below). |
| `mark-environment` | Says which environment a database is: Production, Staging or Development (below). |
| `seed-staging` | Makes a Tenant Root, a Main Operator, Officials, Operators and a Live Event on a staging database (below). |
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

1. Stop every host that writes to the database: the Api, and the Functions API too while it is still deployed (its code left the repository with #647). The command reads every Participation first and replaces the changed ones afterwards, so a write by another process in between would be overwritten, and the code that is deployed reads only the new shape.
2. Take a backup. Rollback is a restore of it; there is no way back from the new shape other than that.
3. Run `migrate-participation-copies --apply`, then this command with `--apply`, then the other data migrations of the runbook. Review each dry run before its apply.
4. Deploy the Api and the Ui together, and follow the runbook of #648 for old Judge builds, which are not supported afterwards.

Staging first, rehearsed on a restored copy of production data. There is no lazy upgrade and no dual-read: a Participation in the old shape that reaches the new code loads with no times, and the next write of it loses them for good, which is why the code is never deployed before this command has run.

## migrate-event-liveness

```powershell
dotnet run --project tools/NTS.Tools -- migrate-event-liveness --connection-string <mongo> [--database nts] [--apply]
```

ADR-0007 makes an Event Live until the end of its last day and Historic from then on, which the clock decides, and the code stores no flag. Before it, an Event stored `IsActive`: true from its start, and false once someone deactivated it or once its end had passed and a list of the active Events was read. This command removes the field from every document of the Events collection (`event_informations`). Old documents still load without it, so the removal is cleanup and no precondition of the code. What the command is really for is what comes before it:

- **An Event that was deactivated before its last day is Live again** from the release that carries #628, because the flag no longer says otherwise. The dry run lists those Events, so that the owner decides about each one before the flag that said otherwise goes.
- **The counts** say how many Events there are, how many are Live and how many Historic under the rule (an Event is Live while the clock is before the end of its last day, and Historic from that instant), and how many are deleted (soft-deleted), which are neither and are not asked about. They lose the flag like the others.

It is idempotent: a second run finds no flag to remove and nothing to ask about, so it changes nothing.

### The dry run

A dry run changes nothing. It prints the counts, how many documents store the flag, and two lists:

```text
Database: nts
Dry-run event-liveness migration.
As of 2026-10-06 12:00:00Z
event_informations: 14 documents read: 3 Live under the rule, 10 Historic, 1 deleted
IsActive: stored on 13 documents, to remove
Events that are inactive and whose last day is still ahead: 1
  event_informations 3f2504e0-4f89-41d3-9a0c-0305e82c3301 "Sofia CEI 2*": IsActive false, the last day ends 2026-10-11 20:59:59Z
Events whose EndDay is not a date: 0
Nothing was changed. Run again with --apply to persist.
```

- **Events that are inactive and whose last day is still ahead.** The flag says `false`, or says nothing while another Event still stores one, and the last day has not ended. Each is listed with its id, its name, what the flag said and when the last day ends. `--apply` refuses to run while this list is not empty, prints it, writes nothing and exits with 1. The owner resolves each one: correct its `EndDay` if the Event is over, or remove the Event, and run the dry run again. An Event with no flag is read as inactive only while some Event still stores the flag: once it is gone from every document (this command has run, or the Events were all made after #628) a Live Event has none, and nothing is asked.
- **Events whose EndDay is not a date.** The rule cannot be applied to an Event whose `EndDay` is missing or is a string or a document, so it is listed with its id, its name and the type it has, and `--apply` refuses for it too.

### The cutover

The order of the migrations and the steps of the cutover are owned by the runbook of [#648](https://github.com/Not-Endurance/not-timing-system/issues/648); this is what concerns this command, which touches the Events collection only and does not depend on the other migrations.

1. **Run the dry run before the release that carries #628**, between scheduled Events (ADR-0007), and resolve the list it prints: correct the `EndDay` or remove the Event, then run it again until the list is empty. Staging first, rehearsed on a restored copy of production data.
2. Take a backup. Rollback is a restore of it; there is no way back from the new shape other than that.
3. Run it with `--apply` only once the hosts are those of the release that carries #628, or are stopped, with the other data migrations of the runbook. The code before that release reads the flag, so an Event whose flag was removed is no longer active to it, and a Live Event would disappear from the old hosts.
4. The removal is one update of the collection. If it is interrupted, some Events have lost the flag and others have not, and an Event that was active then shows in the list of the next run: restore the backup and run the dry run again.

## seed-tenant-root and grant-developer

```powershell
dotnet run --project tools/NTS.Tools -- seed-tenant-root --connection-string <mongo> --tenant country-bg --email <exact email> [--database nts] [--apply]
dotnet run --project tools/NTS.Tools -- grant-developer --connection-string <mongo> --email <exact email> [--database nts] [--apply]
```

What only the Developer does, and only by command (ADR-0012): no route of the Api gives a role, so there is no way for anyone who is signed in to become a Tenant Root or the Developer except by being named here. Like the migrations, they are a dry run unless given `--apply`, and what they print names neither the connection string nor anything an account keeps.

- **`seed-tenant-root`** adds the role `tenant-root` to the Membership of the account in the Tenant (an id such as `country-bg`), or a Membership that has it when the account has none there. A Tenant becomes operational, able to hold Events, when it has a Tenant Root, and not before: nobody can create an Event in a Tenant that has none, the Developer included. The Tenant is made when the first person of its country registers, so it has to exist already, and so does the account, which is found by its exact email in any case: the person registers first, and the command is run after. Another Tenant Root of the same Tenant is another run with another email; a Tenant Root cannot make one.
- **`grant-developer`** sets `IsDeveloper` on the account. The Developer holds every right across Tenants except acting as the Main Operator of a Live Event, and belongs to no Tenant.

Both are idempotent: they change one thing of the user document in one atomic update, so running one twice changes nothing and nothing else of the account is touched. Running either on a hosted database is the owner's step, with the connection string supplied on the command line and never kept in a file; the exact commands for staging are in the hand-off of the checkpoint that needs them.

## mark-environment

```powershell
dotnet run --project tools/NTS.Tools -- mark-environment --connection-string <mongo> --environment <Production|Staging|Development> [--database nts] [--apply]
```

Production, staging and a developer's own database hold the same collections, so nothing in a database says which one it is. This writes the marker that does: one document, `environment/environment`, with the `Name` of the environment and when it was written. What must never touch a production database looks at it first: `seed-staging` and the local sign in as (the README of the repository) work only on a database that says it is Staging or Development, and refuse one that says Production, one that says nothing and one that says anything else. `migrate-tenants --apply` and `seed-staging --apply` write it for the database they prepare, so this command is for the databases that neither of them prepares: mark the production database `Production` first of all, before anybody points a machine at it, and a staging one `Staging`.

A marker is never changed to another name. Marking a database that says another name is refused with exit code 1, and so is `migrate-tenants --apply` with another `--environment`; marking one with the name it already has changes nothing. If a marker is wrong, remove the document by hand and run the command again. A dry run (no `--apply`) says what would be written.

## migrate-tenants

```powershell
dotnet run --project tools/NTS.Tools -- migrate-tenants --connection-string <mongo> --environment <Production|Staging|Development> [--database nts] [--main-operator <exact email>] [--apply]
```

ADR-0012 gives every document a real Tenant and every account a home Tenant and Memberships. Before it, every document carried the constant `"nts"`, and the accounts were the `users` documents the Functions API wrote. This command moves the data to the new shape. It reads everything first, then writes in an order that leaves a run that stopped to be finished by running it again, and each step writes only what is not there yet. Documents are read and written as they are stored, so what the command knows nothing of in them is kept.

- **The marker.** The database is marked with the environment it is (`--environment`). An apply needs it: it is how `seed-staging` and the local sign in as tell the database from a production one.
- **The accounts** get the fields Identity needs, in the form it keeps them: `EmailConfirmed` false, a `SecurityStamp`, `LockoutEnabled` and `AccessFailedCount`, and the email written without spaces and in lower case. The address stays unconfirmed. Every person comes back by signing in with a code sent to it, which proves it (ADR-0002), and passkeys are enrolled after that.
- **A home Tenant**, and a Membership in it, is given to every account that has none, from the country its profile names (`CountryRegion`, matched by name, ISO code or NF code in any case, against the countries in the order the Api lists them). An account whose profile names no country that has an ISO code is left without one until the person picks a country.
- **The Tenants** that are placed in are made (`country-bg`, and `country-<iso>` for another country), and Bulgaria's gets the rules the legacy hosts applied (`OnlyAverageLoopSpeed`). A Tenant that exists is completed in what it lacks and nothing else.
- **The data is stamped.** Every document of a collection a Tenant owns (`athletes`, `horses`, `clubs`, `configure_events`, `event_informations`, `event_officials`, `event_operators`, `event_participations`, `event_rankings`, `event_handouts`, `event_grants`) that has no Tenant, or only the constant, gets `country-bg`.
- **Every Event that is not yet Historic gets a Main Operator** (ADR-0012): the one its documents name already, or else the account of `--main-operator` when one is given, or else the only Tenant Root of its Tenant. A Historic Event gets none. An Event whose Tenant has no Tenant Root, or more than one, and that was not given an account to take, waits (see the cutover). An Event whose last day is not a date cannot be told from a Historic one and is taken to be over, and is listed.
- **The Officials and Operators that were linked by email** of every Event that is not yet Historic become grants: the grant of the account when the email has one, and a pending invitation by email otherwise. What a Historic Event links gives nothing, as nobody records on one, and is left in its documents.
- **The state a person kept** (`event_user_sessions`) is keyed by the account the person is, where the stored owner is an email that an account has. An owner that is anything else, such as the object id of an Entra account, is left as it is and counted: nothing maps it.
- **The settings collection** (`settings`) is dropped, as ADR-0012 deletes the aggregate, and **the indexes** the hosts make when they start are made first (the unique ones on the accounts, the codes and the grants), so that a unique index that fails on the data fails here, with this report beside it, and not as a host that does not start.

It is idempotent: a second run finds nothing to write, apart from what it was waiting for.

### The dry run

A dry run changes nothing. It prints every count the apply would write and what would stop it. Nothing in the report is an email or a name: accounts and Events are told by their ids, Tenants by their keys.

```text
Database: nts
Dry-run tenants migration.
Environment marker: none, to be written as Production.
countries: 251 documents read, Bulgaria (BG) is there, 0 without an ISO code
users: 412 documents read, 410 with an email, 2 without one, 412 to complete (3 emails to write in lower case)
  users 6a0b4d52-7c1e-4f33-8a0d-5d5e0c9b1f21: no email, left as it is
Accounts that share an email: 0 emails
Home Tenants to place: country-bg 371, country-ro 9, 32 accounts without one that stay so
tenants: 2 to make (country-bg, country-ro), 0 to complete ()
athletes: 1204 documents to stamp with country-bg
horses: 1311 documents to stamp with country-bg
event_informations: 14 documents to stamp with country-bg
Events that are not Historic: 2, 0 to give a Main Operator, 2 waiting
  country-bg has no Tenant Root: seed one with seed-tenant-root and run again, or name the account with --main-operator.
  event 3f2504e0-4f89-41d3-9a0c-0305e82c3301: no Main Operator yet
Started Events whose last day is not a date: 0, taken to be over
event_grants: 14 to make (3 pending invitations), 0 copies name an account that is not there
event_user_sessions: 31 to key by the account, 9 that stay as they are
settings: 1 documents, to drop
indexes: 12 to make (identity_email_unique, ...)
Nothing was changed. Run again with --apply to persist.
```

An apply refuses, writes nothing and exits with 1 while any of these holds; a dry run prints them as `An apply would be refused`:

- **No environment, or one that is none of Production, Staging and Development.** Say which one the database is.
- **The database is marked as another environment** than the one named. A marker is never changed.
- **Two accounts share an email**, whatever the case and the spaces. One person cannot be told from another, and the unique index on the email would fail. The ids are listed; resolve the accounts and run it again.
- **There is no Bulgaria (ISO code `BG`) among the countries**, so the Tenant everything is stamped with cannot be made.
- **`--main-operator` names an email that no account has.** The account has to exist, so that it can be signed in as.

What does not stop an apply is listed too, to be looked at: accounts with no email (left as they are), documents whose id is not a standard UUID (below), countries with no ISO code (no Tenant is made for them), Events that wait for a Main Operator, and Officials or Operators that name an account that is not there (no grant is made).

### The cutover

The order of the migrations and the steps of the cutover are owned by the runbook of [#648](https://github.com/Not-Endurance/not-timing-system/issues/648); this is what concerns this command.

1. **Staging first**, rehearsed on a restored copy of production data, with the dry run reviewed before the apply. Take the backup first: rollback is a restore of it, there is no way back from the new shape other than that.
2. **Stop the legacy hosts and the Api.** The command reads before it writes, so a write by another process in between would be overwritten.
3. **Run the dry run, read it, then apply** with `--environment Production` (or `Staging` on the staging database). Read the lists of accounts and of Events with care: they are the places the data has a mistake the command will not guess about.
4. **Seed the Tenant Root**: `seed-tenant-root --tenant country-bg --email <exact email> --apply` for the person who runs the federation. The account exists after step 3, so there is nothing to register first.
5. **Run `migrate-tenants --apply --environment <the same>` again.** It gives the Events that waited their Main Operator, the Tenant Root, and writes nothing else. Naming the account with `--main-operator` in step 3 does it in one run, but for every Event that has none, in place of the Tenant Root of its Tenant, so use it only when the Events are all one federation's.
6. **Run it once more after the ids are converted** (`migrate-identities`, #637): the documents the command counts as having another id (accounts, Events and their Setups that are not UUIDs yet) are left as they are until then, and the report says how many wait.

The Api and the Ui are deployed together afterwards, as the runbook of #648 has it. There is no dual-read and no lazy upgrade: the code that is deployed reads only the new shape.

## migrate-identities

```powershell
dotnet run --project tools/NTS.Tools -- migrate-identities --connection-string <mongo> [--database nts] [--report <path>] [--decisions <path>] [--apply]
```

ADR-0009 makes every id a Guid, and ADR-0010 registers an Athlete and a Horse once, in Setup, so that every copy of one carries the Guid of its row. Today one person exists as dozens of copies, with ids that were minted per copy or are 0, names in Bulgarian in some Events and in Latin in others, and a FEI ID on a minority. This command builds the registry from them in two halves. Without `--apply` it is **a dry run, which changes nothing in the database**: it reads, groups the copies by the rules of the matching service (`NTS.Domain.Setup.Services.Identities`, the rules the registry form and the search use too), and writes two files for the owner, and it tells what an apply would finalise, verifies the result an apply would give and lists what would stop it. With `--apply` it does what the owner decided (below).

- **It reads** every collection that holds an Athlete or a Horse, in whichever shape it has: the rows of Setup (`athletes`, `horses`), the Combinations of a configure event (its pool and the Participations of its Competitions), the Participations of a started Event, and the copies of a Participation that a Ranking and a Handout of before ADR-0006 still embed. A person is whatever is stored under a field called `Athlete` or `Horse`, so a nested shape is read as well. It also reads the names and the dates of the Events, to show them as evidence. The id 0 is no id. The integer id of a copy is its legacy id; a Guid or an object id is not one.
- **It is read-only and deterministic.** The same data and the same decisions give the same report, byte for byte: nothing in it is a date of the run or a path, and the order of everything is that of the keys, whatever the order the documents came in. The one thing that depends on the clock is the section on what an apply would do, which lists the Rankings of the Events that have ended by then. The reads are paged (100 documents at a time, in the order of the ids, so a page does not overlap the next) and a page is asked for again after the wait the server asks for when Cosmos DB for MongoDB throttles it (error 16500, `RetryAfterMs`), so it works on Cosmos DB for MongoDB and on Atlas. Run it with the hosts stopped, or on a restored copy: a collection that changes while it is read can show a document twice or not at all.
- **The report** (`--report`, default `.tmp/migrate-identities/report.md`) is Markdown: first the counts for Athletes and Horses (copies read and where they were, distinct spellings and legacy ids, identities found, Setup rows that exist, rows that would be created, Setup duplicates that would merge, copies whose Setup row is gone, items left to review); then **one section per automatic rule** (same FEI ID, same name, same name in another script, same legacy id) that lists each pair of spellings it makes one, so that no merge is silent; the Setup duplicates; the rows that would be created; and the **review list**: groups that are held, conflicts, spelling variants and near misses, each under its key with the evidence for every spelling: its FEI ID, legacy id, Country as stored (`Bulgaria (BGR)` and `Bulgaria (BG/BUL)` show apart), Club, copies by place and the rides they are of (a Participation that two Rankings copy is one ride), the Events with their dates, the Horses or Athletes it was entered with, and whether the two sides were ever in the same Event, which two people can be and one cannot.
- **The decisions file** (`--decisions`, default `.tmp/migrate-identities/decisions.json`) is JSON. The dry run writes it as a template with every item still to decide, and reads it back on the next run:

  ```json
  {
    "rules": { "Held": null, "Conflict": null, "SpellingVariant": null, "NearMiss": "different" },
    "items": {
      "athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev": "different",
      "athlete (fei 400): nikola todorov <> athlete: nikola todorow": "same",
      "horse: thunderbold <> horse: thunderbolt": null
    }
  }
  ```

  An item is `same` (one Athlete or Horse), `different` (two, and it is settled) or empty (still to decide, and so two for now). The **key** of an item is made of the normalised spellings of both groups (trimmed, spaces collapsed, lower case, in order) and their FEI IDs, never a database id, so it is the same on the next run, on a restored copy and after the ids are Guids; the report prints each item under its key to be found in the file. A **rule** settles every item of a class (`Held`, `Conflict`, `SpellingVariant`, `NearMiss`) that has no decision of its own, and an item's own decision beats the rule. A later run applies the decisions and no longer lists a settled item; it keeps everything the owner wrote in the file (and what it does not know of), adds the items that are new, and writes the file only when that changes it and the owner has not saved it since the run began (a decision saved while the run read the database is kept, the run says so, and is made again). A file that is not valid (not JSON, a word that is not `same` or `different`, a rule that is not a class) stops the run before anything is read or written, naming the item.
- **Held groups.** Spellings that an automatic rule joins through a chain can still disagree: two of them are equal names under different FEI IDs, say, joined by a third that has none. Such a group is held whole, and nothing in it merges by itself: it is decided as a whole (`same` makes it one, `different` keeps every spelling apart), under the key of the group. A `same` for a pair next to a group that is held and undecided waits for it, and the report says so.
- **The report also says** which decisions match nothing in this data (a typing mistake, or data that has changed), which `different` is overruled by other decisions of `same` that join the two (a contradiction, to resolve before applying), and which decision waits for a held group.

### The apply (#637)

`--apply` converts every id and every reference of every collection to a Guid (ADR-0009), merges the Athletes and the Horses that are the same and gives each a row of Setup (ADR-0010), stores the final placings of the Rankings of the Events that have ended (ADR-0006, #640) and verifies the result. It prints each step as it is done and each check of the verification, and exits with 1 when it is refused or a check fails.

- **The Guid of an id** is a name-based UUID (RFC 4122 version 5) of the id space, the thing the id is scoped to and the integer (`LegacyGuids`), so it is the same on every run, in every environment and on every machine, and nothing is stored to map one to the other. The table of ids (`IdPaths`) says, for every id of every collection, embedded ones included, the space it is in and what it is scoped to; a test over the types of the models fails when a model has an id that the table has not, or a collection that it does not know. A Combination is shared by a Setup and the Event that was started from it, and so are the Athlete, the Horse, the Club, the Country, the User and the Event: they are in one space each, and take one Guid. A Participation, a Ranking, a Handout, an Official, an Operator and a state a person keeps are scoped to their Event, a Phase of a started Participation to the Participation, and what a Setup embeds (Competitions, Phases, Loops, Participations, Officials, Operators) to the Setup. A started Participation, Phase, Official and Operator were made new when the Event started, so they are spaces of their own and not the Setup's. An id that was a Guid when it was made (a time event of a Phase) is left as it is. The `_id` of what a person keeps per Event is an object id and stays.
- **The Guid of an Athlete or a Horse** is the Guid of its identity, the same for every copy. It is the one that is written already in a row or a copy of the identity (copies that carry one Guid are one identity, whatever joined them before); else the one of its surviving row (of several rows, the one the most Participations ride on, which are those of the copies that carry its integer, then the lowest id), made of that row's id; else of its lowest id that is not 0; else of its FEI ID or its smallest name. The rows that are not the survivor are re-pointed and deleted, last. An identity with no row gets one, built from its latest copy (the latest Event, and in one Event the later place): the Name is the Cyrillic spelling if there is one and the Latin one otherwise, the NameEnglish the Latin spelling when the Name is Cyrillic, the Country the row of the countries that the copy's country is (by its id, its ISO code, its code of the federation or its name, so that Bulgaria entered as BGR or as BUL is the ISO country), the Club the copy's, in the Tenant of the document. A row that exists is never overwritten: it only gets an empty NameEnglish filled when its Name is Cyrillic. Core names are never changed.
- **It is refused, and writes nothing,** for what the data and the decisions do not agree on: a decision that other decisions overrule, a decision that waits for a group that is held, a decision that matches nothing, a copy with no name (these four only while some copy has no Guid yet: once every copy is converted the decisions settle nothing and the data has changed since they were taken, so a second run is no mistake), an identity that has two Guids already written (but not one that the rules alone joined, with every copy converted: that is what an earlier run left when it kept apart spellings that are one now) or whose copies carry one Guid while an item decision of `different` keeps them apart (a decision that contradicts a Guid already written), an id that the table cannot convert (a text, an integer where a Guid is made, an Event that cannot be told), and a collection that the table does not know and that has a number for an id. The dry run lists each as `An apply would be refused`. A refusal about a decision quotes its key, which holds the spellings of the people it is about, so the console can name them in an error and in nothing else: keep its output out of issues.
- **A run that stopped is finished by running it again.** The plan is made once, from the data before the first write, and kept in the collection `migrate_identities_plan` while the run lasts, because a copy that is converted no longer says what the matching needs (its integer id is the Guid now). A run that finds it finishes with it, if the decisions file is as it was (it keeps a fingerprint of the decisions and refuses otherwise), and the last step deletes it. It is one document, a few hundred kilobytes for the data of today (the limit of a document is 16 MB on Atlas, and 2 MB on Cosmos DB for MongoDB). If what stops a run half way is a name, an id or a decision that you then change, restore the backup and start again, because the kept plan was made from the old one. Each document is written on its own (no transaction is assumed): the Guid-keyed twin is written, then the original is deleted, and a document whose id stays is replaced in place; a document that is converted already changes nothing, so a run on converted data writes nothing. A throttled request (Cosmos DB for MongoDB error 16500) is made again after the wait the server asks for. The steps are `plan`, the collections (`countries`, `clubs`, `users`, `event_informations`, `configure_events`, `event_officials`, `event_operators`, `event_participations`, `event_rankings`, `event_handouts`, `event_user_sessions`, `athletes`, `horses`), `merged-rows`, `plan-consumed` and `rankings`; a test stops the run after each of them and runs it again.
- **The final placings** of every Ranking of an Event that has ended and has no rank on any entry are stored by the Results code (`Ranking.Finalise`), after the conversion, because that code reads only Guid-keyed documents; a Ranking of one entry is rank 1. It writes only while no entry holds a rank, so a second run, and the sweep of the Api, change nothing, and a rank that is stored is never rewritten. A Ranking with only some of its ranks, one that the domain refuses (it counts a Participation that is not there), and every Ranking of an Event whose rules cannot be read, are told and left alone, and are no failure of the verification. The dry run lists the Rankings it would finalise, found in the stored documents.
- **The verification** is a read-only pass after the apply, and in the dry run over the result the plan would give (the documents converted in memory). It prints pass or fail for: no integer id or reference remains (in the collections of the table, and no number for an id in any other); every reference resolves (the Event, the account and the Participation a document names); every Athlete and Horse id of every document is a Setup row's; every Ranking of an Event that has ended has its placings; the counts of Events, Participations, Rankings and Handouts are unchanged; and the names in Core are unchanged. An Athlete's `User` (retired by #635) is left in the documents as it is and is not looked at.

### What the owner reads and decides

1. Run the dry run, open the report, and read the counts. The rows that would be created and the copies whose Setup row is gone are people who rode but are not in Setup.
2. Go through the review list. Decide each item in the decisions file, or set a rule for a class when the whole class is alike (near misses of Horses that are plainly different animals, say), and run the dry run again until nothing is left, or what is left is to stay as it is: items still undecided when the tool is applied stay separate identities.
3. Keep both files out of git. They hold real names; the default folder `.tmp` is ignored, and a test pins that.

### Readings of the rules that the report shows

ADR-0010 has them: a zero FEI ID is none for both kinds; two different FEI IDs under names that are only a near miss are two people and are not listed; a legacy id shared under different FEI IDs is a conflict; a near miss is at most two edits of the words of the looser key **and fewer than half the length of the shorter name**, so that `Max` and `Mia` are not listed. The dry run ignores `TenantId`: the registry is one across Tenants.

### The cutover

The order of the migrations and the steps of the cutover are owned by the runbook of [#648](https://github.com/Not-Endurance/not-timing-system/issues/648); this is what concerns this command.

- It runs last of the data migrations (ADR-0009), after `migrate-participation-copies`, which turns the Ranking and Handout copies into references (so on migrated data the Rankings and Handouts hold no person and the counts for them are 0), after `migrate-phase-times` and `migrate-event-liveness`, and after the first run of `migrate-tenants`; the second run of `migrate-tenants` follows it (step 6 of its cutover), because the accounts, Events and Setups that had no Guid wait for the ids.
- Staging first, then a rehearsal on a restored copy of production data, with the report read and the decisions taken on the copy; the decisions are keys of spellings, so they carry over to production. The rehearsal ends with `--apply` on the copy and the checks read: the Guids a rehearsal gives are the Guids production will get.
- On the day: stop the hosts (the Api, which serves the Ui, and the Functions API while it is still deployed), take the backup, run the other data migrations, run the dry run once more and read what an apply would do and the verification of the planned result, resolve anything that would refuse the apply, run `--apply`, read the checks (the exit code is 1 if one fails), run `migrate-tenants --apply` again, and deploy the Api, which serves the Ui. The order and the steps are the runbook's (#648).
- Rollback is a restore of the backup: nothing converts a Guid back to an integer, and no old build reads the new data. A run that stopped is not a rollback; it is finished by running it again.
- It is deleted after the deploy, like the other migrations: the folder `tools/NTS.Tools/IdentityMigration` and its line in `Program.cs` and this section, and its tests (`Temporary/Identity*Tests.cs`, `Temporary/IdConversionTests.cs`, `Temporary/IdPathsTests.cs`, `Temporary/LegacyGuidsTests.cs`, `IdentityMigrationDocuments.cs`, `OutputFilesTests.cs`, `ThrottledReadsTests.cs` and `IdentitiesMigrationToolTests.cs` of the unit tests, `IdentitiesMigrationTests.cs`, `IdentitiesApplyTests.cs`, `IdentitiesApplyRankingsTests.cs`, `IdentitiesApplyToolTests.cs`, `IdentitiesApplyRandomWorldTests.cs`, `Infrastructure/IdentityRegistryData.cs`, `Infrastructure/LegacyWorldData.cs` and `Infrastructure/LegacyShape.cs` of the integration tests, and the reference to `NTS.Tools` in `NTS.Tests.Unit.csproj`). `Shared/ModelMapping.cs` stays, as `seed-staging` uses it.

## seed-staging

```powershell
dotnet run --project tools/NTS.Tools -- seed-staging --connection-string <mongo> --tenant-root <email> --main-operator <email> [--official <email>:<Role>]... [--operator <email>]... [--environment Staging] [--event-name <name>] [--days 7] [--start 00:00] [--database nts] [--apply]
```

What a person who tries the platform on a staging database needs, made in the Tenant of Bulgaria: a Tenant Root (which makes the Tenant operational), a Main Operator, Officials and Operators, and a Live Event with twelve Participations that the Officials, the Operators and the Main Operator may send Snapshots to. The accounts are named by their email and made when they are not there, with the address unconfirmed (each person signs in with a code sent to it); an account that exists is left as it is, but for the Membership and the role the seed gives it. The people and the horses of the Event are invented. The Event is made the way the Api starts one (its Setup, its Core, and the Officials, Operators, Participations and Rankings it copies and makes), and every id comes from the name of the Event and of the thing, so the same name is the same Event on every run: a run that stopped is finished by running it again, and `--days` moves its end.

- `--official` is an email and a role of the Setup (`Steward`, `ChiefSteward`, `GroundJury`, `GroundJuryPresident`, and the other roles), and can be given any number of times; so can `--operator`.
- `--environment Staging` (or `Development`) marks a database that has no marker. A database that says Production, or anything else that is not Staging or Development, is refused, and so is one that says nothing when `--environment` is not given: the seed never marks a database as Production and never writes into one.
- `--days` is how many days from today the Event is Live, today included (7 by default) and `--start` is the time of the day, in UTC, the competition starts, which a Snapshot is placed after (`00:00`).

A dry run (the default) says what would be made and what would stop it, naming accounts by their ids. An apply refuses with exit code 1 when the database is marked Production or is not marked and `--environment` is not given.

Run it on a hosted database only as the owner, with the connection string on the command line and never in a file, an issue or a log. The exact commands for staging are in the hand-off of the checkpoint that needs them.

