---
name: not-infrastructure-conventions
description: Use when adding, reviewing, or refactoring reusable Not* nuget infrastructure, shared print/file/browser/backend services, Blazor support components, HTTP/storage abstractions, server adapters, or any code where Codex must decide whether behavior belongs in generic Not infrastructure versus NTS business/domain/application code. Apply conventions that keep Not* nugets generic, convention-driven, lightly customizable, and free of NTS-specific business logic.
---

# Not Infrastructure Conventions

Use Not* nugets as reusable infrastructure and UI foundation packages. They should set consistent conventions, encapsulate repeated technical behavior, and expose a narrow customization surface. They should not become broad frameworks or places for NTS business logic.

Use this together with `$logic-separation` when a change also involves domain rules, application orchestration, repositories, or business state transitions. Use `$blazor-components` when the change touches Razor component structure.

## Boundary Rule

Keep the meaning of the behavior in the owning layer:

- Put NTS business rules, event-specific workflows, ranking/handout semantics, domain entities, and application use cases in `src/Domain`, `src/NTS.Application*`, or app-specific NTS projects.
- Put generic technical behavior in Not* nugets: browser file delivery, print rendering infrastructure, HTTP helpers, Blazor building blocks, storage adapters, authentication primitives, serialization, localization plumbing, and framework integration.
- Put app adapters in `src/Apps/*`: Azure Functions, Blazor pages, hubs, process entrypoints, route binding, response shaping, and app-local wiring.

Not* code may know about generic contracts such as file content, print requests, HTTP settings, or component parameters. It must not know about NTS concepts such as event ids, document types, rankings, handouts, participations, officials, protocols, business statuses, or NTS-specific file naming.

## Convention, Not Framework

Design Not* nugets to establish a convention that works out of the box.

Prefer:

- One small primary abstraction for a capability, such as a print service that owns print actions.
- Static defaults and simple option objects for values that naturally vary.
- A small number of extension points where real use cases need variation.
- Encapsulation of the core workflow so every application gets the same behavior.
- Generic request/response shapes that are reusable across apps.

Avoid:

- Exposing every internal decision as a parameter or option.
- Making consumers inject several low-level services just to perform one user action.
- Requiring consumers to compose infrastructure steps such as API call, byte download, iframe print, and confirmation plumbing.
- Adding app-specific enum values, ids, flags, paths, templates, or document names to Not* contracts.
- Creating framework surfaces with many knobs before repeated use proves they are needed.

If a consumer must pass many configuration values, ask whether Not should choose a convention instead and expose only the meaningful variation.

## Service Shape

Choose service boundaries by capability rather than mechanism.

- Use a single public service for the thing the consumer wants to do, such as `INPrintService` for print activities.
- Hide technical collaborators behind the implementation, such as API clients, JS runtime modules, Playwright renderers, or file download scripts.
- Use a separate low-level collaborator only when it is a real reusable port, not just a step consumers should orchestrate.
- If an interface directly represents one simple implementation and no broader abstraction family is involved, keep the interface in the same file as the implementation.
- Keep public request contracts generic and client-shaped: callers provide content, file names, template ids, page options, and assets; backend services render and validate generically.

Example direction:

- Good: `BrowserPrintService` implements `INPrintService`, calls `INPrintApiService`, and owns JS print/download behavior.
- Good: server `PlaywrightPrintService` implements `INPrintService` for PDF/ZIP generation.
- Bad: a Blazor page injects an API print client, a browser print service, and a file service, then manually coordinates them.

## Not Package Responsibilities

Put these in Not* nugets:

- Generic contracts such as `NFileContent`, `NPrintDocumentRequest`, and `NPrintPageOptions`.
- Generic validation of transport/infrastructure contracts: required HTML, safe file names, supported template ids, generic page options.
- Generic browser infrastructure: JS interop for print/download, local storage, file delivery, browser-only adapters.
- Generic server infrastructure: Playwright rendering, template rendering, common CSS, static template assets.
- Generic Blazor components that encapsulate their own infrastructure dependencies and expose minimal parameters.

Do not put these in Not* nugets:

- NTS document type, event id, ranking id, participation id, official/logo paths, or business workflow decisions.
- Business-specific naming such as ranklist ZIP entry names, handout deletion policy, or event completion sequencing.
- Domain/application rules, domain events, repository orchestration, or app-specific side effects.

## Customization Surface

Allow customization when it changes reusable behavior, not when it leaks internals.

Good customization:

- `templateId` selecting a supported generic template.
- Page options such as paper format, orientation, margin, and scale.
- Caller-provided HTML/content/assets/file names.
- A small set of parameters that directly affect a reusable component's visible purpose.

Poor customization:

- Labels/colors for a component that already has a convention.
- Business document type flags.
- Backend-known logo paths for app-specific images.
- Service injections that force consumers to know the internal workflow.
- Options for behavior that every app should handle consistently.

## Review Checklist

Before committing a Not* change, check:

- Does this package still make sense without NTS?
- Is any NTS domain word or business rule leaking into Not contracts or services?
- Does the public API express the capability, or the implementation steps?
- Can a consumer use the feature by providing content/data rather than wiring infrastructure?
- Are defaults conventional and stable?
- Is customization limited to real variation points?
- Are separate class/interface/record definitions in separate files, except simple interface-plus-direct-implementation pairs or private nested definitions?
- Are tests placed at the owning layer: generic Not behavior in unit tests, high-value NTS workflows in integration tests?
