# Passkey and email-code sign-in: implementation spec

Status: ready for implementation. Derived from issue #597 and the decisions in `CONTEXT.md` and `docs/adr/0001-passkey-authentication.md`. Where this spec differs from the issue, the differences are listed in section 14.

Terms follow `CONTEXT.md`. Components are named by boundary (Api, EventBus, Ui). This spec names no files, paths or namespaces, because the project structure is changing.

## 1. Scope

In scope: sign-in and registration with passkeys or emailed codes, the sign-in session, passkey management, profile edits, the sign-in parts of the Ui, the email seam, the transitional legacy adoption, and removal of MSAL and Entra.

Out of scope: rollout and cutover, Judge authentication, securing the existing REST routes, tenant-based authorization, migration tooling beyond adoption, federated sign-in, attestation validation, self-service email change, account deletion, and shared-device mode.

## 2. Boundaries and hosting

One host resource serves all three boundaries on one origin.

| Boundary | Owns |
|---|---|
| Api | REST routes, sign-in and registration endpoints, passkey endpoints, sign-out, the identity store (users, passkeys, sign-in sessions, code challenges, rate counters), email sending, and the transitional adoption unit |
| EventBus | The Witness and Judge hubs, the in-memory Judge connection map, pending snapshots, the UDP handshake, and snapshot write authorization |
| Ui | The Blazor WebAssembly client: anonymous by default, sign-in and registration routes, passkey JavaScript interop, profile and passkey screens |

Constraints:

- The Witness hub relays to the Judge hub through in-memory state, so the resource runs a single instance while that relay exists.
- Session validation is one component used by Api and EventBus, so REST, hub negotiation and hub connections apply the same rules.
- The host serves Ui routes with fallback to the Ui entry page. Sign-in routes are Ui routes (ADR 0001).

Origins per environment:

| Environment | Passkey RP ID | App origin |
|---|---|---|
| Production | `timing.no-company.eu` | `https://app.timing.no-company.eu` |
| Staging | `staging.timing.no-company.eu` | `https://app.staging.timing.no-company.eu` |
| Local | `localhost` | the local origin, which must be a secure context |

The app origin must be under the RP ID in every environment. Configuration sets both values exactly. Startup fails if the app origin is not under the RP ID, or if production is configured with a non-production RP ID.

## 3. Configuration

All values come from per-environment configuration. Secrets never appear in source.

- Passkey RP ID and app origin (exact values).
- Session cookie name, proposed `__Host-notiming-session`.
- Session lifetime: 30 days, sliding.
- Code HMAC key (secret). Startup fails outside development if it is missing.
- Email: provider (Brevo, console or outbox), Brevo key (secret), and sender domain (`mail.timing.no-company.eu` in production, `mail.staging.timing.no-company.eu` in staging).
- Code rules: six digits, lifetime 10 minutes, five attempts, resend cooldown 60 seconds.
- Send limits: 5 per hour per address, 10 per hour per IP address, 100 per hour overall.
- Trusted proxy: the platform front end's address, used for the forwarded client IP. No other forwarded headers are trusted.

Production startup refuses the console and outbox senders, a missing HMAC key or Brevo key, a non-production RP ID, and an app origin outside the RP ID. No authentication bypass exists in any hosted environment.

## 4. Data

MongoDB collections:

**users** (the `User` term)

- Existing fields are unchanged, including the integer `Id`, names, country, club, FEI ID and roles.
- `Email` is stored lowercase on every write. Legacy documents can have mixed case, and adoption rewrites the stored value, because the snapshot write policy compares exact values.
- `EmailProvedAt` is set when a code is verified. It is null on legacy documents.
- `PasskeyUserHandle` is 32 random bytes, base64url. It is set when the first passkey is enrolled, or at adoption. It is never the integer `Id`.
- `SecurityStamp` is random. Rotating it ends every sign-in session for the user (section 6.8).
- `passkeys` is an embedded array of credentials: credential ID, public key, signature counter, name, created time and last-used time.

Indexes on users:

- Partial unique index on `Email` where `EmailProvedAt` exists.
- Non-unique index on `Email` for lookups. Legacy duplicates are allowed until they are proved.
- Unique index on `PasskeyUserHandle` where it is set.

**sign_in_sessions**

- `_id` is the SHA-256 hash of the session token, hex-encoded. The raw token exists only in the cookie.
- `UserId`, the `SecurityStamp` value at creation, `CreatedAt`, `ExpiresAt` and `LastExtendedAt`.
- TTL index on `ExpiresAt`.

**code_challenges**

- One document per normalized address. A new request replaces the existing one.
- `Purpose` is `signin` or `register`.
- `CodeMac` is HMAC-SHA256 of the email and the code, keyed with the configured secret. Including the email binds the MAC to the address.
- `AttemptsLeft` starts at 5. `CreatedAt`, `ExpiresAt` (TTL, 10 minutes) and `LastSentAt` (for the cooldown).
- `Registration` holds names, country, club and FEI ID for `register` challenges only. It is deleted with the challenge.

**rate_counters**

- Keyed by scope (address, IP or global), the key, and the hour bucket. Holds a count and a TTL.

Identity fields never appear in a response: email, `PasskeyUserHandle`, `SecurityStamp`, credential IDs, public keys, code MACs, token hashes and registration fields. Logs follow section 13.

## 5. Endpoints

All routes sit under `/api`. Every state-changing request to a cookie-authenticated endpoint must carry the custom request header (section 7).

| Route | Method | Access | Purpose |
|---|---|---|---|
| `/api/me` | GET | anonymous | Returns `signedIn`, `displayName` and `hasPasskey`. Nothing else. |
| `/api/auth/sign-in/code` | POST | anonymous | Starts a sign-in by email. |
| `/api/auth/register` | POST | anonymous | Starts a registration with details. |
| `/api/auth/code/verify` | POST | anonymous | Verifies a code and starts a sign-in session. |
| `/api/auth/passkey/options` | POST | anonymous | Discoverable sign-in options. |
| `/api/auth/passkey/verify` | POST | anonymous | Verifies an assertion and starts a sign-in session. |
| `/api/auth/sign-out` | POST | anonymous | Ends the current session, if any. |
| `/api/me/profile` | PATCH | signed-in | Edits country, names, club and FEI ID. Country and both names are required. |
| `/api/me/passkeys/options` | POST | signed-in | Creation options for a new passkey. |
| `/api/me/passkeys` | GET, POST | signed-in | Lists passkeys, and registers one from an attestation. |
| `/api/me/passkeys/{id}` | PATCH, DELETE | signed-in | Renames a passkey, or removes it unless it is the last one. |

Unchanged anonymous routes stay as they are, including `GET /api/users` and `GET /api/users/{email}`. Those lookups are a known gap until the API is secured, and this work does not change them.

Removed: `POST /api/users/register` and `PATCH /api/users/{email}/profile`.

Response rules:

- Sign-in and register return the same status, body and headers whether or not the address has an account. Mail is sent after the response, so response time does not depend on whether a mail was sent.
- Sign-in with an unknown address creates a challenge, as with a known one, but sends no mail. Register always sends mail.
- Register with a non-empty honeypot field returns the normal response and does nothing else.
- Verify returns one generic failure for a wrong, expired, exhausted or missing challenge. Success returns `signedIn: true` and sets the cookie.
- Limits and cooldowns return 429 with `Retry-After`. They are applied before the existence check, so they do not reveal whether an address has an account.
- The passkey verify endpoint returns one generic failure.

Every new endpoint is private unless listed as anonymous above. A test enumerates every endpoint with its access level and fails on any unexpected anonymous endpoint.

## 6. Flows

**6.1 Passkey sign-in.** The Ui requests options. The browser's autofill offers a passkey on the email field, or the user taps "Sign in with a passkey". The browser runs the ceremony through JavaScript interop, and the Ui posts the assertion. The Api verifies it through Identity, finds the user from the credential's user handle, checks that the credential belongs to that user, creates a session and sets the cookie. The Ui then calls `/api/me` and continues in the app without a reload.

**6.2 Code sign-in.** The user submits an email. The Ui shows the same message whether or not an account exists: "If an account exists, a code is on its way." The user submits the code. The Api checks the attempt count and the MAC first, which uses an attempt, then branches on the address:

- one proved user: sign in;
- one legacy user: adoption (section 10), then sign in;
- more than one user document: refuse, log the case, and return the generic failure;
- no user: return the generic failure.

On success the Ui offers passkey enrolment, which the user can skip.

**6.3 Registration.** The form collects email, country, first name and surname. It shows a one-line privacy notice linking to the policy, and has a hidden honeypot field. The Api stores the details in a `register` challenge and sends the code. Verification then branches as in 6.2, with one difference: a new user is created only on the no-user branch, with `EmailProvedAt` set. On the other branches the details are discarded, and the user signs in to the existing record. The Ui then offers passkey enrolment, which a device without passkey support can skip.

**6.4 Passkey enrolment.** The Api generates a `PasskeyUserHandle` if the user has none. It returns creation options with that handle, excluding existing credentials, requiring user verification and a resident key, and requesting no attestation. The browser creates the credential, and the Api stores it through Identity's passkey handling and sends the "passkey added" notice. The default name is "Passkey, <date>".

**6.5 Passkey removal and rename.** Rename changes only the name. Removal is one atomic update that removes the credential only when the array holds more than one entry. Otherwise it returns a conflict, and the user keeps their last passkey.

**6.6 Sign-out.** The Api deletes the session document by its hash and clears the cookie. The Ui updates its state at once and reconnects the live connections anonymously.

**6.7 Session check.** Runs on every request, including hub negotiation and websocket handshakes. It reads the cookie and hashes the token, then looks up the session. It rejects a missing, expired or unknown session, and compares the session's `SecurityStamp` with the user's. A rejected session is anonymous on public routes and 401 on private ones. When `ExpiresAt` is more than an hour old, the check extends it and re-sends the cookie with the same token and a fresh `Max-Age`. A new token is issued at every sign-in.

**6.8 Stamp rotation.** The owner-only operation sets a new `SecurityStamp` and deletes every session for the user. It is a service method, not an endpoint, in this work.

## 7. Cookies and request protection

Session cookie: name `__Host-notiming-session`, value a random 256-bit token encoded as base64url. Attributes: `Path=/`, `Secure`, `HttpOnly`, `SameSite=Lax`, no `Domain`, `Max-Age` of 30 days. The host-only, `__Host-` form means sibling products on the same zone cannot read, set or overwrite this cookie.

CSRF:

- State-changing requests to cookie-authenticated endpoints must carry the custom header `X-NTS-Request`. The header is not CORS-safelisted, so a cross-origin request needs a preflight, and the CORS policy refuses it.
- The server also rejects requests whose `Sec-Fetch-Site` is `cross-site`, and requests whose `Origin` is present and differs from the app origin.
- `SameSite=Lax` alone does not stop a sibling subdomain, because subdomains of one zone are same-site. The header check is what stops it.

WebAuthn origin: the Identity passkey options set the RP ID to the configured value and pin the origin to the exact app origin. The default subdomain rule is replaced.

Security headers: HSTS, a Content Security Policy in report-only mode first, `X-Content-Type-Options: nosniff`, `frame-ancestors 'none'`, and a strict `Referrer-Policy`.

## 8. Passkeys

- Built on ASP.NET Core Identity's passkey support (`IPasskeyHandler<TUser>`, the `UserManager` passkey methods, and the passkey store contract). Identity does not map endpoints or browser script, so both are ours.
- The store implements the passkey contract against the user document and its embedded credentials.
- Options: discoverable credentials required, user verification required, attestation not validated, RP ID and origin as in section 7.
- Challenges are single-use and expire. Where Identity keeps them is an open item (section 15).
- The browser script is loaded by the Ui and handles base64url conversion and errors. Errors map to localized messages. Microsoft's Blazor template sample is the reference for the browser calls, including the workaround for password managers that break `PublicKeyCredential.toJSON`.

## 9. Email

- An interface sends mail. Implementations: Brevo (production and staging), console (development) and outbox (development and tests). Changing provider means one class and configuration.
- Templates exist for the code and for the passkey notice, in English, Bulgarian and Turkish. The language comes from `Accept-Language`, with English as the fallback. Strings use the existing localization.
- Codes never reach logs. The outbox stores codes only outside production.
- Sending happens after the response. A failed send is logged without the address or the code.

## 10. Transitional legacy adoption (ADR 0001)

Adoption is one standalone unit. The core verify path asks an optional adoption interface whether an address matches a legacy user. The unit is its only implementation. Removing the unit and its one registration line leaves the core unchanged, and the lookup then finds no legacy users.

Behaviour, applied only on a verified code:

- The address matches exactly one user document with `EmailProvedAt` null: set `EmailProvedAt`, rewrite `Email` to its normalized form, generate a `PasskeyUserHandle` if missing, keep the `Id` and roles, and sign in.
- More than one matching document: refuse and log the count, without the address.
- Passkey sign-in never adopts, because legacy users have no passkeys.
- Each adoption is logged with the user's `Id`.

The unit is removed by hand once it is no longer needed. Nothing else depends on it.

Tests for this unit: adoption keeps the `Id`, officials and operators stay authorized afterwards, a duplicate is refused, a mixed-case email is rewritten, and removing the unit leaves every other test passing.

## 11. Ui

- The Ui is anonymous by default and calls `/api/me` on start.
- Sign-in, registration, code entry, the passkey enrolment prompt, passkey management and profile are Ui routes. They use the existing MudBlazor components and layout.
- Sign-in completes in the app, with no full-page navigation. After sign-in the Ui calls `/api/me` and continues.
- Sign-out clears the Ui's signed-in state at once, then reconnects the live connections anonymously.
- The blanket authorize and the WebAssembly authentication package are removed. Sign-in routes are anonymous.
- No tokens or authentication state go in browser storage. Only non-sensitive UI preferences may be stored.
- Hub connections send the cookie automatically. Anonymous connections stay accepted for public reads. Write controls are unchanged.

## 12. Removals

- The MSAL and Entra packages, their settings, the Entra runbook, and Entra token validation.
- The blanket authorize attribute, and the Entra sections of the Witness readme.
- The test-token path: integration tokens and the integration auth switch.
- `POST /api/users/register` and `PATCH /api/users/{email}/profile`.

## 13. Logging and telemetry

Events logged: sign-in succeeded (method, user `Id`), sign-in failed (method, reason category), code sent, code verification failed (reason), limit hit (scope), passkey added, renamed or removed (user `Id`), sign-out, session rejected (reason), adoption, duplicate refused, and stamp rotated.

Never logged: codes, MACs, tokens, passkey material, or raw email. Where correlation is needed, log a hash of the email.

## 14. Changes to issue #597

- Legacy users: "not read, upgraded or linked" and "no legacy documents" are replaced by ADR 0001.
- Sign-in pages: "the backend serves the sign-in pages" and the full-page navigation are replaced by in-app Ui routes (ADR 0001).
- Passkey RP ID: `timing.no-company.eu` in production, as in the issue. The app runs on `app.` under it, which the issue did not specify (ADR 0001).

## 15. Open items to verify during implementation

These are facts to confirm, not decisions for the user.

1. Where Identity's passkey handler keeps challenges, and whether it needs ASP.NET Core Data Protection. If it does, the key ring must persist on the resource.
2. The members of the passkey store contract. The Microsoft docs do not list them, so check the API reference.
3. That Identity's options accept "resident key required", since the docs' example uses "preferred".
4. Autofill (conditional mediation) on the email field, across the target phone browsers.
