---
name: localization
description: Use when adding, reviewing, or refactoring NTS localized user-facing text, NStrings/NtsStrings properties, localized resource values, placeholders, translations, or deciding whether text should be localized.
---

# NTS Localization

Use localization for text intended to be seen by end users. Do not localize developer-only diagnostics such as `GuardHelper` messages or similar internal invariant/helper errors.

## Key Ownership

- Add generic/shared keys to `NStrings`.
- Add NTS-specific component or application wording to `NtsStrings`.
- Keep UI code and Razor markup referencing the strongly typed string property instead of raw resource key literals.

## Key Naming

- Name the property after the English text itself, not after where it is used.
- Use snake case: spaces become underscores.
- Represent dynamic values in the property name with a double underscore.
- Always end localized string properties with `_string`.

Examples:

```csharp
public static string Home_string => LocalizeString(nameof(Home_string));
public static string Sorry_we_seem_to_have_fallen_off_the_horseback_string =>
    LocalizeString(nameof(Sorry_we_seem_to_have_fallen_off_the_horseback_string));
public static string Cannot_delete___because_it_is_used_string =>
    LocalizeString(nameof(Cannot_delete___because_it_is_used_string));
```

## Resources

- Add every new key to `src/NTS/Localization/Resources/LocalizedStrings.resx` with the English value as the default.
- Add translations for the same key to the other supported resource files, currently `LocalizedStrings.bg.resx` and `LocalizedStrings.tr.resx`.
- When only part of a value is dynamic, use a placeholder in the resource value and surround it with single quotes, for example `Cannot delete '{0}' because it is used`.

## Blazor Usage

- In Blazor behind classes, expose localized text through protected properties only when markup needs extra state, composition, formatting, or fallback logic.
- Do not propagate simple localized text through unnecessary behind properties; use `NStrings` or `NtsStrings` directly in Razor when no extra state or composition is needed.
- Keep injected/default-access properties first, protected members next, and public `[Parameter]`/`[CascadingParameter]` properties last to satisfy the static analyzer ordering rules.
