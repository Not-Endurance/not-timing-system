# NoTiming

The domain glossary for NoTiming. Each term is the name used in code, issues and discussion. Architecture and layering rules live in `docs/architecture.md`.

## Language

### Product and people

**Product**:
A deployable system with its own user store and its own passkeys, such as NoTiming or mass-driver.
_Avoid_: app, project

**Event**:
A competition with its own event ID. Witness follows it and Judge runs it.

**Witness**:
The phone-facing web app that followers use to follow an event, and that event staff use to send snapshots.

**Judge**:
The desktop client that runs an event and receives snapshots from Witness.

**Visitor**:
Someone who is not signed in. Visitors can follow events.
_Avoid_: anonymous user, guest

**User**:
A person known to a product by one email address. One record holds the sign-in credentials and the profile, and event roles refer to it.
_Avoid_: account, profile

**Official**:
A User holding an official role for one event. An official with an allowed role can send snapshots for that event.

**Operator**:
A User assigned to one event. A steward operator can send snapshots for that event.

### Sign-in

**Sign-in**:
Proving control of a User's email address, with a passkey or an emailed code, to start a sign-in session.
_Avoid_: login

**Registration**:
Creating a User by proving an email address with a code. Registering an address that already has a User signs that User in instead.

**Passkey**:
A discoverable, user-verified WebAuthn credential that signs a User in. A User can hold several.
_Avoid_: security key

**Code challenge**:
A pending request for an emailed six-digit code. It holds the registration details until the code is entered.

**Sign-in session**:
The server-side state created by a successful sign-in and referenced by a host-only cookie. Sign-out or a revoked User ends it.
_Avoid_: session, login session, UserSession

### Event data

**Snapshot**:
A group of entries that Witness sends to Judge for an event.

**UserSession**:
A Witness user's per-event record of the snapshots they have sent and selected. It is not a sign-in session.

**Tenant**:
A `TenantId` field on documents. It is not an entity, and tenant-based authorization is out of scope for sign-in.

### Transition

**Legacy user**:
A User created before sign-in codes existed, whose email has never been proved.
_Avoid_: old user, unverified user

**Adoption**:
Attaching a legacy user to a sign-in by emailed code, keeping its existing Id so event roles still apply. Transitional: removed by hand once no legacy users are left.
_Avoid_: migration, link
