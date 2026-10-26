# Tenants organise data, and authority belongs to one Event

Status: proposed

Amended by #671: the single Main Operator and the exception for a Live Event are replaced by Levels, Root and Console Operators, and one declared mechanism enforces the matrix. The first form is described under Considered options.

ADR-0002 put a tenant id on every document whose only value is the constant `"nts"`, read by nothing, and left tenant-scoped authorization out of scope. Today anyone who reaches the API can read or change any Event. A Tenant now means something: a country's equestrian federation. It is how data is organised and how authority is delegated, and it is not a wall around reads. Authority is a Level held at a place. Root, which is the Developer anywhere and a Tenant Root in its own Tenant, holds every right and may run any Event or assign Console Operators to it; a Console Operator, assigned to one Event, works its Console and links the Operators and Officials who record on it. Anyone may read an Event (ADR-0001), a few registries are searchable across Tenants on purpose, and writes and grants never cross a Tenant. One central mechanism decides who may do what from a declared table of rows, so that no endpoint decides it for itself.

## What follows from it

**Levels.** A caller stands at one of five Levels at a place, and each holds everything the ones below it do: Anonymous (not signed in), Account (signed in, holding nothing there), Staff (an Operator or an Official linked to the Event by a grant), Console Operator (assigned to the Event by a grant) and Root (the Developer, or a Tenant Root of the Event's Tenant). The place is an Event, a Tenant or the platform, and the Level is per place and not once: a Tenant Root in one Tenant can be an Operator of an Event in another and a Console Operator of a third. At a Tenant a Console Operator is one of any Event of it that has not ended; across Tenants it is one of any such Event, or a Tenant Root of any Tenant, which are the people who set an Event up. At the platform the Developer alone is Root. The Level comes before the stage of the Event (not started, Live, Historic: ADR-0007), so a person who may not do an action is only told that. The roles are defined in `CONTEXT.md`: Developer, Tenant Root, Console Operator, Operator and Official.

**What each Level may do.** A row needs the Level it names or a higher one, at the place and the stage it names:

| Row | Place | Needs | When |
|---|---|---|---|
| Read public Event views | none | Anonymous | any |
| What an account does for itself or reads of the registries: its profile, state and passkeys, its link to an Athlete or a Horse, the history of a Participant, the registry of its Tenant, the search by name across Tenants (the name and the name in English only) | none | Account | any |
| See how times were recorded (time events) | Event | Staff | any |
| Send a Snapshot | Event | Staff whose grant may record: any Operator, an Official in {Steward, ChiefSteward, GroundJury, GroundJuryPresident} | Live |
| Configure an Event: start it, change it, brand it, link Operators and Officials | Event | Console Operator | not started, Live |
| Edit the Setup | Event | Console Operator | not started |
| Read the Setup and who has access | Event | Console Operator | any |
| Run the Event: edit what it keeps, reset it | Event | Console Operator | Live |
| Assign and remove Console Operators | Event | Root | not started, Live |
| Delete an Event | Event | Root | not started |
| Create an Event | Tenant | Root | any, the Tenant must be operational |
| Tenant rules and Tenant branding | Tenant | Root | any |
| Edit the Tenant's registry (Athletes, Horses, Clubs); search its accounts by name | Tenant | Console Operator | any |
| Read the whole row of an Athlete or a Horse of another Tenant (FEI ID, club, country), match a search against its FEI ID, search accounts across Tenants | platform | Console Operator | any |
| Edit the countries | platform | Root | any |

Seeding a Tenant Root and granting Developer are not rows: the Developer does them by command, and no route does.

**Root runs everything, and several may run one Event.** Root holds every row at every stage the row allows, a Live Event included. Nothing needs one writer: the server records every time and the Console is one more viewer of it (ADR-0013), so several Console Operators and Root may work an Event at once. A Tenant Root cannot create another Tenant Root. A Level gives the Level below it and never its own: the Developer gives Tenant Roots, by command; Root gives Console Operators; a Console Operator gives Operators and Officials; Staff give nothing. A Live Event cannot be locked out, because Root assigns another Console Operator.

**Console Operators are grants.** A Console Operator is a Grant of one Event, beside the Operator and the Official: named by an exact email or by an account, pending until the person registers with that email, and ended when removed. It is a resource of its own (`console-operators`), so that every route carries one fixed requirement: assigning and removing are Root's, and the Operator and Official grants are a Console Operator's. An Event has none until Root assigns one and needs none, and nothing is assigned when an Event is made.

**What a refusal says.** A row answers with the Level it needs: 401 `not-signed-in`; 403 `not-allowed` (it needs Staff), `not-console-operator`, `not-root`, and `not-developer` (the platform's own row); and the state of the place, 409 `event-not-started`, `event-started`, `event-ended` and `tenant-not-operational`. The Level is checked before the stage.

**The order of the answers.** A request is answered in this order, and the first thing wrong is the answer: signed in (401); the transport (415, 413, 400); the place it names, found once (404); the Level against the row, then the stage (403, 409); the content (400, 422); then the work. A person who may not do an action never learns what is wrong with its content. The place may be named in the route, in the filter, in the body, or only by the id of a row, which is why the transport comes first.

**How it is enforced.** An endpoint declares what it needs and where its place is named, as an attribute on its handler (`[Requires(Capability.ConfigureEvent, PlaceIn.Body)]`), and writes nothing else of it. One endpoint filter, registered once for the routes of the API, does the rest in the order above: it finds the caller, the place and the caller's Level there (the grants are read once), asks the policy, runs the validation of the request (ADR-0008), and hands the handler the valid request and a context with the caller and the place. The policy is one pure function of a Level and the state of the place, and the capabilities of an Event or a Tenant are its answers for the caller, so a screen and the server cannot differ. It fails closed: the host does not start when a route of the API is neither on the public list nor declares a requirement, and a test enumerates the real endpoints. The host's own jobs are not callers and are not asked (ADR-0007).

**Tenants appear, then become operational.** A Tenant is created when the first person from a country with an ISO code registers (ADR-0002, #601). It becomes operational, able to hold Events, when the Developer seeds its Tenant Root. A person whose country has no operational Tenant can still follow Events, search, read their own history and receive grants by email.

**Accounts are global and access is a grant.** One account has one passkey and may hold grants from several Tenants. A grant names an account by its exact email; an unknown email becomes a pending invitation that attaches when that person registers. There is no account listing. The account search by name is tenant-scoped, takes words of which one has at least two characters, returns a page of results (ten by default) with the display name and a partly masked email, is rate limited, and reaches across Tenants only when the caller asks for it explicitly.

**The tenant filter is part of every repository.** Every document carries a real tenant id in place of the constant, and every repository query filters by the current Tenant. Inside an Event the current Tenant is the Event's; elsewhere it is the signed-in account's selected Tenant (the home Tenant by default, with a switcher only when the account holds grants in more than one). Anonymous views use the public allowlist. A cross-tenant query is a named capability of a collection, never a flag a caller can pass to any repository, and exists for Athletes, Horses, Clubs, Officials, accounts and the public list of Events.

**Registry rows are owned by the registering Tenant and referenced across Tenants.** An Athlete, Horse, Club or Official has one row. Another Tenant finds it by the cross-tenant search and its Participation copy carries that row's Guid with the names as entered (ADR-0006, ADR-0010); it cannot edit the row, and the duplicate warning of ADR-0010 spans Tenants. A rider's whole history therefore spans Tenants.

**Tenant rules replace static options.** The rules of Regional competitions (judging loop speed on the average only, which ranker applies) are fields of the Tenant, edited by Root and copied into the Event when it starts, as other Setup data is (ADR-0006), so a finished Event never changes when a Tenant edits them. `StaticOption`, `StaticSettings`, the `Setting` aggregate and `DetectionMode` are deleted: the `Setting` constructor assigns a process-wide static, which cannot work in one process serving several Tenants, and nothing reads `DetectionMode`. The country comes from the Tenant and the UI language is an account preference. Logos and header branding belong to the Tenant, with an override per Event, stored in Mongo.

**Public reads are an allowlist.** One list in the Api names the anonymous read views; everything else answers 401, Setup and registry reads included. Reading by Athlete or Horse stays for signed-in accounts (ADR-0010), and the routes that list users are deleted.

**The data moves once.** `migrate-tenants` assigns all data under `"nts"` to the Tenant of Bulgaria, turns existing Operators and Officials, who are linked by email, into grants or pending invitations, and assigns nobody to run an Event: Root runs it until it assigns a Console Operator. Users re-enrol with a passkey (ADR-0002).

## Considered options

**Hard isolation between Tenants.** Breaks the rider's whole history across federations (ADR-0010) and public following of any Event.

**Ownership by user only, with no Tenant in authorization.** Has no place for the federation, for who appoints whom, or for the rules a federation sets.

**One registry row per Tenant.** A rider who competes in two countries is two people, and the history splits.

**Exactly one Main Operator per Event, and a Root that cannot act on a Live Event** (the first form of this decision). It came from the Judge app being the one writer (ADR-0004, ADR-0005). ADR-0013 removed that need, and what was left was a rule of accountability that costs a hand-over, locks an Event out when its Main Operator is lost (a fix only the database could make), and treats a Tenant Root worse than the Developer. Who gave access and who recorded a time are logged, which answers who ran an Event without a single holder.

**Levels held once per account.** An account that is a Tenant Root in one Tenant would be Root everywhere or nowhere, and authority never crosses a Tenant.

**Authorization in the handlers** (the first form of this decision). Each endpoint read its body, looked the user up, found the Event, asked the policy and checked the shape in an order of its own, so the same chain stood in dozens of places and the answers differed. A declared requirement and one enforcement replace it.

**A framework authorization policy and handler, or a middleware.** They run before the body is bound, so a place named in a body needs a second read of it; they cannot validate, and they cannot hand the Event they found to the handler without a side channel. An endpoint filter runs after the body is bound once, and holds access, place and validation in one ordered pipeline.

**An account listing for granting.** Leaks every account to every Tenant; exact email and a guarded name search replace it. The owner chose a name search over email only, with the guardrails above.

## Consequences

The name search can show display names across Tenants when the caller asks for it, and is accepted with its guardrails. Root can change a Live Event, and can always assign a Console Operator to it; who ran it rests on the log (who gave access, who recorded a time) and not on a single holder. Several people may edit what an Event keeps at once: Participations are guarded by their version (ADR-0013), while Rankings, Officials and Handouts are last-write-wins and not announced, so two editors of those can overwrite each other until they too count writes or are announced. ADR-0002's tenant paragraph is amended (tenant-scoped authorization is now in scope, memberships carry the roles), and ADR-0010 now reads "registered once across Tenants". The `send-group` route of Snapshots takes one Event, so that access is decided once, before the handler. Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).

## Readings made when it was built (#643)

The matrix is the specification and one pure function evaluates it (`AccessPolicy`), checked row by row from the table above. Where the table is short of a sentence, the code takes the stricter reading, and these are the readings:

- **Stages.** An Event is not started, Live until the end of its last day, then Historic (ADR-0007). Root assigns Console Operators until the Event ends and deletes an Event only before it starts; a Historic Event is read-only and nobody assigns, links, brands, resets or deletes it.
- **Configuring an Event is a Console Operator's and Root's.** Configuring an Event, starting it, linking Operators and Officials and branding it are open to a Console Operator of the Event and to Root, before the Event starts and while it is Live. Assigning a Console Operator is Root's alone, as is deleting an Event before it starts; a Console Operator does neither.
- **Root's every right includes a Live Event**, sending a Snapshot among them. The Developer and a Tenant Root of the Tenant record, edit and reset on a Live Event as a Console Operator does, and are named by their account as anybody is.
- **An Event is made in the Tenant the caller acts in** (the selected Tenant, the home Tenant by default), which has to be operational even for the Developer. Nothing sent names a Tenant or a Console Operator.
- **A Console Operator is named like any grant**, by an exact email or by an account found through the guarded name search, and the answer says whether it is pending, which Root and a Console Operator are trusted with.
- **The searches.** The account search is open to Root and to a Console Operator of an Event of the Tenant that is not yet Historic (the same people as the registry edit); its named view across Tenants is for the same people in any Tenant, a Tenant Root of any Tenant counting among them. The registries of Athletes, Horses, Clubs and Officials are searched across Tenants by any signed-in account, with the same limits and the same budget. What an account that sets nothing up reads of an Athlete or a Horse is its name and its name in English, and only those are matched: the owner's rule of 2026-10-23 is that a signed-in account reads no more of a person than the name (no FEI ID, no email, nothing that the profile may hold later). The whole row is for the people who set an Event up, who need the FEI ID, the club and the country of another Tenant's rider for a Combination and for the FEI export, and they are the people who search the accounts across Tenants.
- **Invitations are taken at the sign-in with a code.** A pending grant attaches to the account that proves its email, which registering does too, and attaching is never a reason for a sign-in to fail: when the grants cannot be written it is logged and tried again at the next sign-in. An Event holds at most 500 grants, a guard against a flood and not a quota.
- **What changes who may do what is logged.** An Event made, a grant given or removed (a Console Operator's included), the rules of a Tenant edited and the invitations taken are events 1101 and 1104 to 1108, each with the id of the user who did it and the ids of what it was done to. 1102 and 1103, the assignment and the hand-over of a Main Operator, are retired and not reused. None carries an email, a name or anything else an account keeps.

## Readings made when the reference data moved (#603)

The registries, the countries and the Setup of an Event became resources of the Api (ADR-0008). The matrix is short of a sentence in five places, and these are the readings; the first three are rows of the policy (`EditSetup`, `ReadConfiguration`, `EditCountries`), checked in the table of its unit tests.

- **The Setup is frozen at the start.** A Console Operator and Root configure an Event until it starts, and once it has the Console works on the copies the Event made of it (ADR-0006), so nobody changes its Setup, not even while it is Live, which is why this is narrower than "configure an Event", which they do while it is Live too. A change of a started Setup is 409 `event-started` or `event-ended`.
- **Who reads a Setup.** A Console Operator of the Event and Root, at every stage; a list of Setups is the Tenant's for Root, and for a Console Operator it is those of the Events it runs now. An Official or an Operator does not read the Setup: what they see of an Event is the public views and what the capabilities give them.
- **The countries are the Developer's.** They are the platform's reference data, which belongs to no Tenant and is read by every account; nobody removes one.
- **Any signed-in account reads the registry of its Tenant.** The matrix says who edits it and that any signed-in account searches it across Tenants, so a row is read by its id by anybody signed in, as the rows found by the search are; "Public reads are an allowlist" keeps an anonymous caller out of all of it.
- **The link of an Athlete to an account is the server's.** It holds an email, so no route shows it, takes it or lets it be filtered on, and an edit of the Athlete leaves it as it was. It goes with the Athletes' own rework (ADR-0010).
- **A client that makes its ids may name them.** A create takes the id of the document, and the same id again is the row that was made, which is how an idempotent create works for anything a client makes the id of; another Tenant's row with the id is not told about.
- **Deleting an Event takes its grants** with it, so that none is left waiting for an Event that is not there.
- **The legacy routes stay until the Functions API is retired (#647).** The Functions project still answers the old routes of the families that moved, open as they were; the clients of the platform no longer call them.

## Readings made when what an Event keeps moved (#604)

What an Event makes when it starts and works on while it is Live, its Participations, Rankings, Officials and Handouts, became resources of the Api (ADR-0008, ADR-0006). The matrix says "run the Event" and says nothing of the stage, and these are the readings; the first is a row of the policy (`RunEvent`, checked in the table of its unit tests).

- **A Console Operator of a Live Event, and Root, write them.** Only a Live Event has them to change: an Event that has not started is a Setup, which has none (409 `event-not-started`), and one that has ended is read-only (409 `event-ended`). Anybody else is refused (403 `not-console-operator`). Making, changing and removing are the one row.
- **Anybody reads them (ADR-0001), by the Event.** A list names the Event at the head of its filter, because the Event says which Tenant it is read in: the Tenant of a row is the Tenant of its Event, told by the Event and never by the caller, and no parameter takes one. A row is read by its id whatever the Tenant, as the public views show it.
- **A row keeps its Event.** The Event is named when a row is made, and a change that names it, the Tenant or the version is refused. A row made with an id that is a row of another Event, or of another Tenant, is 409 `id-taken`, and is not told about.
- **The account an Official is linked to is the grants' business.** It holds an email, so no route shows it, takes it or lets it be filtered on, and a change of the Official leaves it as it was; whom an Official or an Operator is linked to is told by the grants of the Event (`ConfigureEvent`).
- **A Participation that is named is not removed.** A Ranking holds the ids of the Participations it counts and a Handout the id of its Participation (ADR-0006), and the Results of a Ranking cannot be composed without one of them, so removing a Participation that either still names is 409 `participation-in-use`, as ADR-0010 has it that what is referred to cannot be deleted. Whether the Console takes the entries out and removes the Handouts first, or the server does it for it, is the owner's to say when the Console is built (#646).
- **Operators are not a resource of their own.** They are grants, and so is a Console Operator, whose grants are the resource `console-operators`. What a person may do about an Event is told by its capabilities, and the Ui asks them (`canSnapshot`) instead of working it out from lists of Officials and Operators, which the Api does not give away.

## Readings made when the migration was built (#607)

"The data moves once" is short of a sentence in a few places. The commands and what they print are in `tools/NTS.Tools/README.md`, and these are the readings.

- **A database says what it is.** Production, staging and a developer's own database hold the same collections, so one document, `environment/environment`, says which. `migrate-tenants --apply` needs `--environment` and writes it, `seed-staging` and the local sign in as (ADR-0002) work only on a database that says it is Staging or Development, and refuse one that says Production, one that says nothing and one whose marker says anything else (a misspelt marker is not read as "not production"), and a marker is never changed to another name.
- **Existing accounts keep their address unconfirmed.** ADR-0002 has every person re-enrol: they sign in with a code sent to the address, which is what proves it, and add a passkey after. The migration completes an account with the fields Identity needs and confirms nothing.
- **Nobody is assigned.** The migration assigns no Console Operator: Root runs an Event until it assigns one, so no Event waits for a Tenant Root to be seeded. A Historic Event gets none, and the Officials and Operators it linked become no grants: nobody records on a Historic Event, so a grant on it would give nothing.
- **A person's state is re-keyed only where its owner is an email.** What the Functions API stored as the owner of a state (`event_user_sessions`) is the identifier of the sign-in of its day. An email that an account has is turned into the id of the account, and any other, such as the object id of an Entra account, is counted and left, because nothing maps it.
- **What waits for the ids.** A document whose id is not a standard UUID (an account, an Event or its Setup) is counted and left as it is until the ids are converted (ADR-0009, #637), so the command is run again after them and every step is the same on the second run.
- **The indexes are made first.** The unique ones of the hosts (the email of an account, a code, a grant) can fail on data, and failing in the command with its report beside it is better than a host that does not start. The tools are .NET 8 and cannot reference the identity library, so the definitions are replicated, and a test runs the makers of both hosts over a database the command made, so they cannot drift apart unnoticed.
- **The seed is invented data.** `seed-staging` makes a small Live Event of invented people and horses, with the documents the Api makes when it starts an Event (made in one place, `EventStartDocuments`), because the data of the golden Event is in the shape of before the migrations. A person who tries the platform on a staging database is a Tenant Root, a Console Operator, an Official or an Operator of it.

## Readings made when branding was built (#646)

The logos of a Tenant and of an Event are in `/api/tenants/{id}/branding` and `/api/events/{id}/branding` (`.claude/skills/rest-api/SKILL.md`), and these are the readings.

- **The branding is the Tenant's, in a collection of its own.** `brandings` holds one document for the Tenant and one for each Event that has logos of its own, each with at most a logo for the left and one for the right of a header. It is a collection a Tenant owns, so it is read and written through the Tenant's collection like the others and has the isolation tests of all of them, and the logos are read only when a document is printed, so that they weigh nothing on any other read: they are not in the document of the Tenant or of the Event.
- **An Event is printed with its own logo for a position, and the Tenant's where it has none.** The Console asks the Event for what it is printed with, and the Api says which of the two each position is. Taking a logo away from an Event gives back the Tenant's, and a reset does not take it away.
- **Who may.** Root edits the Tenant's logos and is the only one who reads them there (the Event's answer already has them for everyone who prints). A Console Operator of the Event, or Root, edits the Event's until its last day ends; anybody who is signed in reads what an Event is printed with, as the Results it is printed on are public. Reads are not on the public list: the viewers do not show logos yet, and putting a route on that list is a decision of its own.
- **What a logo is.** An image of a kind a browser prints (PNG, JPEG, GIF, WebP), of at most 512 KiB, by what it begins with and not by what it says it is. SVG is not taken: it can carry a script, and it is shown by every person who prints. The limit is the Api's, so the Console tells a person the same and the same applies to a program that does not go through the Console.
- **A logo is sent as base64 in the document of its resource.** The Api takes no body that is not a document of JSON:API, and the logo is of the resource it is a member of, so there is no route for an upload. The cost is a third in size, which with the limit of a document (1 MiB) is why two logos of the largest size are two requests.
- **Printed with what is read, by value.** The Console puts the image into the page it prints as a `data:` URL, so a printed page needs nothing of the host, and a person who is signed out between the Console and the printer still gets the logos.

## Readings made when the timeline was built (#619)

- **"See how times were recorded" is asked on the read of a Participation, and the names of the people on a route of its own.** The Api reads the grants of the caller for the Event of a Participation it serves, and tells its time events to the callers the policy's `SeeTimeEvents` allows: Staff and Root, at every stage of the Event. For anybody else the same Participation has the times of its Phases and no events. The names of the accounts that recorded a time are told to the same people and to nobody else, and only for an Event they may see the events of, so the route is not a way to learn the name of an account.
