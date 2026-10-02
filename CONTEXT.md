## Overview
This project employs a Domain Driven Design architecture with strict layer and boundary separations. The codebase consists of two parts:
- Not* "nugets", located in /nugets
- NTS* - stands for No Timing System - the name of the product. 

You can see a clear corelation between Not and NTS projects and they are layered identically. 

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
The highest authority in the scope of an Event — full access, writes included, and expected to exceed Official access over time. An Operator is *never* named in Results, FEI exports, or any other produced document; they exist for the system, not for the record. Operator however is not a Platform admin or developer. The Operator running the Event's primary Judge app is the main Operator. 
_Avoid_: Admin, superuser, sysadmin

**Staff**:
Operators and Officials together — the people who work an Event and may see how its times were recorded. Everyone else, signed in or not, sees only the recorded times.

### Data

**Setup**:
The reference data and Event configuration prepared before an Event, such as its Athletes, Horses and Clubs. An Athlete or Horse is registered in Setup once.

**Core**:
What an Event produces while it runs and afterwards: its Participations, Rankings, Handouts and Officials. Core holds copies of Setup data as entered when the Event started, so a later Setup change does not rewrite a finished Event.

### Competing

**Event**:
An endurance show held at one location over a span of days. Every Participation, Official and Snapshot is scoped to one Event.
_Avoid_: Competition (that is one ride within an Event)

**Live Event**:
An Event that is still running: its apps stay connected to each other and Staff may record and change times. It becomes a Historic Event at the end of its last day, and from then on changes to it are refused.
_Avoid_: Active event, current event

**Historic Event**:
An Event that has ended, viewed as a record: nothing about it can be changed, and its Results, Startlist and other produced documents can still be printed. Opening one never affects a Live Event.
_Avoid_: Past event, archived event

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
One leg of a Participation: the loop ridden from Start to Arrival, then the vet gate where the Combination is Presented (again, if re-inspection is requested), then a Rest that ends at the Out time, which is the next Phase's Start.

**Presentation**:
A Combination presenting at the vet gate, captured as a time. Presenting again after re-inspection is requested is a Represent: the same kind of time, marked as a re-presentation.

**Snapshot**:
A time an Official captures for a Combination, by start number, and sends to the Judge app as an Arrival or a Presentation. It is an input, not part of the Participation: Judge records every Snapshot as a time event on a Phase, accepted or rejected.

**Time event**:
A past-tense fact recorded on a Phase — Arrived or Presented, or an Update of one — with an outcome: accepted, or rejected with a reason (including a manual reject by the main Operator). A Phase's times are the latest accepted time event of each kind; rejected ones stay in its history.
_Avoid_: Event (that is the show), phase event, snapshot event

**Update**:
Correcting times already recorded on a Phase — an Official changing a sent Snapshot's time, or the main Operator editing the Phase in Judge. It is its own kind of time event, not a repeat of Arrived or Presented, because it records a different intent; the earlier time stays in the history.

**Disable**:
The main Operator negating a time event: its outcome becomes a manual reject, so the previous accepted time of that kind takes over again. The event stays in the history and can be enabled again.

**Change notification**:
The signal Judge sends to every app of an Event when a Participation has been changed and saved. It names the Participation, never describes it: receivers read the Participation again.
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
- Secondary business logic - contained in `NTS.Application`, `NTS.Judge`, `NTS.Witness` or the so-called applicaiton level. It handles interactions between domain boundaries and infrastructure. The key component here and entry point should be the Service. Services should be suffixed with `*Service` and usually implement multiple segregated interfaces (conforms to Interface segregation principle)
- Storage - contains operations related to storing something. All of the storage should be conform to the `IRepository<T>` abstraction.
- Blazor - the layout is composed of a Header, Drawer and Main content. Components that are navigated to via Router should be suffixed with `*Content.razor`.
- Service registration - services in NTS projects should be registed to the container via the `ITransient`, `IScoped` and `ISingleton` marker interfaces. Those interfaces shouldn't be inherited by other interfaces, but implemented by the classes implementing those interfaces. Services should not be registered manually
- *Exceptions*: `NTS.Application` - we need to keep service registraiton manual for NTS.Application as it's also being used by NTS.Warp application in order to share RPC contracts. 

## Blazor
Component rules
- Components should inherit from `NComponent` or one of it's derivatives almost always. Exceptions can be made for example if exteding an external component such as `ErrorBoundary` or `MudTextField`. 
- Component `.razor` files should not contain code-behind, instead they should *inherit* from a `Behind.cs` class of the same name - i.e `Component.razor` inherits `ComponentBehind.cs`. Components shouldn't use `partial` code behinds as this inheritance offers a better control on what to expose to the razor file. 
- Component behinds should never expose services directly, as exception in service execution leads to poor Blazor experience - `ErrorBoundaries` cannot handle some async exceptions gracefully in my experience. Instead `NComponent` defines a `Handle(Exception ex)` method which is used to that end. 
- Component behinds should expose only properties and not fields 
- Component behinds should expose only *Safe* methods - a method is safe if it's wrapped with a `try-catch` that users `NComponentHandle` or if its name is suffixed with `Safe`. The latter should only be used if the method is passed to a Safe component parameter (also suffixed by safe).
- Base classes for internal use should be suffixed with `Base` - example `Event : EventBase`. Base classes that are intended to be implemented by the user don't have to follow that rule - example `KrudShell`
- Base classes and interfaces should be in `Abstractions`zw
