## Overview
This project employs a Domain Driven Design architecture with strict layer and boundary separations. The codebase consists of two parts:
- Not* "nugets", located in /nugets
- NTS* - stands for No Timing System - the name of the product. 

You can see a clear corelation between Not and NTS projects and they are layered identically. 

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
- Component behinds should never expose services directly, as exception in service execution leads to poor Blazor experience - `ErrorBoundaries` cannot handle some async exceptions gracefully in my experience. Instead `NComponent` defines a `Handle(Exception ex)` method which is used to that end. Hence Component Behinds 
- Component behinds should expose only *Safe* methods - the executing logic is wrapped in a `try-catch` block which uses `NComponent.Handle`. Alternatively a method is considered Safe if it's being passed to an `N*` component parameter with `Safe` suffix. This indicates the method is already invoked in a `try-catch` block. In this case the method has to also be suffixed with `Safe` to indicate this.
- Base classes for internal use should be suffixed with `Base` - example `Event : EventBase`. Base classes that are intended to be implemented by the user don't have to follow that rule - example `KrudShell`
- Base classes and interfaces should be in `Abstractions` directory
