# The Judge-facing REST surface is ported into NoTiming.Api and stays anonymous until Judge moves in

Status: superseded by ADR-0011

Judge (the Windows desktop app) is out of scope for authentication in this work and reaches the backend anonymously, as it does today (tracked in #593). Rather than keep the Static Web Apps Functions API alive beside the new host, we port every route Judge uses into NoTiming.Api under an explicitly anonymous route group and retire the Functions project. The Witness-only routes (registration, profile, session state) are not ported; the Witness read surface stays anonymous per ADR-0001; the print endpoints are dropped. Judge-facing user reads return profile fields only, never identity fields.

## Considered options

**Keep the frozen Functions API.** It runs on a runtime that leaves support in November 2026, cannot move to .NET 10 while hosted as managed functions, has no static egress address for a MongoDB Atlas access list (#512), and would leave two deployables sharing one database.

**A Function App on the same plan.** Fixes the runtime and address problems but keeps a second deployable and adds a Storage account.

## Consequences

The anonymous write surface now lives in the same host as the authenticated one. That exposure is unchanged from today, is confined to one clearly named route group, and can later be closed with a single check (a shared key, or authentication once Judge is part of the web UI). This ADR is temporary by design and is superseded when Judge moves into the web UI.

Installed Judge builds have the old API address baked in, so they need a build with the new address at cutover. A Static Web Apps redirect cannot carry the writes: its redirect rules support only status codes 301 and 302, which clients turn into GETs.
