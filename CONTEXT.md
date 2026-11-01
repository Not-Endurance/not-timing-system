## Overview
This project employs a Domain Driven Design architecture with strict layer and boundary separations. The codebase consists of two parts:
- Not* "nugets", located in /nugets
- NTS* - stands for No Timing System - the name of the product. 

The NTS projects follow the layering of the Not projects, except that the application and storage layers live inside the two hosts, `NoTiming.Api` and `NoTiming.Ui` (ADR-0011). 

## Language

### People

**Athlete**:
The human rider, recognised as the same person across Events. One half of a Combination; a Participation shows the Athlete as entered at that Event. Two copies are one Athlete when they have the same FEI ID, or the same name once trimmed and case-folded, in either script (the Bulgarian name and its Latin form by the official transliteration are the same name), or the same legacy id other than 0; a spelling variant or a near-miss of a name is for a person to decide, a conflict (equal names under different FEI IDs) is held, and the Country and the Club never decide. An Athlete that a Participation refers to is not deleted.
_Avoid_: Rider, competitor

**Horse**:
The equine half of a Combination, recognised as the same animal across Events. A Participation shows the Horse as entered at that Event. The rules that make two copies one are the Athlete's, with the FEI ID taken as the text it is; a Horse that a Participation refers to is not deleted.

**Participant**:
An Athlete or a Horse: the umbrella term for the two kinds of row of the registry (ADR-0010). A Participant is recognised across Events and Tenants, is found by name (in the script it is registered in, and in English where the row has its name in English; a signed-in account reads no more of a Participant than that name, and the FEI ID, the club and the country are for the people who set an Event up), and has a history: the Participations it was in, newest first, each with its placing. It is one half of a Combination, never the Combination itself, and never a ride.

**Combination**:
An Athlete and a Horse entered together as a single competitor, identified by a start number. This is the pair that competes — it is *not* a person, and it is *not* a Participant: each of its halves is one.
_Avoid_: pair, entry, competitor

**Official**:
Certified personnel appointed to an event: Ground Jury, Veterinary Commission, Stewards, Technical Delegate, Foreign Judge. Officials are named in produced documents such as Results and FEI exports.
_Avoid_: Judge, referee, personnel

**Operator**:
A person who works an Event with Snapshot access, like an Official. An Operator is *never* named in Results, FEI exports, or any other produced document; they exist for the system, not for the record. An Operator is not a Developer.
_Avoid_: Admin, superuser, sysadmin

**Console Operator**:
An Operator who also works the Console of one Event: configures and starts it, edits what it keeps, links its Staff, brands it and resets it, and records Snapshots. Root assigns a Console Operator to an Event, as a Grant, and an Event has as many as Root assigns, none included: Root runs an Event that has none. Several may work the Console at once, because the Console is one more viewer of what the server records.
_Avoid_: Main Operator (the retired name, from when an Event had exactly one), admin

**Staff**:
Operators (Console Operators among them) and Officials together — the people who work an Event and may see how its times were recorded: its time events, who recorded them and when. Root sees them too. Everyone else, signed in or not, sees only the recorded times.

### Tenancy

**Account**:
A person's sign-in identity, one per person across all Tenants. What an Account may do is decided by the roles and grants it holds, not by the Account itself.

**Tenant**:
A country's equestrian federation (for example the Bulgarian Federation of Equestrian Sports) — the organiser that owns Events and the Setup data it prepares. A Tenant comes into being when the first person from its country registers, and becomes operational — able to hold Events — once the Developer gives it a Tenant Root. It sets the rules its Regional competitions use, such as how loop speed is judged; an Event keeps the rules as they were when it started. A Tenant organises data rather than walling it off: public views of Events and, where allowed, searches for Athletes, Horses, Clubs, Officials and accounts reach across Tenants. Authority never crosses: no access one Tenant grants carries into another Tenant's Events.
_Avoid_: Organisation, account, customer

**Home Tenant**:
The Tenant an Account is placed in from its country: at registration, from the country the person chose, and for an Account from before Tenants, from the country its profile names, the first time it signs in. It is set once and profile edits never move it. An Account whose profile names no country that has an ISO code has none until the person picks one.
_Avoid_: Primary tenant, default tenant

**Membership**:
An Account's standing in one Tenant: the Tenant and the roles held there. Registering gives one Membership, in the Home Tenant, with no role; being made a Tenant Root adds that role to a Membership. An Account has as many Memberships as Tenants it takes part in, and what it may do in a Tenant comes from the roles of its Membership there.
_Avoid_: Tenant role, affiliation

**Tenant Root**:
A role within one Tenant, held by one or more accounts and created by the Developer — a Tenant Root cannot add another. A Tenant Root holds the Root Level in its own Tenant: it creates the Tenant's Events and may run any of them, before they start and while they are Live, or assign Console Operators to run them.
_Avoid_: Admin, owner

**Developer**:
A platform-level role reserved for the platform owner, with the Root Level in every Tenant and the platform's own reference data, such as the countries, to itself. It is not granted through a Tenant, and only by command: no route gives it, or a Tenant Root.
_Avoid_: Admin, superuser

**Root**:
The highest Level, held by the Developer and, within its own Tenant, by a Tenant Root: every right over the Events and the registry of the Tenants in reach, at every stage the action allows, a Live Event included. Root assigns Console Operators to Events and does not need to be one to run an Event.

**Level**:
How far a caller reaches at one place — an Event, a Tenant, or the platform — from the lowest: Anonymous (not signed in), Account (signed in, holding nothing there), Staff, Console Operator, Root. Each Level holds everything the ones below it do. An Account stands at a Level per place, not once: a Tenant Root in one Tenant can be an Operator of an Event in another and a Console Operator of a third. The stage of an Event then decides which rights of a Level are open.
_Avoid_: User (for the Account that holds nothing), permission

**Grant**:
The access given to a person for one Event: as a Console Operator, as an Operator, or as an Official of a role. Root gives a Console Operator grant; a Console Operator gives the Operator and Official ones. A grant names the person by their exact email. When the email has an Account the grant is that Account's at once; when it has none it is a pending invitation, which gives nothing until the person registers with that email and then attaches to the new Account. A grant belongs to the Tenant of its Event and ends the moment it is removed.
_Avoid_: Permission, role assignment, invite

**Regional rules**:
How a Tenant's Regional competitions are judged: whether the speed of a loop is judged on the average only, and which ranker applies. A Tenant Root sets them on the Tenant, and an Event copies them when it starts, so what an Event was judged by never changes when the Tenant edits them. An Event that started without any is judged by the FEI's rules.

**Capabilities**:
What the caller may do about an Event, or in a Tenant, told by the same access policy that refuses the action. A screen shows controls from these and never works them out for itself.

### Data

**Setup**:
The reference data and Event configuration prepared before an Event, such as its Athletes, Horses and Clubs. An Athlete or Horse is registered in Setup once. The Setup of an Event is changed by Root and its Console Operators until the Event starts and by nobody from then on, because the Console works on the copies Core made of it; the registries and the countries it is made from go on changing.

**Core**:
What an Event produces while it runs and afterwards: its Participations, Rankings, Handouts and Officials. Core holds copies of Setup data as entered when the Event started, so a later Setup change does not rewrite a finished Event.

**Environment marker**:
The one document of a database that says which environment it is, Production, Staging or Development, which nothing else in it does: they hold the same collections. The commands that prepare a database write it and it is never changed to another name. What must not touch a production database, the staging seed and the local sign in as, works only on one that says it is Staging or Development, and refuses one that says Production, one that says nothing and one that says anything else.
_Avoid_: Environment flag, database tag

### Competing

**Event**:
An endurance show held at one location over a span of days. Every Participation, Official and Snapshot is scoped to one Event.
_Avoid_: Competition (that is one ride within an Event)

**Live Event**:
An Event that is still running: its Console and its viewers see changes as they are recorded, and Staff may record and change times. It becomes a Historic Event at the end of its last day, and from then on changes to it are refused. Nobody is told when it ends: whoever has it open learns it from the first change that is refused, and the page they are on becomes the record of the Event where it is.
_Avoid_: Active event, current event

**Historic Event**:
An Event that has ended, viewed as a record: nothing about it can be changed, and its Results and other produced documents can still be printed. It has no Startlist, which is about who starts next, and no Snapshot to capture. Opening one never affects a Live Event; the connection to an Event that has ended is closed when the page that shows it learns that it has.
_Avoid_: Past event, archived event

**Console**:
The workspace of Root and the Console Operators for configuring and running an Event: Setup, the live dashboard, startlist, handouts and rankings. Each reaches it from as many tabs or devices as they like, and several at once: the Console is one more viewer of what the server records.
_Avoid_: Judge, Judge app

**Participation**:
One Combination's ride at an Event — its category, phases, and outcome: the act of Participants competing, recorded. A record of competing, not a competitor and not a person. The same ride can count in the Rankings of more than one Competition.
_Avoid_: entry, run

> **A Participant is an Athlete or a Horse, not the pair and not the ride.** The competing pair is a **Combination**; its ride is a **Participation**. A thing named `Participant` that means the pair, the ride or a signed-in account is a misnomer to be corrected: `WitnessAccessLevel.Participant` was one such misnomer and became `Registered` — "signed in without a write role" (see #592); the Ui has no such level any more (#671), and is told by the Api whether the person may send a Snapshot.

**Ranking**:
The Participations that compete for placement in one Competition and Category, each marked ranked or not ranked. While its Event is Live it holds no placings and Results derive them; once the Event is Historic it keeps its final placings.

**Results**:
The placings of a Ranking. While the Event is Live they are derived from its Participations' latest times whenever they are needed, a produced document; for a Historic Event they are the Ranking's final placings, as they were printed.

**Handout**:
The printable sheet of one Combination's times, produced for it after each Phase. It is a live view of the Participation, not a snapshot: it prints the times as they stand then.

**Logos**:
The two images at the head of every document an Event prints, one at the left and one at the right. Root keeps the Tenant's own, and a Console Operator of an Event, or Root, may put the Event's own in place of either until the Event ends; a place the Event has none for is printed with the Tenant's. An image is a PNG, JPEG, GIF or WebP of at most 512 KiB, kept by the server, so that every browser that prints the Event's documents prints the same.
_Avoid_: Branding (the name of the Api's resource, not of the thing), letterhead

> **Write access is not "Official".** Snapshot writes are granted to an Official whose role is one of {Steward, ChiefSteward, GroundJury, GroundJuryPresident}, **or** to any Operator, a Console Operator among them, **or** to Root. "Official" alone is not the boundary.

### Timing

**Phase**:
One leg of a Participation: the loop ridden from Start to Arrival, then the vet gate where the Combination is Presented (again, if re-inspection is requested), then a Rest that ends at the Out time, which is the next Phase's Start. A Snapshot is placed in a Phase by its own time, and a Representation or Inspection request by the time it is made, with one comparison: at or after the next Phase's Start it belongs to the next Phase and that Phase becomes the current one, otherwise it belongs to the current Phase. The time of a request is the clock of the server, which should agree with the clocks of the devices to within about a minute around the Out time; times are compared by the time of the day, so a ride that crosses midnight is not supported.

**Presentation**:
A Combination presenting at the vet gate, captured as a time. Presenting again after re-inspection is requested is a Represent: the same kind of time, marked as a re-presentation.

**Snapshot**:
A time an Official captures for a Combination, by start number, and sends in for the Event as an Arrival or a Presentation. It is an input, not part of the Participation: the server records every Snapshot as a time event on a Phase, accepted or rejected, and answers with it. The device makes its id, which is the id of the event, so that a Snapshot sent again is the same Snapshot and is recorded once.

**Time event**:
A past-tense fact recorded on a Phase — Arrived or Presented, an Update of one, or an edit of the Phase form in the Console (a PhaseUpdated, which lists the several times it changed) — with an outcome: accepted, or rejected with a reason (including a manual reject made in the Console). A Phase's times are the latest accepted time event that lists each of them (a time that an edit cleared is listed with none); rejected ones stay in its history. A time that breaks the order Start, Arrival, Presentation, Representation, compared among the times that exist, is rejected as an invalid time and never thrown at; a Presentation without an Arrival is legal, because a delayed Arrival looks exactly like that.
_Avoid_: Event (that is the show), phase event, snapshot event

**Update**:
Correcting times already recorded on a Phase — an Official changing a sent Snapshot's time, named by the Snapshot's id (an ArriveUpdated or a PresentUpdated), or someone editing the Phase in the Console (a PhaseUpdated, which lists only the times the Save changed, a cleared one as listed with none, and edits Start in place). It is its own kind of time event, not a repeat of Arrived or Presented, because it records a different intent; the earlier time stays in the history. It sets an absolute value, so the last one wins. Name the source when writing about it: a PhaseUpdated is the Phase form, a PresentUpdated is an Official's.

**Disable**:
Negating a time event in the Console: its outcome becomes a manual reject, so the previous accepted time of that kind takes over again, and it keeps when it was disabled. The event stays in the history and can be enabled again. Only an accepted event is disabled and only a disabled one is enabled: what the rules rejected stays as it is. The choices are made in the timeline of the Console and saved as one change.

**Timeline**:
The Console's dialog for one Participation: every time event of every Phase, in the order they were recorded, with what became of it, how it was acquired, who recorded it and when. Whoever works the Console chooses in it the events to disable and to enable, and saves the choice as one change; only Staff may read how times were recorded.

**Change notification**:
The signal sent to everyone viewing an Event when a Participation has been changed and saved. It names the Participation, never describes it: receivers read the Participation again.
_Avoid_: Event (that is the show), Update (that is a correction of times)

### Testing

**Unit test**:
A test of domain logic, and of application logic where it is judged necessary, that needs no host, database, network, browser or real clock.
_Avoid_: Fast test, mock test

**Integration test**:
A test of a whole feature through its real entry points (HTTP, the live connection, the browser) against a real database. It doubles only what lies at the border of the system.
_Avoid_: End-to-end test

**Feature**:
One capability of the product, and the folder that holds its integration tests.
_Avoid_: Feature slice

**Journey**:
The path a feature takes from its entry point to its outcome, written as numbered stages. A feature has one or more journeys.
_Avoid_: Flow, workflow

**Stage**:
One numbered, named step of a journey, such as "3 Enter code". A failure names the stage it stopped at.
_Avoid_: Step

**Scenario**:
One case of how a journey goes: every stage succeeds, or it stops at one stage, or it runs under different rules, such as the FEI or the Regional ruleset. A journey has one or more scenarios.
_Avoid_: Case, example

**Border**:
The edge of the system an integration test runs. What the product does not own, such as the clock and outbound email, is replaced by a double there; everything inside it is real.

**Harness**:
Code that tests use but that is not itself a test: fixtures, drivers, seeds, fakes and the rules that architecture tests check. It is kept apart from the tests that use it.
_Avoid_: Helpers, test support

## Not* projects
They are separated by function - Blazor, Storage, Application. etcs. *Not* is shared amongs them. These are intended to packaged up and used in other projects to bootsrap functionality, ensure consistent behavior and allow for easier maintenance. Elements of Not should be completely stripped of business logic and should provide a streamlined, generic API striving for a ballance between strict, conssitent behavior and enough configurability to be multi-purposed.
Notes:
- Notable exception is `Not.Krud` - as of now this is a standalone framework designed to streamline CRUD operations of complex Domain aggregates. It allows each aggregate entity to be editable individualy via simple `IRepository<T>` interface without it's Aggregate root yielding the final control.
- Service registration - services in Not* should be registered manually, using the `N*Builder` pattern (see `NApplicationBuilder`) for example
- Components defined in `Not.Blazor` should begin with `N` - `NTable`, `NNotifier`, `NTextField` etc

# NTS* projects
NTS stands for *No Timing System*, the Brand of the current product - a cheap alternative to full-fledged Endurance timing systems.
- Domain Model - hardcode business logic and validation is contained within the `NTS.Domain.*` namespace, which holds the domain model. It has to be as pure as reasonably possible. Some "pollution" is acceptable - like coupling with `MediatR.INotification` or `Not.Krud` framework, however there are no references to repositories or other infrastructure.
- Projects - `NTS` (localization), `NTS.Domain` (shared, with Tenant and the Account), `NTS.Domain.Setup`, `NTS.Domain.Core`, `NTS.Contracts`, the two hosts `NoTiming.Api` (the server) and `NoTiming.Ui` (the app), and the Razor libraries of the Ui, `NoTiming.Console` (the pages of the Console, fetched when a signed-in person goes there) and `NTS.Blazor` (what the Console and the viewers share). The Api references the Ui to serve it, the Ui never references the Api, and the domain references no infrastructure; the architecture tests read the project files and say so. Nothing else is deployed: the Functions API, Warp and the Static Web Apps are gone (ADR-0011).
- Secondary business logic - the so-called application level, which lives in the hosts: server write-side services and access policies in `NoTiming.Api`, one folder per feature, and client view services in `NoTiming.Ui`. It handles interactions between domain boundaries and infrastructure. The key component here and entry point should be the Service. Services should be suffixed with `*Service` and usually implement multiple segregated interfaces (conforms to Interface segregation principle)
- Storage - contains operations related to storing something: the Mongo wiring in the Api and the REST repositories in the Ui. All of the storage should be conform to the `IRepository<T>` abstraction.
- Blazor - the Ui is one Blazor WebAssembly app. The layout is composed of a Header, Drawer and Main content. Components that are navigated to via Router should be suffixed with `*Content.razor`.
- Service registration - services in NTS projects should be registed to the container via the `ITransient`, `IScoped` and `ISingleton` marker interfaces. Those interfaces shouldn't be inherited by other interfaces, but implemented by the classes implementing those interfaces. Services should not be registered manually

## Blazor
Component rules
- Components should inherit from `NComponent` or one of it's derivatives almost always. Exceptions can be made for example if exteding an external component such as `ErrorBoundary` or `MudTextField`. 
- Component `.razor` files should not contain code-behind, instead they should *inherit* from a `Behind.cs` class of the same name - i.e `Component.razor` inherits `ComponentBehind.cs`. Components shouldn't use `partial` code behinds as this inheritance offers a better control on what to expose to the razor file. 
- Component behinds should never expose services directly, as exception in service execution leads to poor Blazor experience - `ErrorBoundaries` cannot handle some async exceptions gracefully in my experience. Instead `NComponent` defines a `Handle(Exception ex)` method which is used to that end. 
- Component behinds should expose only properties and not fields 
- Component behinds should expose only *Safe* methods - a method is safe if it's wrapped with a `try-catch` that users `NComponentHandle` or if its name is suffixed with `Safe`. The latter should only be used if the method is passed to a Safe component parameter (also suffixed by safe).
- Base classes for internal use should be suffixed with `Base` - example `Event : EventBase`. Base classes that are intended to be implemented by the user don't have to follow that rule - example `KrudShell`
- Base classes and interfaces should be in `Abstractions`zw
