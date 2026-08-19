# Public read access for the Witness app

Status: accepted

Every Witness page sat behind a blanket `@attribute [Authorize]` in `_Imports.razor`, so following a live event required a Microsoft sign-in even to read a startlist. We decided to make the read-only surface — everything except `/snapshot` and `/profile` — publicly available without registration, while keeping every write (Snapshotting) behind an authenticated Official or Operator, enforced server-side in `WitnessReceiveAuthorizer` exactly as before.

## What this does and does not change

The authorization boundary itself is unchanged. Warp already refused unauthenticated writes, validated the Warp scope, and checked the Official/Operator lookup in Mongo; the Witness hub's `OnConnectedAsync` was already unauthenticated. What changes is the client: the blanket route attribute is removed, and `NtsClientRpcAccessTokenProvider` stops demanding a token unconditionally.

Access is modelled as a four-state `WitnessAccessLevel`: `Unknown` (pre-initialization only), `Anonymous` (no session, independent of whether an event is connected), `Registered` (signed in, no write role), and `Official`. `Anonymous` is deliberately session-scoped rather than event-scoped so the navigation drawer can decide whether to show "Sign in" before any event exists.

Note that `Official` is not the true write boundary and never was: `SnapshotAccessPolicy` grants writes to an Official in {Steward, ChiefSteward, GroundJury, GroundJuryPresident} **or** an Operator with role Steward.

## Considered options

**Anonymous SignalR connections vs. REST-only reads.** We chose to let anonymous clients connect to Warp and receive live pushes. A REST-polling fallback would have avoided unauthenticated socket connections entirely, but live data is the Witness app's purpose — a public view that lags is close to worthless — and the server already treated connect as public while enforcing on write.

**A lightweight anonymous token.** Rejected: issuing tokens to unregistered visitors reintroduces registration through the back door and defeats the point of the change.

**Distinguishing anonymous from expired sessions.** `NtsClientRpcAccessTokenProvider` consults `AuthenticationStateProvider` up front rather than inferring intent from an `AccessTokenResultStatus`. An Official whose token merely expired must still be redirected to refresh; silently degrading them to a read-only connection would surface later as confusing rejected Snapshots.

## Consequences

We accept unbounded unauthenticated SignalR connections to Warp as a new abuse surface. `CorsOriginValidator` constrains browser origins but not direct clients, and there is no connection cap or rate limit. This is an explicit acceptance, not an oversight — rate limiting is real work justified by load we have not observed, and it can be added without revisiting this decision.

The profile-completion gate is narrowed to `/snapshot`. Left as-is, a signed-in user with an incomplete profile would have been pinned to `/profile` while an anonymous visitor browsed freely — signing in would have granted strictly less access than not signing in.

Public read access is hard to walk back once links circulate. That is the main reason this is recorded here rather than left to the commit history.

## Out of scope

The Nexus HTTP API is untouched. Every Function is already `AuthorizationLevel.Anonymous`, destructive ones included; this change does not alter that exposure, but it does change the invitation. Hardening it is tracked separately.
