---
name: code-style
description: Use when adding, reviewing, or refactoring NTS C# or Razor code where member ordering, property ordering, analyzer sorting, or formatting decisions matter.
---

# Code Style

Use these conventions for code-style choices that affect analyzer compliance or review noise.

## Formatting

- Do not apply code-style formatting manually.
- Do not run formatter-only cleanup as part of a feature or fix.
- CSharpier runs in CI and applies formatting automatically, so keep edits focused on the requested behavior and required analyzer compliance.

## Member Ordering

- Static analyzer member ordering matters.
- For properties, order by accessibility:
  - internal/default-access properties first
  - protected properties next
  - public properties last
- This especially affects Blazor behind classes where injected services are internal/default-access, computed members are protected, and `[Parameter]` properties are public.
