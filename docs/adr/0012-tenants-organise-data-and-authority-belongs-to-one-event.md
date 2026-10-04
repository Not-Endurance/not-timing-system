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
