---
name: rest-api
description: Shape HTTP endpoints, payloads, query parameters and the client repositories that call them. Use when adding or changing an endpoint in NoTiming.Api or the client that calls it.
---

# REST API

Endpoints follow JSON:API 1.1 (jsonapi.org). Only the grammar of `filter` is borrowed from OData. The reasons are in ADR-0008. A resource that has not been ported yet is legacy and keeps its shape (list at the end).

## Routes

- A collection is a plural kebab-case noun: `/events`, `/snapshot-results`. An item is `/events/{id}`.
- Event-scoped resources stay flat and carry `eventId` as an attribute: `/participations?filter=eventId eq 5`.
- The method carries the meaning: POST creates (201, `Location`), GET reads, PATCH changes members (200 with the resource), DELETE removes (204).
- A named collection is a GET-only view of one resource type that the glossary names: `/events/live`, `/events/historic`. It returns the same resource type, each item's `self` link points at the canonical item URL, and it takes the same query parameters. Every other narrowing uses `filter`.
- Model an operation as a create, update or delete of a resource: starting an Event is `POST /events` naming the configure event, resetting one is `DELETE /events/{id}`. An operation whose effect differs from its method's default meaning shows its intent in the path, `POST /{collection}/{id}/actions/{verb}`, and is noted in this file.

## Documents

- Media type `application/vnd.api+json` on both `Content-Type` and `Accept`.
- A resource object is `{ "type": "<collection>", "id": "<string>", "attributes": { ... } }`. The id is a string on the wire and an integer in the domain.
- Members are camelCase. Foreign keys are plain attributes (`eventId`, `participationId`). `relationships` and `include` wait for a consumer that needs them.
- Values the server computes (`isLive`) are read-only attributes; writes ignore them.

## Status and errors

- 200 read or update. 201 create. 204 delete. 400 malformed request, filter or parameter. 404 missing item. 409 conflict with current state. 422 domain rule violated. 500 unexpected. An empty collection is 200 with `"data": []`.
- Every error is `{ "status", "code", "title", "detail"? }` in `errors`. `code` is a stable kebab-case identifier (`event-ended`) that clients branch on. `title` and `detail` are for people and change with the wording.
- The status always reports the outcome.

## Query parameters

- `filter` holds one OData `$filter` expression: `filter=eventId eq 5 and isNotRanked eq false`. The server maps it onto `ODataQueryOptions` and applies it to Mongo LINQ.
- `sort` lists members, `-` prefix for descending (`sort=-endDay`). It maps to `$orderby`.
- `page[size]` and `page[number]` map to `$top` and `$skip`. A collection that can outgrow one screen takes them.
- `fields[...]`, `include` and any other parameter answer 400 until a consumer needs them.

## Legacy resources

Served by the Functions project, in their old shape (`Result<T>` envelope, PascalCase, HTTP 200 for domain errors): athletes, clubs, configure-event, countries, event, handouts, horses, officials, operators, participations, rankings, settings, snapshot-results, user-sessions, users. The ticket that ports a resource removes it from this list.

## Done

An endpoint is done when every section above holds for it, every failure path returns a `code`, and an integration test covers one success and one rejected request.
