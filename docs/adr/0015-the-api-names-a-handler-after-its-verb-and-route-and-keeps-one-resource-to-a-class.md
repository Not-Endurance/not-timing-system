# The Api names a handler after its verb and route, and keeps one resource to a class

Status: proposed

The JSON:API code of NoTiming.Api is good and stays (ADR-0008, ADR-0012): the documents, the errors with a stable code, the query, the one endpoint filter that decides who may and validates the request. What was not consistent was the code around it. A handler was called `List`, `Read` or `Remove` in a dozen classes, so a stack trace or a search named no route; `AccountEndpoints` mapped sign-in pages, code challenges, sessions and `/me` together; an endpoint class called another's helpers; request types lived at the foot of the endpoint file that wrote them first; and every handler held its own status, code and title as literals, though ADR-0008 says it holds none. With 77 route registrations in 23 files this is the point to settle the conventions, so that a new endpoint is written by copying the shape of its neighbour and the code itself is the catalogue of the Api.

## What follows from it

**A handler is named from its method and its route, by a rule a test can check.** The name is the method (`Get`, `Post`, `Patch`, `Delete`) followed by each segment of the route without `api`, in Pascal case, with a parameter written `By` and its name: `GET /api/me/profile` is `GetMeProfile`, `POST /api/passkeys/actions/request-options` is `PostPasskeysActionsRequestOptions`, `PATCH /api/events/{id}/branding` is `PatchEventsByIdBranding`. There is no `Async` suffix. `EndpointNames` derives the name; the routes that a family of resources makes from a lambda (the registries, what an Event keeps, the links of a Participant) are named with it (`MapNamedGet` and the like), so that nobody types a name; and `EndpointNamesTests` holds every route of the real Api to the rule and says the expected name when one is not. Pages (`/sign-in`, `/register`, `/privacy`, `/account/passkeys`), the hub, `/healthz` and the answer to an unknown API route are not routes of the JSON:API and are not held to it.

**The intent of a route is in its URL.** POST creates, PATCH changes members, DELETE removes, GET reads, and there is no PUT: the JSON:API changes with PATCH. Because the name is derived from the route, an operation that is not plain CRUD says what it is in the URL (`POST /api/passkeys/actions/request-options`, or a resource nested under the one it belongs to), never in a handler name that the URL does not carry.

**One class maps one resource.** A `<Resource>Endpoints` class holds the routes of one JSON:API resource and their handlers, and its `Map<Resource>` registers them; the pages of the sign-in surface are `AccountPages.MapAccountPages`. `ApiEndpoints` lists the classes in the order that matters, and `ApiPipeline` holds only the middleware. A class that two endpoints share something through is not an endpoint class: the document two resources take is a request type, the rule two ask is a class of its own, and `NoEndpointClassIsCalledByAnother` fails an endpoint class that another source names.

**What a request is and what an answer says are beside the endpoint, in files of their own.** The request types of a resource are in `<Resource>Requests.cs`. The status, the stable code and the title of every answer a handler or a request gives are named properties of `<Feature>Failures` (`AccountFailures.InvalidEmail`), with `With(detail)` for what is wrong this time, and the codes the pipeline itself answers are `JsonApiFailures` and `AccessFailures`. A handler returns `Failures.X.AsResult()` or throws a typed exception that `ApiFailures` maps, and holds no code string. The wording and the codes did not change when they moved.

**The rules of the JSON:API are a skill that names no repository.** `.claude/skills/rest-api/SKILL.md` says how to shape endpoints and the clients that call them in any repository: naming, central authorization, central serialization, paging and filtering, repositories. How this repository does it is in the code, in this ADR, ADR-0008, ADR-0012 and the "Endpoints" section of `AGENTS.md`; there is no catalogue of routes to keep in step with the code, because the name of a handler is its route and `ApiEndpoints` lists the classes.

## Considered options

**Names that say the intent (`Assign`, `Rename`, `SelectTenant`).** They read well and cannot be checked, so `List`, `Read` and `Remove` repeated in every class and a name told nothing of its route. For plain CRUD the verb is the intent, and for the rest the URL says it.

**A catalogue of routes in the skill, or generated into a document by a test.** The first drifts from the code, as the old one did, and the second is machinery to keep a copy of what the names and the list of classes already say.

**An analyzer for the names.** It would fail in the editor rather than in a test, and costs a package of its own; the test enumerates the real routes, as `DenyByDefaultTests` does, and says the expected name.

**Splitting by feature (`MapAccount`, `MapTenancy`).** A feature map that composes other maps is a layer with nothing of its own, and the pages and the API routes of the account were in one. `ApiEndpoints` is the one list.

## Consequences

Renaming the handlers changed no URL, method, status code, error code, header, cookie or payload; the integration tests that call the routes were not changed. The log category of "signed out" is now `SessionEndpoints`. What is not plain CRUD today and has its intent in the name of a route (`console-operators` and `event-grants` are assigned and removed as resources of their own, by the id of the Event in the body) keeps its URL until a change of the URL is made with its clients and tests. The account routes (code challenges, sessions, registrations, passkeys) still read their own bodies and hold the ceremony in the handler; moving them to the shape of the others is the Identity module's work (#658). The error codes of the logo reader (`Logo`) are still literals of that class.
