---
name: blazor-components
description: Use when adding, reviewing, or refactoring NTS Blazor/Razor components, especially .razor files, component parameters, injected services, event callbacks, print components, or UI code-behind classes that should follow the repo's split .razor plus Behind.cs convention.
---

# Blazor Code Behind

## Core Pattern

Split every NTS Blazor component into:

- `ComponentName.razor` for markup only.
- `ComponentNameBehind.cs` for parameters, injected services, computed state, lifecycle methods, and event handlers.

The Razor file should:

- Include `@inherits ComponentNameBehind`.
- Avoid `@code` blocks.
- Avoid `@inject`.
- Bind only to protected/public members supplied by the behind class.

The behind class should:

- Be named after the component, for example `List.razor` with `ListBehind.cs`.
- Not include `.razor` in the class or file name.
- Inherit the repo-appropriate base type, usually `NComponent`, `NStatefulComponent`, or `ComponentBase`.
- Keep injected services non-public. Prefer private/default-access injected properties.
- Expose protected passthrough properties or methods for markup instead of exposing injected services directly.

## Services And State

Do not let markup reach through a service such as `Service.Document` or `RankingService.Current`.

Prefer behind passthrough members:

```csharp
[Inject]
IExampleService Service { get; set; } = default!;

protected ExampleDocument? Document => Service.Document;
protected IReadOnlyList<ExampleItem> Items => Service.Items;
```

## Event Handlers

Behind class methods used by markup or component callbacks must be exception-safe.

- If assigning to another component parameter whose name ends with `Safe`, suffix the handler with `Safe`.
- Otherwise wrap the method body in `try`/`catch` and call `Handle(ex)` when the base class supports it.

Examples:

```razor
<RankingMenu OnSelectedSafe="SelectRankingSafe" />
<NButtonPrimary OnClick="Save" />
```

```csharp
// Safe methods are already executed in try-catch in a dif
protected void SelectRankingSafe(Ranking ranking)
{
    Service.Select(ranking);
}

protected async Task Save()
{
    try
    {
        await Service.Save();
    }
    catch (Exception ex)
    {
        Handle(ex);
    }
}
```

## Review Checklist

- No `@code` in `.razor`.
- No `@inject` in `.razor`.
- `.razor` has `@inherits ComponentNameBehind`.
- Behind file is `ComponentNameBehind.cs`.
- Injected services are not exposed directly to markup.
- Markup uses passthrough properties for service state.
- `*Safe` component parameters receive `*Safe` handlers.
- Other handlers catch exceptions and call `Handle(ex)` where available.
- If Markup components cross 140 symbol on a single line - break the parameters apart on new lines
- Separate components on the same nested level by an empty line
- Keep behinds clear from any business or infrastructure logic - inject services instead
