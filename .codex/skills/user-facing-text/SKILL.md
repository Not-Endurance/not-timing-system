---
name: user-facing-text
description: Use when adding or changing user-facing text in NTS UI, contracts, validation messages, resources, helper text, labels, buttons, or localized strings.
---

# NTS User-Facing Text

Use this skill whenever text can be shown to a user.

## Rules

- Ensure user-facing text is localized.
- Localize through the standard `NStrings` / `NtsStrings` property approach, following the `localization` skill for key ownership and resource updates.
- Do not make the property name a description of the text. Keep it close to the existing text, use `_` instead of spaces, and always end with `_string`.
- Parameters in localized values must be single-quoted, for example `'{0}'`.
- If a localized value contains a dynamic value, represent it in the property name with a double underscore.
- If a value has multiple sentences, use only the first sentence for the property name.
- If the property name would exceed 80 characters, cut it at an appropriate place and append `_string`.
