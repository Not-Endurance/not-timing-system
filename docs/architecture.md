# Architecture

## Overview
This project employs a Domain Driven Design architecture with strict layer and boundary separations. The codebase consists of two parts:
- NC* "nugets", located in /nugets
- NoTiming* - the product, formerly NTS (No Timing System).

You can see a clear corelation between NC and NoTiming projects and they are layered identically. 

## NC* projects
They are separated by function and callsite - servers should reference Client-side dependencies and vise-versa. *NC* is shared amongs them. These are intended to packaged up and used in other projects to bootsrap functionality, ensure consistent behavior and allow for easier maintenance. Elements of NC should be completely stripped of business logic and should provide a streamlined, generic API striving for a ballance between strict, conssitent behavior and enough configurability to be multi-purposed.
Notes:
- `NC.Krud` - as of now this is a standalone framework designed to streamline CRUD operations of complex Domain aggregates. It allows each aggregate entity to be editable individualy via simple `IRepository<T>` interface without it's Aggregate root yielding the final control.
- Service registration - services in NC* should be registered manually, using the `N*Builder` pattern (see `NApplicationBuilder`) for example
- Components defined in `NC.Blazor` should begin with `N` - `NTable`, `NNotifier`, `NTextField` etc

### NC module interface
An NC module exposes its entry points through an `Interface/` directory at the module root, for example `nugets/NC.Server/Authentication/Interface`. The namespace follows the folder, for example `NC.Server.Authentication.Interface`.
- `Interface/` holds only the components the caller invokes as entry points, such as the service interface the caller injects. A type the caller doesn't invoke stays out of it.
- Argument and return models stay in their own subdirectories and may be public. Being used by an entry point doesn't move them into `Interface/`.
- Module wiring goes in `Interface/` too: the registration and endpoint mapping methods, such as `AddNoTimingAuthentication` and `MapNoTimingAuthentication`, so the host takes what it depends on from one folder. Configuration the host binds stays in its own folder.
- Everything else in the module is internal. Interface members never expose internal types, storage documents or BSON shapes.
- Modules that predate this rule keep their current layout and are not changed by it.

# NoTiming* projects
NoTiming (formerly NTS) is the brand of the current product - an affordable alternative to full-fledged Endurance timing systems.
- Domain Model - hardcode business logic and validation is contained within the `NoTiming.Domain.*` namespace, which holds the domain model. It has to be as pure as reasonably possible.
- Secondary business logic - contained in `NoTiming.Api.Application`, `NoTiming.Api.Application`, `NoTiming.Judge` is the so-called applicaiton layer. It handles interactions between domain boundaries and infrastructure. The key component here and entry point should be the Service. Services should be suffixed with `*Service` and usually implement multiple segregated interfaces (conforms to Interface segregation principle)
- Storage - contains operations related to storing something. Standard CRUD storage  should be conform to the `IRepository<T>` abstraction.
- Service registration - services in NoTiming projects should be registed to the container via the `ITransient`, `IScoped` and `ISingleton` marker interfaces. Those interfaces shouldn't be inherited by other interfaces, but implemented by the classes implementing those interfaces. Services should not be registered manually
- *Exceptions*: `NoTiming.Application` - we need to keep service registraiton manual for NoTiming.Application as it's also being used by the **NoTiming** shell in order to share RPC contracts. 

## Blazor
The layout is composed of a Header, Drawer and Main content. Components that are navigated to via Router should be suffixed with `*Content.razor`.

Component rules
- Components should inherit from `NComponent` or one of it's derivatives almost always. Exceptions can be made for example if exteding an external component such as `ErrorBoundary` or `MudTextField`. 
- Component `.razor` files should not contain code-behind, instead they should *inherit* from a `Behind.cs` class of the same name - i.e `Component.razor` inherits `ComponentBehind.cs`. Components shouldn't use `partial` code behinds as this inheritance offers a better control on what to expose to the razor file. 
- Component behinds should never expose services directly, as exception in service execution leads to poor Blazor experience - `ErrorBoundaries` cannot handle some async exceptions gracefully in my experience. Instead `NComponent` defines a `Handle(Exception ex)` method which is used to that end. 
- Component behinds should expose only properties and not fields 
- Component behinds should expose only *Safe* methods - a method is safe if it's wrapped with a `try-catch` that users `NComponentHandle` or if its name is suffixed with `Safe`. The latter should only be used if the method is passed to a Safe component parameter (also suffixed by safe).

## Naming
- Methods don't carry an `Async` suffix. The exception is a member that implements a framework interface, which keeps that interface's name so the implementation matches it. In the authentication module these are the ASP.NET Identity store members of `IdentityUserRepository`: `UpdateAsync`, `AddOrUpdatePasskeyAsync` and `GetPasskeysAsync`.
- Base classes for internal use should be suffixed with `Base` - example `Event : EventBase`. Base classes that are intended to be implemented by the user don't have to follow that rule - example `KrudShell`
- Base classes and interfaces that are not part of a module's public entry points should be in `Abstractions`. A module's entry points go in `Interface` (see NC module interface)
