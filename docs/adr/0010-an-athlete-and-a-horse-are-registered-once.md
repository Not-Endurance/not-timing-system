# An Athlete and a Horse are registered once, and every copy carries their Id

Status: proposed

A Participation embeds the Athlete and the Horse it was entered with, and the configure event an Event started from embeds them again, so one person or animal exists as dozens of copies with nothing tying them together: ids that were minted per copy or are 0 (ADR-0009), names in Bulgarian in some Events and in Latin in others, and a FeiId on a minority. Setup holds 60 Athletes and 71 Horses; the finished Events hold about 170 and 195 distinct ones. Nobody can ask what an Athlete or a Horse has ridden. We now register an Athlete or a Horse once, in Setup, and every copy carries that row's Guid and keeps the names as entered. A one-off migration builds the registry from the data that exists, and Setup stops new duplicates from appearing.

## What follows from it

**The registry is Setup.** Every Athlete and Horse has exactly one row in `athletes` or `horses`, including those that exist today only as copies; the migration creates those rows from the latest copy, and Setup rows that are the same person are merged into one. The Id of a copy equals the Id of its row, as the Id of a Core Event already equals its configure event's.

**Copies keep names as entered.** The migration rewrites ids and nothing else in Core: a finished Event reads as it was printed, which is also why live references from Participations to Setup stay deferred (ADR-0006). Setup rows change otherwise only to fill an empty NameEnglish.

**What counts as the same.** Matched automatically: the same FeiId (an Athlete's compared as a number, a Horse's trimmed and case-folded); an exact name after trimming, collapsing spaces and case-folding, compared across Name and NameEnglish of every copy; the same name under the official Bulgarian-to-Latin transliteration; and the same legacy id other than 0. A match on FeiId or legacy id whose names share nothing is a conflict, not a match. Sent to review: looser spelling variants (ю as yu or u, я as ya or ia), near-misses of one or two edits, and conflicts, such as equal names with different FeiIds, where the whole group is held. Country is shown as evidence and is never a veto: Bulgaria is entered as both BGR and BUL. Horses follow the same rules with no extra corroboration, because the pool is small. The owner decides review items in a decisions file; items left undecided stay separate identities.

**A migration, not a feature.** `migrate-identities` (ADR-0009) is a dry run unless told to apply. It writes its report and reads its decisions outside git, because both hold real names; it is idempotent, so it can be run until nothing is left to decide and it finishes after an interruption; and it ends with a read-only verification. It is deleted after deploy, like the other migrations. The matching rules live in one domain service so that Setup and the search use the same ones.

**Setup prevents duplicates.** An Athlete or Horse that any Participation refers to cannot be deleted, and creating or renaming one whose name (in any script) or FeiId exists warns and offers the existing row. `Athlete.User` is retired: it points from the Athlete to a user, nothing reads it, and the user's own link to an Athlete lives with the user.

**Reading by Athlete or Horse needs sign-in.** The registry makes one person's whole record queryable, which per-Event Results never were. The Participations of an Athlete or of a Horse, the Search that finds them and the Profile that holds them are served to signed-in users only, enforced by the API and hidden from anonymous visitors in the UI. That is an exception to the public reads of ADR-0001, and the privacy policy has to cover it: the Junior and Young Rider categories include minors.

## Considered options

**Live references from Participations to Setup** (ADR-0006's deferred option). Gives the same stable identity, but changes what finished Events show whenever Setup changes. Still deferred.

**A separate collection of people and horses.** Two registries to keep in step with Setup's pick lists, for rows Setup already has: names, FeiId, Country.

**Merge only on FeiId and exact names.** Safest, and leaves about thirty pairs that are plainly the same, a name in both scripts, to hand review. The pool is small enough that the owner chose confidence for strict transliteration and review for everything looser.

**Anonymous reads.** Consistent with ADR-0001, but anyone could assemble a person's record, minors included.

## Consequences

Judge's pick lists grow from the Setup rows to everyone who ever competed. A wrong merge puts one person's rides on another's page and is undone only from the backup, which is why anything looser than a FeiId, an exact name, strict transliteration or the same legacy id waits for the owner. Review items still undecided when the tool is deleted stay separate; merging them later is a Setup feature, not a rerun. The migration runs last in the cutover (ADR-0009), after the copies migration of ADR-0006, so Rankings and Handouts already hold references and only Setup, the configure events and the Participations are rewritten.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
