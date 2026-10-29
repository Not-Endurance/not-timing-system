# Tenants organise data, and authority belongs to one Event

Status: proposed

ADR-0002 put a tenant id on every document whose only value is the constant `"nts"`, read by nothing, and left tenant-scoped authorization out of scope. Today anyone who reaches the API can read or change any Event. A Tenant now means something: a country's equestrian federation. It is how data is organised and how authority is delegated, and it is not a wall around reads. A Tenant Root creates the Tenant's Events and appoints a Main Operator for each; the Main Operator links accounts to that Event's Officials and Operators; the Developer sits above every Tenant. Anyone may read an Event (ADR-0001), a few registries are searchable across Tenants on purpose, and writes and grants never cross a Tenant.

## What follows from it

**Roles.** Developer (the platform owner), Tenant Root (a role in one Tenant, held by one or more accounts), Main Operator (exactly one per Event), Operator (Snapshot access, never named in documents) and Official (named in documents) are defined in `CONTEXT.md`. What each may do:

| Action | Who |
|---|---|
| Read public Event views | Anyone, signed in or not |
| Read an Athlete's or Horse's history, search the registry across Tenants | Any signed-in account |
| See how times were recorded (time events) | Staff: Officials, Operators, the Main Operator |
| Send a Snapshot | An Official in {Steward, ChiefSteward, GroundJury, GroundJuryPresident}, any Operator, the Main Operator |
| Configure an Event and use the Console | The Main Operator |
| Create an Event | A Tenant Root |
| Assign the Main Operator | The Tenant Root while the Event is not Live; once Live, only the Main Operator hands it over |
| Link accounts to Officials and Operators | The Main Operator |
| Edit the Tenant's registry (Athletes, Horses, Clubs) | A Tenant Root, and the Main Operator of any Event of that Tenant that is not yet Historic |
| Tenant rules and Tenant branding; an Event's branding override | A Tenant Root; the Main Operator |
| Reset a started Event, delete an unstarted one | The Main Operator may reset while Live; a Tenant Root may delete before it starts; neither once Historic |
| Search accounts by name | A Tenant Root, the Main Operator |
| Seed a Tenant Root, grant Developer | The Developer, by command |

The Developer has every right across Tenants except acting as Main Operator on a Live Event. A Tenant Root cannot create another Tenant Root, and who is Main Operator can only be changed by the people above. A Live Event whose Main Operator is locked out is a database fix by the Developer, not an in-app action.

**Tenants appear, then become operational.** A Tenant is created when the first person from a country with an ISO code registers (ADR-0002, #601). It becomes operational, able to hold Events, when the Developer seeds its Tenant Root. A person whose country has no operational Tenant can still follow Events, search, read their own history and receive grants by email.

**Accounts are global and access is a grant.** One account has one passkey and may hold grants from several Tenants. A grant names an account by its exact email; an unknown email becomes a pending invitation that attaches when that person registers. There is no account listing. The account search by name is tenant-scoped, takes at least three characters, returns at most ten results with the display name and a partly masked email, is rate limited, and reaches across Tenants only when the caller asks for it explicitly.

**The tenant filter is part of every repository.** Every document carries a real tenant id in place of the constant, and every repository query filters by the current Tenant. Inside an Event the current Tenant is the Event's; elsewhere it is the signed-in account's selected Tenant (the home Tenant by default, with a switcher only when the account holds grants in more than one). Anonymous views use the public allowlist. A cross-tenant query is a named capability of a collection, never a flag a caller can pass to any repository, and exists for Athletes, Horses, Clubs, Officials, accounts and the public list of Events.

**Registry rows are owned by the registering Tenant and referenced across Tenants.** An Athlete, Horse, Club or Official has one row. Another Tenant finds it by the cross-tenant search and its Participation copy carries that row's Guid with the names as entered (ADR-0006, ADR-0010); it cannot edit the row, and the duplicate warning of ADR-0010 spans Tenants. A rider's whole history therefore spans Tenants.

**Tenant rules replace static options.** The rules of Regional competitions (judging loop speed on the average only, which ranker applies) are fields of the Tenant, edited by a Tenant Root and copied into the Event when it starts, as other Setup data is (ADR-0006), so a finished Event never changes when a Tenant edits them. `StaticOption`, `StaticSettings`, the `Setting` aggregate and `DetectionMode` are deleted: the `Setting` constructor assigns a process-wide static, which cannot work in one process serving several Tenants, and nothing reads `DetectionMode`. The country comes from the Tenant and the UI language is an account preference. Logos and header branding belong to the Tenant, with an override per Event, stored in Mongo.

**Public reads are an allowlist.** One list in the Api names the anonymous read views; everything else answers 401, Setup and registry reads included. Reading by Athlete or Horse stays for signed-in accounts (ADR-0010), and the routes that list users are deleted.

**The data moves once.** `migrate-tenants` assigns all data under `"nts"` to the Tenant of Bulgaria, turns existing Operators and Officials, who are linked by email, into grants or pending invitations, gives past Events no Main Operator and gives every Event that is not yet Historic its Tenant Root as Main Operator. Users re-enrol with a passkey (ADR-0002).

## Considered options

**Hard isolation between Tenants.** Breaks the rider's whole history across federations (ADR-0010) and public following of any Event.

**Ownership by user only, with no Tenant in authorization.** Has no place for the federation, for who appoints whom, or for the rules a federation sets.

**One registry row per Tenant.** A rider who competes in two countries is two people, and the history splits.

**The Tenant Root as an implicit Main Operator of every Event, or a Developer who can take over a Live Event.** The owner wants the person who runs an Event accountable and not overridable.

**Several Main Operators.** Nothing technical asks for it any more (ADR-0013); one accountable person per Event is the product rule, and hand-over covers a change of person.

**An account listing for granting.** Leaks every account to every Tenant; exact email and a guarded name search replace it. The owner chose a name search over email only, with the guardrails above.

## Consequences

The name search can show display names across Tenants when the caller asks for it, and is accepted with its guardrails. The Developer cannot rescue a Live Event from inside the product, only from the database. ADR-0002's tenant paragraph is amended (tenant-scoped authorization is now in scope, memberships carry the roles), and ADR-0010 now reads "registered once across Tenants". Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).

## Readings made when it was built (#643)

The matrix is the specification and one pure function evaluates it (`AccessPolicy`), checked row by row from the table above. Where the table is short of a sentence, the code takes the stricter reading, and these are the readings:

- **"Not Live" is "not started".** An Event is not started, Live until the end of its last day, then Historic (ADR-0007). A Tenant Root assigns the Main Operator and deletes an Event only before it starts; a Historic Event is read-only and nobody assigns, links, brands, resets or deletes it.
- **The Main Operator's rows are the Main Operator's alone.** Configuring an Event, linking accounts and branding it are the Main Operator's, before the Event starts and while it is Live. A Tenant Root who is not its Main Operator has none of them, however early: it assigns itself first. Handing over is the current Main Operator's, and only while the Event is Live; before it starts the Tenant Root assigns.
- **The Developer's every right excludes what the Main Operator does on a Live Event**, which includes sending a Snapshot: the Developer records times on a Live Event only if the Main Operator has linked it as an Operator or an Official, like anybody else. Before the Event starts the Developer configures, links, brands, assigns and deletes in any Tenant. An account that is the Main Operator by appointment holds the role as anybody does, the Developer included.
- **An Event is made in the Tenant the caller acts in** (the selected Tenant, the home Tenant by default), which has to be operational even for the Developer. Nothing sent names a Tenant or a Main Operator.
- **The Main Operator is named by account.** An assignment or a hand-over names an account by its id, found by the guarded name search, so no route confirms whether an email has an account. A grant is named by the exact email or by an account, and its answer says whether it is pending, which a Main Operator is trusted with.
- **The searches.** The account search is open to a Tenant Root, to the Main Operator of an Event of the Tenant that is not yet Historic (the same people as the registry edit) and to the Developer; its named view across Tenants is for the same people in any Tenant. The registries of Athletes, Horses, Clubs and Officials are searched across Tenants by any signed-in account, with the same limits and the same budget.
- **Invitations are taken at the sign-in with a code.** A pending grant attaches to the account that proves its email, which registering does too, and attaching is never a reason for a sign-in to fail: when the grants cannot be written it is logged and tried again at the next sign-in. An Event holds at most 500 grants, a guard against a flood and not a quota.
- **What changes who may do what is logged.** An Event made, a Main Operator assigned or handed over, a grant given or removed, the rules of a Tenant edited and the invitations taken are events 1101 to 1108, each with the id of the user who did it and the ids of what it was done to. None carries an email, a name or anything else an account keeps.

## Readings made when the reference data moved (#603)

The registries, the countries and the Setup of an Event became resources of the Api (ADR-0008). The matrix is short of a sentence in five places, and these are the readings; the first three are rows of the policy (`EditSetup`, `ReadSetup`, `EditCountries`), checked in the table of its unit tests.

- **The Setup is frozen at the start.** The Main Operator configures an Event until it starts, and once it has the Console works on the copies the Event made of it (ADR-0006), so nobody changes its Setup, not even while it is Live, which is why this is narrower than "configure an Event", which the Main Operator does while it is Live too. A change of a started Setup is 409 `event-started` or `event-ended`. The Developer may change a Setup before the start and, as elsewhere, not what the Main Operator does on a Live Event.
- **Who reads a Setup.** The Main Operator, a Tenant Root of the Tenant that holds the Event and the Developer, at every stage; a list of Setups is the Tenant's for a Tenant Root and the Developer, and for anybody else it is those of the Events the account runs now. A started Event is the Core's, so a hand-over moves its Setup from the list of the Main Operator who had it to the list of the one who has it, and a list shows the Core's Main Operator for a started Event, as reading the Event by its id does. An Official or an Operator does not read the Setup: what they see of an Event is the public views and what the capabilities give them.
- **The countries are the Developer's.** They are the platform's reference data, which belongs to no Tenant and is read by every account; nobody removes one.
- **Any signed-in account reads the registry of its Tenant.** The matrix says who edits it and that any signed-in account searches it across Tenants, so a row is read by its id by anybody signed in, as the rows found by the search are; "Public reads are an allowlist" keeps an anonymous caller out of all of it.
- **The link of an Athlete to an account is the server's.** It holds an email, so no route shows it, takes it or lets it be filtered on, and an edit of the Athlete leaves it as it was. It goes with the Athletes' own rework (ADR-0010).
- **A client that makes its ids may name them.** A create takes the id of the document, and the same id again is the row that was made, which is how an idempotent create works for anything a client makes the id of; another Tenant's row with the id is not told about.
- **Deleting an Event takes its grants** with it, so that none is left waiting for an Event that is not there.
- **The legacy routes stay until the Functions API is retired (#647).** The Functions project still answers the old routes of the families that moved, open as they were; the clients of the platform no longer call them.

## Readings made when what an Event keeps moved (#604)

What an Event makes when it starts and works on while it is Live, its Participations, Rankings, Officials and Handouts, became resources of the Api (ADR-0008, ADR-0006). The matrix says "use the Console: the Main Operator" and says nothing of the stage, and these are the readings; the first is a row of the policy (`EditEventData`, checked in the table of its unit tests).

- **The Main Operator of a Live Event writes them, and nobody else.** Only a Live Event has them to change: an Event that has not started is a Setup, which has none (409 `event-not-started`), and one that has ended is read-only (409 `event-ended`). The Developer's every right stops short of acting as the Main Operator of a Live Event, so the Developer is refused too (403 `not-main-operator`), and a Tenant Root who is not the Main Operator of the Event is refused as at every other row. Making, changing and removing are the one row.
- **Anybody reads them (ADR-0001), by the Event.** A list names the Event at the head of its filter, because the Event says which Tenant it is read in: the Tenant of a row is the Tenant of its Event, told by the Event and never by the caller, and no parameter takes one. A row is read by its id whatever the Tenant, as the public views show it.
- **A row keeps its Event.** The Event is named when a row is made, and a change that names it, the Tenant or the version is refused. A row made with an id that is a row of another Event, or of another Tenant, is 409 `id-taken`, and is not told about.
- **The account an Official is linked to is the grants' business.** It holds an email, so no route shows it, takes it or lets it be filtered on, and a change of the Official leaves it as it was; whom an Official or an Operator is linked to is told by the grants of the Event (`LinkAccounts`).
- **A Participation that is named is not removed.** A Ranking holds the ids of the Participations it counts and a Handout the id of its Participation (ADR-0006), and the Results of a Ranking cannot be composed without one of them, so removing a Participation that either still names is 409 `participation-in-use`, as ADR-0010 has it that what is referred to cannot be deleted. Whether the Console takes the entries out and removes the Handouts first, or the server does it for it, is the owner's to say when the Console is built (#646).
- **Operators are not a resource.** They are grants. What a person may do about an Event is told by its capabilities, and the Ui asks them (`canSnapshot`) instead of working it out from lists of Officials and Operators, which the Api does not give away.

## Readings made when the migration was built (#607)

"The data moves once" is short of a sentence in a few places. The commands and what they print are in `tools/NTS.Tools/README.md`, and these are the readings.

- **A database says what it is.** Production, staging and a developer's own database hold the same collections, so one document, `environment/environment`, says which. `migrate-tenants --apply` needs `--environment` and writes it, `seed-staging` and the local sign in as (ADR-0002) work only on a database that says it is Staging or Development, and refuse one that says Production, one that says nothing and one whose marker says anything else (a misspelt marker is not read as "not production"), and a marker is never changed to another name.
- **Existing accounts keep their address unconfirmed.** ADR-0002 has every person re-enrol: they sign in with a code sent to the address, which is what proves it, and add a passkey after. The migration completes an account with the fields Identity needs and confirms nothing.
- **The Main Operator of an Event that is not Historic is the one it has, else the account the command is told, else the only Tenant Root of its Tenant.** An Event whose Tenant has no Tenant Root or has several, and that was told of nobody, waits: the Developer seeds the Tenant Root and runs the command again, which gives it its Main Operator and writes nothing else. A Historic Event gets none, as the paragraph says, and the Officials and Operators it linked become no grants: nobody records on a Historic Event, so a grant on it would give nothing.
- **A person's state is re-keyed only where its owner is an email.** What the Functions API stored as the owner of a state (`event_user_sessions`) is the identifier of the sign-in of its day. An email that an account has is turned into the id of the account, and any other, such as the object id of an Entra account, is counted and left, because nothing maps it.
- **What waits for the ids.** A document whose id is not a standard UUID (an account, an Event or its Setup) is counted and left as it is until the ids are converted (ADR-0009, #637), so the command is run again after them and every step is the same on the second run.
- **The indexes are made first.** The unique ones of the hosts (the email of an account, a code, a grant) can fail on data, and failing in the command with its report beside it is better than a host that does not start. The tools are .NET 8 and cannot reference the identity library, so the definitions are replicated, and a test runs the makers of both hosts over a database the command made, so they cannot drift apart unnoticed.
- **The seed is invented data.** `seed-staging` makes a small Live Event of invented people and horses, with the documents the Api makes when it starts an Event (made in one place, `EventStartDocuments`), because the data of the golden Event is in the shape of before the migrations. A person who tries the platform on a staging database is a Tenant Root, a Main Operator, an Official or an Operator of it.
