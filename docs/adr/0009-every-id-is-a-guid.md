# Every Id is a Guid, and the legacy ints are converted once, at the cutover

Status: proposed

Ids are random ints minted in whichever process builds the entity: a static `Random` and a static set that is unique per process only, so a collision surfaces as a Mongo duplicate-key error. In production data this is what no stable identity looks like: in fifteen of twenty-nine finished Events the Athlete copies all carry the Id 0, and older Events minted a new Horse id for almost every ride. ADR-0010 needs identities that outlive an Event, and a Guid is an id any process can mint without asking anyone. We could give only Athlete and Horse a Guid, but `Entity`, the repository and Krud abstractions, the routes, the hub payloads and the stored documents are `int` all the way down, so that leaves two base model shapes to support for good. We chose one: every Id is a Guid, in the domain, in storage and on the wire, and the legacy ints are converted once.

## What follows from it

**One kind of id.** `Entity`, `Aggregate`, `IIdentifiable`, the repository ports, Krud, the `{id}` routes, every model and every foreign key (`EventId`, `UserId`, ...) carry a Guid. Start numbers, distances and counts stay ints. New ids come from `Guid.NewGuid()`. On the wire the id is already a string (ADR-0008) and stays one.

**Equality compares ids.** An `Entity` equals another of the same type with the same `Id`. Today equality compares hash codes, which is exact only because an int's hash is the int; `Guid.GetHashCode` folds 128 bits into 32, so two different Guids can compare equal.

**Stored as BSON UUID, set explicitly.** One registration writes every Guid, `_id` included, as a standard-representation UUID. No Guid is persisted today, so nothing relies on the driver's default.

**The data converts once, last.** `migrate-identities` replaces each int with a Guid derived from the id space, the parent document and the int, a name-based UUID, so the conversion is a pure function: a run that stops halfway finishes when it is run again, and a rehearsal on a restored copy produces the Guids production will get. Copies of one thing share an id space and therefore a Guid; ids of things embedded in one aggregate are scoped to it, because the ints were unique per process only. Athletes and Horses that are the same take the survivor's Guid (ADR-0010). It is the last migration of the cutover, after those of ADR-0005, ADR-0006 and ADR-0007, so it converts the final shapes.

**Code first, data last.** The Guid change lands before the other specs' code, so everything after it is written against Guid, and the identity store of the passkey work (ADR-0002) is keyed by Guid from the start. From the first Guid commit on, every release of the branch needs the data migration, and no old Judge or Witness build is supported, the condition ADR-0006 already sets.

## Considered options

**A Guid for Athlete and Horse only.** Fixes identity and keeps two shapes of entity, repository, Krud model and route for good. A generic `Entity<TId>` through the shared `Not.*` libraries costs nearly as much as converting.

**A Guid property next to the int id.** Two ids per entity, and the int keeps its flaws: collisions, the value 0 and equality by hash.

**A table of old int to new Guid.** Stateful, so it can be lost or stale between runs, and every reference needs a lookup. A pure derivation stores nothing.

**Time-ordered Guids.** Better index locality, not needed at this size, and `Guid.CreateVersion7` is .NET 9 while the shared libraries stay on .NET 8 (ADR-0002). Changing the generator later touches one method.

## Consequences

ADR-0002's "keyed by their int id" and the `rest-api` skill's "an integer in the domain" become "a Guid", and filter examples use a Guid literal, which the OData grammar writes unquoted. Judge, Witness, the API and Warp ship together, as in ADR-0006. Links and bookmarks that carry an int id stop working, as the route renames of ADR-0007 already do for past Events. The old ints survive only in the backup taken at the cutover, which is also the rollback. A hash collision between two ids can no longer pass for equality, and a test pins it.
