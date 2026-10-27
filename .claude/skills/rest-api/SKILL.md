---
name: rest-api
description: Shape JSON:API endpoints, payloads, query parameters and the client repositories that call them. Use when adding or changing an HTTP endpoint, or the client that calls it, in any repository.
---

# REST API (JSON:API)

Endpoints follow JSON:API 1.1 (jsonapi.org). Only the grammar of `filter` is borrowed from OData. What every endpoint would repeat is done once, centrally, so that a handler is the work and nothing else. The route list of a repository is its code: write the code so that it can be tallied (below), and do not keep a second list.

## Names

- A collection is a plural kebab-case noun: `/events`, `/user-sessions`. An item is `/events/{id}`. The caller's own are `/me` routes. A sub-resource nests under its owner.
- The method carries the meaning: POST creates (201 and `Location`), GET reads, PATCH changes the members it names (200 with the resource), DELETE removes (204). Use PATCH to change and never PUT, so that there is one way.
- Anything that is not plain CRUD says what it is in the URL: `POST /{collection}/{id}/actions/{verb}`, or a resource nested under the one it belongs to. Never put intent in a handler name that the URL does not carry.
- A handler is named from its method and route by a rule a test can check: the method, then each segment of the route without the `api` prefix in Pascal case, a path parameter as `By<Param>`, no `Async`. `GET /api/me/profile` is `GetMeProfile`; `PATCH /api/events/{id}/branding` is `PatchEventsByIdBranding`. A stack trace or a search then names the route. Enforce it with a test over the real endpoints that says the expected name, and name a route made by a lambda with the same function.
- One class maps one resource. Nothing but the composition root calls an endpoint class: what two endpoints share (a document, a rule) is in a class of its own. Request types are in files of their own beside the endpoint.

## Central behaviour

Write each of these once and let every handler rely on it.

- **Authorization.** A handler declares what it needs (a capability, and where the request names the place it is about). One endpoint filter finds the caller and the place, asks one policy, validates the request, and only then calls the handler with a typed context, so a handler never reads the session or asks the policy. Deny by default: a route that declares nothing and is not on the one list of public endpoints stops the host from starting, and a test enumerates the real routes.
- **The order of the answers.** The first thing wrong is the answer: not signed in (401), the transport (415, 413, 400), the place it names (400, 404), the caller's Level (403), the state of the place (409), the content (400, 422), then the work. A caller who may not do something is never told what is wrong with what they sent.
- **Serialization and response models.** One results class makes every document: a resource, a page of a collection, an error. It sets the options (camelCase members, no member that is null, enums as names), the media type `application/vnd.api+json` and the `Location`. A handler never builds JSON.
- **Documents.** A resource object is `{ "type", "id", "attributes" }`. The id is a string on the wire. Members are camelCase, foreign keys are plain attributes, and values the server computes are read-only. `relationships` and `include` wait for a consumer. A write sends `Content-Type: application/vnd.api+json`; a body of the wrong type is 415, one that is too large 413 (say the limit), one that is not one resource of the expected type 400.
- **Errors.** `{ "status", "code", "title", "detail"? }` in `errors`. `code` is a stable kebab-case identifier clients branch on; `title` and `detail` are for people. Each answer a handler gives is a named failure (status, code, title) in a failures class beside its feature, and a handler holds no code string. A rule of the domain is a typed exception, mapped to 422 (409 for a conflict with the current state) in one place.
- **Statuses.** 200 read or update, 201 create, 202 taken and the outcome not told, 204 delete, 400 malformed request, filter or parameter, 401 not signed in, 403 not allowed, 404 missing (also for a record that is somebody else's), 409 conflict with the current state, 413, 415, 422 domain rule violated, 429 too many (with `Retry-After`), 500 unexpected. An empty collection is 200 with `"data": []`. The status always reports the outcome.
- **Pagination.** `page[size]` (with a default and a maximum the list states) and `page[number]` (from 1) cut every list that can outgrow a screen; any other value is 400 `invalid-page`. Read one row more than a page to know whether another follows, and answer `meta.page` (`size`, `number`) and `links.self` and `links.next` (left out on the last page). Read the parameters in one place for every list.
- **Filtering and sorting.** `filter` holds one OData `$filter` expression (`filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 and isNotRanked eq false`; a Guid is unquoted, a string is quoted with the quotes doubled), applied by the data layer to the query and never to rows already read. `sort` lists members, `-` for descending. A list that is not sorted is in the order of the ids, so that its pages do not overlap. The members are the ones the resource shows, and one it keeps to itself cannot be filtered or sorted on. An expression that is not valid is 400 `invalid-filter` (`invalid-sort` for a sort), and so is one that is valid and that the data cannot run, never a 500. `fields`, `include` and any other parameter are 400 `unsupported-parameter` until a consumer needs them.
- **Concurrency.** A resource that is edited by more than one writer carries a `version` in the `meta` of its resource object, a change names the version it was based on, a stale one is 409 and a missing one 400. A resource whose id the client makes is created idempotently: the same id again returns the first outcome.

## Data access

- CRUD goes through the repository abstraction (`IRepository<T>`) on both sides. A service handler calls a repository; a client calls a repository whose implementation talks JSON:API to the service. The abstraction may grow to carry paging and filtering. A use case that is not CRUD gets a repository of its own, which extends the abstraction or stands alone. Data access is always behind a repository abstraction with the implementation inverted into it, and never written in a handler or in a screen.
- If the repository has tenants, the tenant belongs inside the repository implementations: they filter every read and stamp every write by the current tenant, and never move a document from one tenant to another. A handler or a client never passes a tenant. A read that reaches across tenants is a named view of its repository, never a flag a caller can send.

## Clients

- A client repository maps the repository calls onto the routes: a list is a GET with `filter`, `sort` and `page[...]` (it pages through until `links.next` is gone), a create is a POST, a change a PATCH, a removal a DELETE. Nothing else builds a URL.
- It sends the media type on `Content-Type` and `Accept`, writes a member that is null as null (on a PATCH that takes the member away), reads the error `code` and gives it to the caller instead of swallowing the failure, and sends the version it was based on where a resource has one.
- An id the client makes is a Guid, sent as the id of the resource, so that a request sent again is the same request.

## Done

An endpoint is done when the names follow the route, it declares what it needs and takes its caller from the context, every failure path returns a named failure, a list takes the pages and the filter of every list, its data goes through a repository, and a test covers one success, one rejected request and, where the request names a place, the order of the answers.
