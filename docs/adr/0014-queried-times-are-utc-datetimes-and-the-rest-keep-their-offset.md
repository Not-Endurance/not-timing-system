# A time the database queries is a UTC DateTime named Utc, and every other time keeps its offset

Status: proposed

MongoDB's `Date` is milliseconds since the epoch and holds no offset. The C# driver stores a `DateTimeOffset` as the document `{ DateTime, Ticks, Offset }` by default, which keeps the offset, but a TTL index or a range filter on the field itself cannot use a document. Forcing the field to a plain `Date` (`BsonType.DateTime`) makes both work and drops the offset without a trace: the value comes back as `+00:00` and nothing in the type says so. We decided that the type says it. A time the server queries is a `DateTime` that is UTC by name, and every other time is a `DateTimeOffset` that keeps what its writer knew.

## What follows from it

**A queried time is a `DateTime` named `…Utc`.** A time the server filters, sorts or expires on (a TTL index, a range filter, an OData `$filter`) is a `DateTime` property whose name ends in `Utc`, with `[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]` so it reads back as UTC and not as local time. The suffix is on the property. The stored element keeps its name when documents or tools already depend on it, and `[BsonElement]` says so. The sessions and the code challenges of `Not.Identity` are the first: `ExpiresAtUtc` and `CreatedAtUtc` are stored as `ExpiresAt` and `CreatedAt`, because the indexes, and the tools that create them, name those fields.

**The conversion is ours, and it is `UtcDateTime`.** A `DateTimeOffset` becomes a stored `DateTime` through `DateTimeOffset.UtcDateTime`. `.DateTime`, `ToLocalTime()` and `DateTime.Now` store a different instant, and a `DateTime` of kind `Unspecified` is never stored. The suffix is there so that whoever reads the call site sees the conversion that is owed.

**Every other time is a `DateTimeOffset` in the driver's document form.** The property says `[BsonRepresentation(BsonType.Document)]`. That keeps the offset and the ticks, and carries the instant as a `Date` at `<field>.DateTime`. It is written on the property, because the driver keeps one serializer per type for the whole process and a registration elsewhere in the process would otherwise decide. `BsonType.DateTime` on a `DateTimeOffset` is not used: it is the silent loss. This holds when the offset means nothing today, such as the time a passkey was created, so that the type never discards what the writer had.

**What is stored already stays readable.** A plain `Date` read into the document form comes back as `+00:00`, which is what it was, so a field can change form without converting its documents. A `Date` has millisecond precision. The document form keeps the ticks.

**A test holds it.** `DateTimeShapeTests` fails on a `DateTime` property that is not named `…Utc` or does not read back as UTC, on a `DateTimeOffset` forced to a plain date, and on a driver that stops behaving as this record describes.

## Considered options

- **One type for everything: a `DateTime` named `…Utc`.** Simple, but a time whose offset is data loses it by construction. `EventSpan` builds its calendar days from the offset it is given.
- **One type for everything: a `DateTimeOffset` forced to a plain `Date`.** What `NStorageBuilder` does for the Nexus data today. Queries work, and the offset goes without a trace.
- **A `DateTimeOffset` in the document form, queried through `<field>.DateTime`.** Keeps both, but a TTL index on a nested path and LINQ filters on it are not verified, and every query would need a string path in place of an expression. Not chosen, and worth looking at again if one type for queried and kept times is wanted.

## Not applied everywhere yet

`Not.Identity` follows it. Nexus does not: `NStorageBuilder` registers every `DateTimeOffset` of the process as a plain `Date`, its models are persisted as the Contracts models, and `EventInformationBusinessService` filters `EventInformationModel.EndDay` with `>` and `<=`. Aligning Nexus changes what its queries filter on and how new documents are written, so it is a change of its own.
