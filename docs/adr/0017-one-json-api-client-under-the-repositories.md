# One JSON:API client does all JSON:API work, and the Ui reaches the Api only through repositories

Status: proposed

The Ui reached the Api through a transport that returned untyped JSON. Each caller built documents, paths, pages and filters by hand and read the answers by hand. `JsonApiRepository` did that work once for the repositories, but it tells a person of a failure itself, turns a failed list into an empty one, and keeps the last error as state. This decision moves all JSON:API work into `JsonApiClient`, gives every failure one type, and makes the repositories the only way the Ui reaches the Api (ADR-0008).

## What follows from it

**The client does the JSON:API work.** `JsonApiClient` (`Not.Application/HTTP`) builds every document, path, page and filter, sends the media type and the write headers, and reads every answer. Its methods are named for the HTTP method: `Get` (a list, with a query), `GetOne` (one row by id), `Post`, `Patch` and `Delete`. An action or a view (`events/live`, the hand-over action, `me`) is the same method with a relative path. The client returns each answer as a value: its status, its document, and its error with the code. It throws only when no answer came. Its JSON options are set on `JsonApiSettings`, and a null is written as null.

**Repositories are the only way in.** A service calls a repository and never the client. `JsonApiRepository` (in `Not.Blazor.Client`, the browser-side package) is the one implementation over the client. There is one repository per resource. It takes the ports the resource has (`ICreate`, `IRead`, `IReadMany`, `IUpdate`, `IDelete`), and an operation beyond them is a method on it. A singleton, such as `me`, `me/profile` or a branding under its owner, is a method on the nearest resource repository. The repository gives the client the collection name and the model, and nothing else.

**A write carries the model.** A write sends every member the model holds. The id goes as `data.id`, not as an attribute. A member the endpoint does not take is ignored, not refused (ADR-0008). A write from the Ui can therefore carry a member the server owns, such as `tenantId`. The endpoints must not take such a member from a body, and the slice that moves each endpoint checks that.

**Failures are one type.** A repository throws `RepositoryException` (in `Not.Application/CRUD/Ports`, beside `IRepository`). It carries the words to tell a person (`Message`), the stable code the Api gave (`Code`, null when there is none), and a `Kind`:
- `Unavailable`: no answer, or an answer to try later: 5xx, 408, 429 and `participation-busy`. The same request may be sent again.
- `NotAllowed`: 401 or 403. The account is asked again.
- `Refused`: any other answer. The same request gets the same answer.

`JsonApiException`, a subtype of `RepositoryException`, carries the `JsonApiResponse`: status, headers, document, `Location`, error and server time. The repository throws it for every answer that refused a request. A request that got no answer is an `Unavailable`, with the transport exception inside it. A missing row is not an error: `Read` returns null, and deleting a missing row succeeds.

A component catches the exception in one of its safe methods and passes it to `Handle`. A `NotAllowed` answer asks the account again in one place, which both `Handle` and the background resend of Snapshots use.

**Paging and filters are bounded.** The route sets the page size: 500 for a list and 25 for a search, the most the Api takes for each. A list that reads past 1,000 pages throws, and it is never cut short silently. The filter writer emits `eq`, `and` and `contains`, the only operators the Api reads. Any other expression is refused, and so is a scope that cannot be written; neither is read in full and filtered afterwards. An operator that a flow needs is added on both sides at once.

**Tests fake the transport.** `JsonApiClient` is tested against a fake `HttpMessageHandler`, services are tested with a fake `IRepository<T>`, and the integration tests run the real Api in-process. There is no in-memory client.

## Considered options

**An in-memory client as the test seam.** It would repeat the JSON:API answers in test code, and it would drift from the real client.

**A client per resource.** The endpoints are known when a repository is written, so a client per resource would add a second place for the same knowledge.

**The client throws on every refusal.** Callers would catch by type and lose the response. Values keep each answer whole, and the repository decides what becomes an exception.

**The server refuses members it does not take.** This was the rule of ADR-0008 before this decision. It makes the client learn which members the server owns. Ignoring them keeps that knowledge on the server, at the cost that a mistyped member answers 200 and is not applied. The Ui and the Api ship in one host (ADR-0011), so the client can be kept in step with the endpoints.

**A list of never-sent members, or an id mode, on the descriptor.** The client would learn what the server owns. The descriptor carries the collection name only.

**An architecture guard against JSON:API logic outside the client.** Not part of this decision. The rules here and in the rest-api skill are enforced by review.

**Tenants in the URL.** Deferred to a separate decision. Until then a client does not pass a tenant (rest-api skill), and the Api takes it from the row, the Event or the account's selected tenant.

## Consequences

ADR-0008's client paragraph is amended in place, as it is still proposed, and its clause on members the endpoint does not take is removed. `docs/agents/shared-packages.md` changes its rows for `Not.Storage` (Mongo only) and `Not.Blazor.Client` (the JSON:API repositories). The Ui no longer references `Not.Storage`, so `MongoDB.Driver` and the OData packages leave its first load, and the first-load test measures the difference.

`IRowReader<T>`, `RowRead<T>` and `HttpCache<T>` are removed. `ParticipationStore` catches `RepositoryException` and skips the change, as it skipped a failed read before. The Warn toast that a failed read showed in the store goes with it.

The MongoDB repositories are out of scope here. When they are next changed, they throw `RepositoryException` with the same three kinds.
