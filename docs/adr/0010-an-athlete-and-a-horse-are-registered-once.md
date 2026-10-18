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

## Amendments

ADR-0012 settles the scope of the registry: one row per Athlete, Horse, Club or Official across all Tenants, owned by the Tenant that registered it. Other Tenants reference it by Guid through the cross-tenant search and cannot edit it, and the duplicate warning spans Tenants.

## Readings made when the matching and the guards were built (#635)

- **One domain service, three keys.** `NTS.Domain.Setup.Services.Identities` holds the rules, pure: `IdentityNames` (the name normalised, in Latin letters by the official Bulgarian-to-Latin system, folded to the looser key, and the words of that key in one order), `IdentityFeiIds` and `IdentityMatcher` (`Classify` a pair, `Among` many for a form, `Group` a set of copies). The class that decided is reported (`MatchClass`): same FEI ID, exact name, strict transliteration, same legacy id are automatic, in that order of precedence; spelling variant, near-miss and conflict are for a review.
- **What is a conflict.** Equal names (exact, by transliteration or by the looser key) under different FEI IDs; a FEI ID or a legacy id that two copies share while their names share nothing (not even a near-miss); and a legacy id that two copies share under different FEI IDs. Two different FEI IDs under names that are only a near-miss are two people, and are not listed. A FEI ID or legacy id match under names that are a near-miss is still a match. A zero FEI ID (any number of zeros) is no FEI ID for an Athlete and for a Horse. A legacy id is given to the service as the text of the number (ADR-0009 allows no integer identifier in our code): its zeros do not count and 0, blank and none are no id.
- **The looser key** folds what the ADR names and the common spellings of the same letters: a mark on a letter goes; ц as ts, tz or c is c; щ as sht or shch is sht; х as h or kh is h; ю as yu, iu, ju or u is u; я as ya, ja, ia or iya is ia, a doubled i being one; what is left of й as y or j is i; x is ks, w is v. ж is not folded (Yana and Zhana stay two names). A hyphen is a space; a dot or an apostrophe goes. The key of a key is the key.
- **A near-miss** is at most two edits of the words of the looser key taken in one order, and fewer edits than half the length of the shorter key, so that two short Horse names (Max and Mia) are not a near-miss of each other and the review is not filled with them. The same words in another order are a near-miss (distance 0). Say if the owner wants exactly two edits whatever the length.
- **A group** is what automatic matches join through any chain, so a Setup row that has both scripts joins a copy in Cyrillic only and a copy in Latin only that match nothing alone. A group that holds two copies that disagree is held whole. A review item is a pair of groups with something alike between them, told by the strongest of the classes between them (a conflict, then a spelling variant, then a near-miss). The same copies in the same order give the same groups.
- **The registry form warns, and names the row.** It warns of a row with the same FEI ID, of one with the same name (the same text, or the same name in the other script) and of one with a similar name (a spelling variant); a near-miss is not a warning. A row is named with its FEI ID and whether it is another Tenant's. The other Tenants' rows are those the cross-tenant search finds for the text that was typed, so one in another script is found once the Search of #638 matches by the looser key. There is no picker in the registry form: to offer a row is to name it.
- **A row that a Participation refers to is not deleted.** The Api refuses with 409 `athlete-in-use` or `horse-in-use` when a Participation of any Tenant, of an Event that has started or of one that has not (its Setup), holds a copy with the id of the row. The row is found among the Tenant's own first, so another Tenant's row is 404 whether it is named or not. The Krud lists ask the service what is there after a delete, so a row that was refused stays in the list and the person has been told why.
- **`Athlete.User` is retired.** The domain property, the model member and the form's carry of it are gone; a stored `User` of an older document is ignored on load and left as it is by an edit, and the legacy Functions host no longer sets it.


## Readings made when the dry run was built (#636)

- **A spelling is the unit.** Copies that agree on the legacy id, the name, the name in English and the FEI ID are one spelling, however many documents hold them, and the matching is done once for them. A Setup row is always a spelling of its own, so that Setup duplicates stay countable. Country and Club are evidence for the report and are not part of a spelling. A copy with no name at all is listed and set aside.
- **The counts.** An identity that has no Setup row is a row to create, and its copies are the copies whose Setup row is gone; an identity with more than one Setup row is a duplicate, and the rows beyond the first are the ones that would merge. A Participation that two Rankings of before ADR-0006 copy is one ride, whatever the number of copies.
- **The key of a decision** is made of the normalised spellings of the two groups of an item (trimmed, spaces collapsed, lower case, in order) and their FEI IDs, the smaller group first: `athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev`. A held group is decided under the key of the group alone. A database id is never in a key.
- **The decisions file** is `{ "rules": { <class>: <word> }, "items": { <key>: <word> } }` with the word `same`, `different` or empty, and the classes `Held`, `Conflict`, `SpellingVariant` and `NearMiss`. An item's own word beats the rule of its class. The dry run writes every item still to decide with no word, keeps everything the owner wrote, and reports a key that matches nothing in the data and a `different` that other decisions of `same` overrule.
- **A held group is decided whole.** `same` makes all of it one; undecided, or `different`, every spelling of it stays apart, which is the rule that items left undecided stay separate identities. A `same` for a pair next to a group that is held and undecided waits for it, and is reported.
- **The days of an Event** that the report names are the days of its place, not the UTC dates of the instants it is stored as, so that the report is the same on any machine.
