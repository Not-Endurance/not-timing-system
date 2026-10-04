---
name: rest-api
description: Shape HTTP endpoints, payloads, query parameters and the client repositories that call them. Use when adding or changing an endpoint in NoTiming.Api or the client that calls it.
---

# REST API

Endpoints follow JSON:API 1.1 (jsonapi.org). Only the grammar of `filter` is borrowed from OData. The reasons are in ADR-0008. A resource that has not been ported yet is legacy and keeps its shape (list at the end).

## Routes

- A collection is a plural kebab-case noun: `/events`, `/snapshot-results`. An item is `/events/{id}`.
- Event-scoped resources stay flat and carry `eventId` as an attribute: `/participations?filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301`.
- The method carries the meaning: POST creates (201, `Location`), GET reads, PATCH changes members (200 with the resource), DELETE removes (204).
- A named collection is a GET-only view of one resource type that the glossary names: `/events/live`, `/events/historic`. It returns the same resource type, each item's `self` link points at the canonical item URL, and it takes the same query parameters. Every other narrowing uses `filter`.
- Model an operation as a create, update or delete of a resource: starting an Event is `POST /events` naming the configure event, resetting one is `DELETE /events/{id}`. An operation whose effect differs from its method's default meaning shows its intent in the path, `POST /{collection}/{id}/actions/{verb}`, and is noted in this file.

## Documents

- Media type `application/vnd.api+json` on both `Content-Type` and `Accept`.
- A resource object is `{ "type": "<collection>", "id": "<string>", "attributes": { ... } }`. The id is a string on the wire and a Guid in the domain (ADR-0009).
- Members are camelCase. Foreign keys are plain attributes (`eventId`, `participationId`). `relationships` and `include` wait for a consumer that needs them.
- Values the server computes (`isLive`) are read-only attributes; writes ignore them.

## Status and errors

- 200 read or update. 201 create. 202 taken, and the outcome is not told. 204 delete. 400 malformed request, filter or parameter. 401 not signed in. 403 not allowed. 404 missing item. 409 conflict with current state. 422 domain rule violated. 429 too many requests (`rate-limited`, with the seconds to wait in `Retry-After`). 500 unexpected. An empty collection is 200 with `"data": []`.
- Every error is `{ "status", "code", "title", "detail"? }` in `errors`. `code` is a stable kebab-case identifier (`event-ended`) that clients branch on. `title` and `detail` are for people and change with the wording.
- The status always reports the outcome.

## Query parameters

- `filter` holds one OData `$filter` expression: `filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 and isNotRanked eq false`. A Guid is written unquoted, as the OData grammar has it. The server maps the expression onto `ODataQueryOptions` and applies it to Mongo LINQ.
- `sort` lists members, `-` prefix for descending (`sort=-endDay`). It maps to `$orderby`.
- `page[size]` and `page[number]` map to `$top` and `$skip`. A collection that can outgrow one screen takes them.
- `fields[...]`, `include` and any other parameter answer 400 until a consumer needs them.

## Authorization

- Every route requires a signed-in caller unless it is on the public-read allowlist (ADR-0012): one list in the Api, enumerated by a test, read-only, and never a write. An unauthenticated caller gets 401; a signed-in caller who may not do it gets 403 with a code such as `not-main-operator`.
- Repositories filter by the current Tenant by default. A cross-tenant read is a named view of its collection, never a parameter a caller can send to any resource.
- There are no anonymous route groups. The sign-in routes below are the only writes that are anonymous: a person signs in to become a caller.
- A write sends `Content-Type: application/vnd.api+json`, and nothing else is read (415 `unsupported-media-type`). A browser may not send that type to another origin without a preflight, which the CORS policy refuses, so another site cannot write with a visitor's cookie. A body that is not one resource of the expected type is 400 `malformed-request`.

## Authentication

A person signs in with a code sent by email or with a passkey, and the session is a cookie the server sets (ADR-0002). The cookie is `__Host-NoTiming`: host-only, HttpOnly, Secure, SameSite=Lax, persistent, 30 days sliding, and it carries only the key of a ticket the server keeps. The pages that use these routes are `/sign-in`, `/register`, `/privacy`, `/account/passkeys` and their assets, served by the Api; the Ui links to them.

- `POST /api/code-challenges` with `{ "email" }` asks for a code. It answers 202 with no body whether or not the address has an account, and inside the resend cooldown of the previous code, so the answer tells nothing; a malformed address is 400 `invalid-email`.
- `POST /api/registrations` with `{ "email", "givenName", "surname", "countryId", "website"? }` registers a new person (#601): 202 with no body, a code is mailed, and nothing is stored until the code comes back to `POST /api/sessions`, which then creates the account (confirmed address, the names, a home Tenant from the country, one membership in it) and signs it in. It answers alike for an address that has an account (that address is sent a sign-in code and what was typed is dropped), for one on a cooldown, for one a configured allow-list keeps from registering (`Registration:AllowList`: addresses and `@domain` entries; staging is closed without one, production is open without one) and for a body that fills `website`, the field of the page that people never see, which only a program fills in. A malformed address is 400 `invalid-email`, a missing, too long (100 characters) or control-character name 400 `invalid-name`, a country that is not one of those the page lists (it needs an ISO code) 400 `invalid-country`. The page lists the countries from the `countries` collection. Every 202 also sets `__Host-NoTiming-Registration` (HttpOnly, Secure, SameSite=Strict, 15 minutes, the same value for a browser that sends it back): its secret is hashed into the details that wait in the challenge, and they are used only when the code comes back with the same cookie. A code that comes back without it, from another browser, still proves the address and opens the account, without the names, the country and the home Tenant: whoever asked for a code for an address first cannot choose the account its owner gets.
- `POST /api/sessions` with `{ "email", "code" }` signs in: 201, the `sessions` resource `current` (`method`: `code` or `passkey`, and `passkeyCount`, which the sign-in page uses to decide whether to offer one) and the cookie. A wrong, spent, expired or exhausted code is 401 `invalid-code`, whatever the reason. With `{ "credential" }`, the assertion a passkey made as the browser's WebAuthn API serialises it, the same route signs in with a passkey; one that signs nobody in is 401 `invalid-passkey`, whatever the reason.
- Asking for a code (`/api/code-challenges` and `/api/registrations` alike) and trying one are rate limited, per host and per hour (`Auth:RateLimits`): a request draws on the budgets of its address (5), its client (10, a client being the forwarded address, an IPv6 one by its /64) and the platform (100); a wrong code draws on those of the address as that client tries it (5), the client (10) and the platform (100), and a right code costs nothing. A request that is out of a budget is 429 `rate-limited` with `Retry-After`, before the address is looked at, so it is the same for every address, and it costs nothing. A malformed request is not counted.
- `DELETE /api/sessions/current` signs out: 204, the ticket is deleted and the cookie cleared. Without a session it is 204 as well.
- `GET /api/me` is the `accounts` resource of the caller (`email`, `emailConfirmed`, `name`, `passkeys`), or 401 `not-signed-in`. A route that needs a caller answers 401 with that code, never a redirect.

### Passkeys

A passkey is a resource of the signed-in person: `/api/passkeys`, with its credential id in base64url as the id. They are discoverable, require user verification and are never attested; the relying-party ID is configuration (`Passkeys:RelyingPartyId`, `localhost` in Development), and a host with none answers 503 `passkeys-not-configured` on every route below that needs one.

- `POST /api/passkeys/actions/request-options` (anonymous) answers the options for `navigator.credentials.get`, and `POST /api/passkeys/actions/creation-options` (signed in) those for `navigator.credentials.create`, which exclude the passkeys the person has. Both are actions in the sense above: they create nothing, and they hold the challenge in a short-lived host-only cookie that the next call consumes.
- `POST /api/passkeys` with `{ "credential", "name"? }` stores the passkey the browser made: 201 and the resource, and a "new passkey added" email in the language of the request (a mail that cannot be sent is logged and does not undo the passkey). A credential that does not verify, or arrives with no ceremony underway, is 400 `invalid-passkey`.
- `GET /api/passkeys` lists them. `PATCH /api/passkeys/{id}` renames (`name`). A name is at most 60 characters, on both routes: longer is 400 `invalid-name`. `DELETE /api/passkeys/{id}` removes: 204, and the last passkey is 409 `last-passkey`, because losing the device would lock the person out. A passkey is looked up among the caller's own: someone else's, or none, is 404 `not-found`.
- The ceremony routes (the two actions, `POST /api/passkeys`, and `POST /api/sessions` with a credential) verify an antiforgery token on top of the media type: the header `X-XSRF-TOKEN` carries the token the server put into the page, and the cookie that goes with it is `__Host-NoTiming-Xsrf` (unprefixed and not Secure-only in Development, which runs on plain http). A request without a matching pair is 400 `antiforgery-token-invalid`.

## Concurrency

- A Participation carries a `version` in the resource object's `meta`. A manual edit (PATCH) sends the version it was based on in `meta`; a stale one answers 409 `participation-changed` and a missing one 400 `version-required` (ADR-0013). Other resources are last-write-wins.
- Creating a resource whose id the client mints (a Snapshot, ADR-0013) is idempotent: the same id again returns the first outcome. It never answers 409 for staleness.

## Legacy resources

Served by the Functions project, in their old shape (`Result<T>` envelope, PascalCase, HTTP 200 for domain errors): athletes, clubs, configure-event, countries, event, handouts, horses, officials, operators, participations, rankings, settings, snapshot-results, user-sessions, users. The ticket that ports a resource removes it from this list. Settings, snapshot-results, the print routes and the routes that list users are not ported: they go with the Functions project (ADR-0011, ADR-0012, ADR-0013).

## Done

An endpoint is done when every section above holds for it, every failure path returns a `code`, and an integration test covers one success and one rejected request.
