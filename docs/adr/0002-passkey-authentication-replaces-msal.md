# Authentication is self-hosted passkeys, not MSAL and Entra

Status: accepted

Witness runs mostly on phones that background, refresh and navigate back constantly. MSAL keeps tokens in the browser and renews them through a hidden iframe or a redirect; on mobile Safari that renewal degrades into full-page redirects, and an interrupted redirect leaves an `interaction_in_progress` flag that blocks further sign-in until storage is cleared. We are replacing MSAL and Entra External ID with server-side sessions: an HttpOnly cookie backed by a server-side ticket, with passkeys (WebAuthn) as the primary credential through ASP.NET Core Identity on .NET 10, and an emailed one-time code as the fallback. There is no password anywhere and no compatibility window: MSAL and Entra code is deleted rather than deprecated, staging first, then production.

The relying-party (RP) ID is the registrable apex domain, with `staging.<apex>` for staging and `localhost` locally. Passkeys are bound to their RP ID and cannot be migrated between IDs, so changing it later makes every user re-enrol. The apex must be final before any production user enrols, and the ID is configuration, never hard-coded. The default `*.azurewebsites.net` and `*.azurestaticapps.net` hostnames fall under public suffixes and cannot serve as an RP ID or a cookie domain, so a domain we control is a prerequisite for device testing.

## Considered options

**Keep MSAL and harden it.** The failures come from the library's browser-held-token model, not from our configuration.

**Run an OAuth/OIDC server (OpenIddict, Duende).** It would issue tokens to clients that no longer exist: the Witness UI is same-origin with the host, and Judge is not a token client. Revisit if a genuine third-party client appears.

**A hosted auth vendor.** A recurring cost and a second identity store for a system with a hard monthly ceiling.

**Azure Communication Services Email for the fallback code.** Microsoft announced in September 2026 that standalone ACS, Email included, retires on 30 September 2028 and advises against onboarding new workloads.

## What follows from it

**One origin, one host.** The session is a host-only `__Host-` cookie, so the sign-in pages, REST, both SignalR hubs and the Witness UI are served by one ASP.NET Core host, NoTiming.Api, on each environment's existing Warp Web App. The hubs must share a process (a Witness Snapshot is relayed to Judge's in-memory primary connection, and installed Judge builds have that address baked in), which is why REST joins them rather than the reverse. That removes a shared Data Protection key ring, the `Domain=` attribute and CORS for the browser client. A separate REST host sharing the cookie was rejected for exactly that complexity. The price: REST and the hubs deploy and restart together and stay single-instance until a backplane exists. `NoTiming.Rest`, `NoTiming.SignalR` and `NoTiming.Ui` can be split out later.

**Identity users are the existing `users` documents, keyed by their id, a Guid (ADR-0009).** Operators, Officials, session state and the user copies embedded in athletes all reference that id, and Judge's user lookup still reads those documents. So Identity fields (stamps, lockout, embedded passkeys) are added in place using the collection's PascalCase naming, unknown fields are preserved, and the normalized email maps onto the existing lowercase `Email`. Unique indexes on optional fields are partial, because a plain unique index treats missing values as equal and would reject the second user without a passkey. A new identity collection with new ids was rejected: it needs a remap of every reference and forks the user model away from the domain `User` aggregate.

**Tenants are a collection seeded from countries.** Every document carries a tenant id whose only value is the constant `"nts"`, and nothing reads it. Tenants become documents in a `tenants` collection (an id like `country-bg`, a name, a kind) so non-country tenants can be added later; a user has a home tenant plus memberships, set from the chosen country at registration and never moved by profile edits. Tenant-scoped authorization is out of scope: this fixes the data shape only.

**Only leaf apps move to .NET 10.** Passkey support needs it, so the host, the generic identity library, the Witness UI and the integration tests move. The shared `Not.*` and `NTS.*` libraries stay on .NET 8 while the MAUI Judge consumes them, since a net8 project cannot reference a net10 one; they upgrade in one pass once Judge moves into the web UI. .NET 8 and 9 both leave support on 10 November 2026.

## Consequences

Every user re-enrols: passkeys and Entra passwords cannot be migrated. The Identity user store is ours to maintain, so each .NET major upgrade needs a check for new members on the store interfaces. The email fallback is the weakest link (whoever controls a mailbox can sign in) and is accepted.

Public read access (ADR-0001) is unaffected: anonymous visitors still read without a session, and only writes and the profile need one. Hosted environments get no authentication bypass; local development gets a Debug-only sign-in that refuses a database marked as production. Until the Functions API is retired (ADR-0003), it and the new host both read the `users` documents, which relies on the storage layer ignoring extra elements; that setting is currently marked as removable and must stay until then.

## Amendments

ADR-0011 moves the hosts and tests to .NET 10 at once and the shared libraries when the Functions API retires: the reason in the paragraph above (MAUI Judge) is gone, but managed Functions still holds the libraries on .NET 8 until then. It also supersedes ADR-0003's anonymous Judge group: the Functions API retires at the single cutover. ADR-0013 removes Judge's in-memory primary connection, so that is no longer a reason for the hubs to share a process with REST; they still do, and stay single-instance until a hub backplane exists. ADR-0012 brings tenant-scoped authorization into scope: Tenants are created on demand at registration, become operational when the Developer seeds a Tenant Root, and the memberships carry the roles. ADR-0009 makes the identity store's key a Guid.
