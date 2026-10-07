## Overview
This project employs a Domain Driven Design architecture with strict layer and boundary separations. The codebase consists of two parts:
- Not* "nugets", located in /nugets
- NTS* - stands for No Timing System - the name of the product. 

The NTS projects follow the layering of the Not projects, except that the application and storage layers live inside the two hosts, `NoTiming.Api` and `NoTiming.Ui` (ADR-0011). 

## Language

### People

**Athlete**:
The human rider, recognised as the same person across Events. One half of a Combination; a Participation shows the Athlete as entered at that Event.
_Avoid_: Rider, competitor

**Horse**:
The equine half of a Combination, recognised as the same animal across Events. A Participation shows the Horse as entered at that Event.

**Combination**:
An Athlete and a Horse entered together as a single competitor, identified by a start number. This is the pair that competes — it is *not* a person, and it is *not* called a Participant.
_Avoid_: Participant, pair, entry, competitor

**Official**:
Certified personnel appointed to an event: Ground Jury, Veterinary Commission, Stewards, Technical Delegate, Foreign Judge. Officials are named in produced documents such as Results and FEI exports.
_Avoid_: Judge, referee, personnel

**Operator**:
A person who works an Event with Snapshot access, like an Official, and is expected to exceed Official access over time. An Operator is *never* named in Results, FEI exports, or any other produced document; they exist for the system, not for the record. An Operator is not a Developer.
_Avoid_: Admin, superuser, sysadmin

**Main Operator**:
The one Operator of an Event who runs it from the Console: the highest authority in the Event's scope, with full access, writes included. An Event has exactly one at a time. It is the Tenant Root who created the Event until that Tenant Root assigns it to someone else, which is only possible while the Event is not yet Live; once the Event is Live only the Main Operator can hand it over, to a named account. The Main Operator links accounts to the Event's Officials and Operators.

**Staff**:
Operators and Officials together — the people who work an Event and may see how its times were recorded. Everyone else, signed in or not, sees only the recorded times.

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
A role within one Tenant, held by one or more accounts and created by the Developer — a Tenant Root cannot add another. A Tenant Root creates the Tenant's Events and is each one's Main Operator until it assigns that role to someone else, usually so the configuration and running of that single Event is done by another person. Once an Event is Live, a Tenant Root who is not its Main Operator cannot take the role over, and the Tenant Root role itself gives no right to run a Live Event.
_Avoid_: Admin, owner

**Developer**:
A platform-level role reserved for the platform owner, with full rights across every Tenant, except acting as the Main Operator of a Live Event: what the Main Operator does there is the Main Operator's alone, and a Live Event whose Main Operator is locked out is a database fix, not an action of the product. It is not granted through a Tenant, and only by command: no route gives it, or a Tenant Root.
_Avoid_: Admin, superuser

**Grant**:
The access the Main Operator gives to a person for one Event: as an Operator, or as an Official of a role. A grant names the person by their exact email. When the email has an Account the grant is that Account's at once; when it has none it is a pending invitation, which gives nothing until the person registers with that email and then attaches to the new Account. A grant belongs to the Tenant of its Event and ends the moment it is removed. The Main Operator is not a grant: the Event names it.
_Avoid_: Permission, role assignment, invite

**Regional rules**:
How a Tenant's Regional competitions are judged: whether the speed of a loop is judged on the average only, and which ranker applies. A Tenant Root sets them on the Tenant, and an Event copies them when it starts, so what an Event was judged by never changes when the Tenant edits them. An Event that started without any is judged by the FEI's rules.

**Capabilities**:
What the caller may do about an Event, or in a Tenant, told by the same access policy that refuses the action: `canSnapshot`, `isMainOperator`, `canHandOver`, `canAssignMainOperator`, `canCreateEvents` for an Event. A screen shows controls from these and never works them out for itself.

### Data

**Setup**:
The reference data and Event configuration prepared before an Event, such as its Athletes, Horses and Clubs. An Athlete or Horse is registered in Setup once. The Setup of an Event is changed by its Main Operator until the Event starts and by nobody from then on, because the Console works on the copies Core made of it; the registries and the countries it is made from go on changing.

**Core**:
What an Event produces while it runs and afterwards: its Participations, Rankings, Handouts and Officials. Core holds copies of Setup data as entered when the Event started, so a later Setup change does not rewrite a finished Event.

### Competing

**Event**:
An endurance show held at one location over a span of days. Every Participation, Official and Snapshot is scoped to one Event.
_Avoid_: Competition (that is one ride within an Event)

**Live Event**:
An Event that is still running: its Console and its viewers see changes as they are recorded, and Staff may record and change times. It becomes a Historic Event at the end of its last day, and from then on changes to it are refused.
_Avoid_: Active event, current event

**Historic Event**:
An Event that has ended, viewed as a record: nothing about it can be changed, and its Results and other produced documents can still be printed. It has no Startlist, which is about who starts next. Opening one never affects a Live Event.
_Avoid_: Past event, archived event

**Console**:
The Operator's workspace for configuring and running an Event: Setup, the live dashboard, startlist, handouts and rankings. Only the Main Operator reaches it, from as many tabs or devices as they like: the Console is one more viewer of what the server records.
_Avoid_: Judge, Judge app

**Participation**:
One Combination's ride at an Event — its category, phases, and outcome. A record of competing, not a competitor and not a person. The same ride can count in the Rankings of more than one Competition.
_Avoid_: Participant, entry, run

> **"Participant" is not a term in this domain.** The competing pair is a **Combination**; its ride is a **Participation**. Anything named `Participant` is a misnomer to be corrected. `WitnessAccessLevel.Participant` was one such misnomer and is now `Registered` — "signed in without a write role" (see #592).

**Ranking**:
The Participations that compete for placement in one Competition and Category, each marked ranked or not ranked. While its Event is Live it holds no placings and Results derive them; once the Event is Historic it keeps its final placings.

**Results**:
The placings of a Ranking. While the Event is Live they are derived from its Participations' latest times whenever they are needed, a produced document; for a Historic Event they are the Ranking's final placings, as they were printed.

**Handout**:
The printable sheet of one Combination's times, produced for it after each Phase. It is a live view of the Participation, not a snapshot: it prints the times as they stand then.

> **Write access is not "Official".** Snapshot writes are granted to an Official whose role is one of {Steward, ChiefSteward, GroundJury, GroundJuryPresident}, **or** to any Operator. "Official" alone is not the boundary.

### Timing

**Phase**:
One leg of a Participation: the loop ridden from Start to Arrival, then the vet gate where the Combination is Presented (again, if re-inspection is requested), then a Rest that ends at the Out time, which is the next Phase's Start. A Snapshot is placed in a Phase by its own time, and a Representation or Inspection request by the time it is made, with one comparison: at or after the next Phase's Start it belongs to the next Phase and that Phase becomes the current one, otherwise it belongs to the current Phase. The time of a request is the clock of the server, which should agree with the clocks of the devices to within about a minute around the Out time; times are compared by the time of the day, so a ride that crosses midnight is not supported.

**Presentation**:
A Combination presenting at the vet gate, captured as a time. Presenting again after re-inspection is requested is a Represent: the same kind of time, marked as a re-presentation.

**Snapshot**:
A time an Official captures for a Combination, by start number, and sends in for the Event as an Arrival or a Presentation. It is an input, not part of the Participation: the server records every Snapshot as a time event on a Phase, accepted or rejected, and answers with it. The device makes its id, which is the id of the event, so that a Snapshot sent again is the same Snapshot and is recorded once.

**Time event**:
A past-tense fact recorded on a Phase — Arrived or Presented, or an Update of one — with an outcome: accepted, or rejected with a reason (including a manual reject by the Main Operator). A Phase's times are the latest accepted time event of each kind; rejected ones stay in its history. A time that breaks the order Start, Arrival, Presentation, Representation, compared among the times that exist, is rejected as an invalid time and never thrown at; a Presentation without an Arrival is legal, because a delayed Arrival looks exactly like that.
_Avoid_: Event (that is the show), phase event, snapshot event

**Update**:
Correcting times already recorded on a Phase — an Official changing a sent Snapshot's time, named by the Snapshot's id, or the Main Operator editing the Phase in the Console. It is its own kind of time event, not a repeat of Arrived or Presented, because it records a different intent; the earlier time stays in the history. It sets an absolute value, so the last one wins.

**Disable**:
The Main Operator negating a time event: its outcome becomes a manual reject, so the previous accepted time of that kind takes over again. The event stays in the history and can be enabled again.

**Change notification**:
The signal sent to everyone viewing an Event when a Participation has been changed and saved. It names the Participation, never describes it: receivers read the Participation again.
_Avoid_: Event (that is the show), Update (that is a correction of times)

## Not* projects
They are separated by function - Blazor, Storage, Application. etcs. *Not* is shared amongs them. These are intended to packaged up and used in other projects to bootsrap functionality, ensure consistent behavior and allow for easier maintenance. Elements of Not should be completely stripped of business logic and should provide a streamlined, generic API striving for a ballance between strict, conssitent behavior and enough configurability to be multi-purposed.
Notes:
- Notable exception is `Not.Krud` - as of now this is a standalone framework designed to streamline CRUD operations of complex Domain aggregates. It allows each aggregate entity to be editable individualy via simple `IRepository<T>` interface without it's Aggregate root yielding the final control.
- Service registration - services in Not* should be registered manually, using the `N*Builder` pattern (see `NApplicationBuilder`) for example
- Components defined in `Not.Blazor` should begin with `N` - `NTable`, `NNotifier`, `NTextField` etc

# NTS* projects
NTS stands for *No Timing System*, the Brand of the current product - a cheap alternative to full-fledged Endurance timing systems.
- Domain Model - hardcode business logic and validation is contained within the `NTS.Domain.*` namespace, which holds the domain model. It has to be as pure as reasonably possible. Some "pollution" is acceptable - like coupling with `MediatR.INotification` or `Not.Krud` framework, however there are no references to repositories or other infrastructure.
- Projects - `NTS` (localization), `NTS.Domain` (shared, with Tenant and the Account), `NTS.Domain.Setup`, `NTS.Domain.Core`, `NTS.Contracts`, and the two hosts `NoTiming.Api` and `NoTiming.Ui`. The Api references the Ui to serve it, the Ui never references the Api, and the domain references no infrastructure.
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
