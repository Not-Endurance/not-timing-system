# Arrivelist: parallel Regional arrival estimates

Status: ready for implementation. Handoff spec — no prior work exists on a branch.

## Problem Statement

The Arrivelist shows officials three estimated arrival times per Participation on course: the earliest permitted arrival (Fast), the expected arrival at the rider's observed pace (Average), and the latest permitted arrival (Slow). Officials use these to know when to expect a horse at the gate and whether a rider is on track to breach a speed restriction.

Those three estimates are computed one way only: cumulative distance ridden divided by cumulative elapsed time, where elapsed time includes vet-gate recovery. That is the FEI model of average speed, and under FEI it is correct.

It is not correct under the Regional ruleset. Regional rules judge speed loop by loop: a Phase's average speed is the loop speed alone, recovery excluded, and the speed restriction is fixed on each individual loop rather than accumulated across the ride. A rider who took the first loop slowly does not thereby earn the right to exceed the maximum average speed on the second. The domain already encodes this for Phase-level speed, but the Arrivelist does not, so at a Regional event every estimate an official reads is derived from the wrong rule — and the error compounds with each completed loop, since it is precisely the accumulated distance and recovery time that the Regional model is supposed to discard.

The two models are not merely differently-rounded. They answer different questions, and they diverge in direction: the FEI model lets banked slowness push a Fast deadline later, which under Regional rules would be a licence to breach the loop restriction.

## Solution

Compute both models for every Participation, all the time, and let the official choose which to look at.

Each Arrivelist entry carries two complete sets of estimates: the existing FEI set, unchanged, and a new Regional set computed loop-by-loop. A three-way selector above the table switches the view between Regional, FEI, and Both. It opens on FEI, so the screen is identical to today's until somebody deliberately moves it.

This is a deliberately additive, comparison-first step rather than a fix. No existing calculation changes, no existing default changes, and the Ruleset is not consulted anywhere. The Regional model has never run against real event data; showing it alongside the model officials already trust lets it be validated on live events at zero risk, and the eventual change — showing one set chosen by the Participation's Ruleset, and deleting the other — becomes a small, well-evidenced follow-up rather than a leap.

## User Stories

1. As a Witness official at a Regional event, I want arrival estimates derived from loop speed rather than cumulative speed, so that the times I read match the rules the event is actually run under.
2. As a Witness official, I want the Arrivelist to look and behave exactly as it does today unless I change something, so that this feature cannot disrupt an event I am running.
3. As a Witness official, I want a single selector that switches the whole table between Regional, FEI, and Both, so that I can change model without leaving the page or reconfiguring anything.
4. As a Witness official viewing a single model, I want the familiar three columns with one value each, so that the layout I have learned to read at a glance is preserved.
5. As a Witness official viewing Both, I want the FEI and Regional values stacked within each of the three columns rather than spread across six, so that the two estimates of the same quantity sit adjacent and can be compared without scanning sideways.
6. As a Witness official viewing Both, I want each stacked value labelled with the ruleset it came from, so that I never have to guess which line is which.
7. As a Witness official, I want the table to remain readable on a tablet at a vet gate, so that adding a second model does not force horizontal scrolling or shrink the times past legibility.
8. As a Witness official on a phone, I want the stacked-row layout to keep working, so that the narrow-screen presentation degrades correctly.
9. As a Witness official who has selected Regional, I want the list ordered by the Regional estimates, so that the rider I should expect first appears first under the model I am reading.
10. As a Witness official who has selected FEI or Both, I want the list ordered exactly as it is ordered today, so that ordering does not shift underneath me while I am also adjusting to a new column.
11. As a Witness official at a Regional event, I want the Fast and Slow estimates to depend only on the current loop's length and start time, so that a slow earlier loop cannot license a fast later one.
12. As a Witness official at a Regional event, I want the Average estimate to reflect the speed of the most recently completed loop, so that the expected arrival tracks the rider's current form rather than a ride-long average.
13. As a Witness official on the first loop, I want the Regional and FEI estimates to agree, so that I am not misled into thinking one model is broken when the two are mathematically identical at that point.
14. As a Witness official, I want a Participation with no completed loop to show no Regional Average, so that the column is empty rather than showing a fabricated number.
15. As a Witness official, I want a Participation whose last completed loop has no positive ride interval to show no Regional Average, so that malformed timing data produces a blank rather than an absurd estimate.
16. As a Witness official at an event containing both FEI and Regional competitions, I want every Participation to carry both estimate sets, so that a mixed event has no riders for whom one model is unavailable.
17. As a Witness official, I want entries with no estimates at all to keep sorting to the bottom of the list, so that the existing convention for unknowable arrivals is preserved.
18. As a Witness official, I want the colour coding of the Fast, Average and Slow columns to be unchanged, so that colour continues to mean urgency rather than ruleset.
19. As a Witness official working in Bulgarian or Turkish, I want the selector and its options localized, so that the new control matches the rest of the application.
20. As a technical official comparing the two models during a live event, I want to switch to Both and watch them diverge across loops, so that I can gather the evidence needed to decide which model the Regional ruleset should finally use.
21. As a developer, I want the Arrivelist to compute both models without reading the Ruleset, so that the presentation decision stays out of the domain and the eventual ruleset-driven change has exactly one place to land.
22. As a developer, I want the two estimate sets expressed as a pair of identically-shaped value objects, so that deleting the losing model later removes a property rather than rewiring six.
23. As a developer, I want the existing FEI calculation left byte-for-byte intact, so that any behavioural difference observed after this change can only have come from the new code path.
24. As a developer, I want unit tests that pin the Regional formulas independently of the FEI ones, so that a discrepancy seen on a live event is known to be a modelling question and not a coding error.
25. As a developer, I want the integration comparison between the live Arrivelist and the recomputed one to cover both estimate sets, so that the new path is exercised end to end at no extra cost.

## Implementation Decisions

**Estimate sets become a value object pair.** Introduce a value object holding the three estimates — Fast, Average, Slow, each a nullable Timestamp. The Arrivelist entry replaces its three flat estimate properties with two properties of this type: one for the FEI model and one for the Regional model. This is a breaking change to the entry's public surface; the Arrivelist table component, the unit tests, and the integration assertion helper are its only consumers and all are updated. The entry is a domain type consumed directly through the Arrivelist service — there is no DTO, wire model, or persisted representation — so the reshape has no serialization or migration consequence.

**The FEI path is unchanged.** Cumulative distance over completed Phases, divided by the completed interval taken from the Total value object (ride time plus recovery time, excluding the final Phase's recovery), projected forward to the arrival time at which cumulative speed would equal the target. Min and max average speed come from the Combination as they do now. This code is moved into the new value object shape and otherwise not touched.

**The Regional path is purely per-loop.** For the active Phase:
- Fast = Phase start time + (Phase length / Combination maximum average speed)
- Slow = Phase start time + (Phase length / Combination minimum average speed)
- Average = Phase start time + (Phase length / the last completed Phase's average loop speed)

Completed distance and completed interval are not inputs. Recovery time is not an input. Fast and Slow are null when the corresponding Combination restriction is absent or non-positive, matching the existing guard on the FEI path. Average is null when there is no completed Phase, or when the last completed Phase's loop interval is not positive — the same divide-by-zero guard the FEI average already applies, applied to the loop interval instead of the cumulative interval.

**"Last completed Phase" means the last Phase before the active one that reports itself complete.** The Arrivelist already assembles this collection to compute cumulative distance; the Regional path takes its final element. An incomplete Phase earlier in the ride is skipped, consistent with how the existing cumulative calculation already treats it.

**The domain reads no Ruleset.** The Arrivelist does not consult the Competition's Ruleset, the Phase's Ruleset, or the static region option that gates loop-speed behaviour elsewhere in the domain. Both sets are computed for every Participation regardless of ruleset. This is deliberate: it keeps the value object free of the branch, makes the comparison available at any event, and defers the question of which ruleset value is authoritative — Competition or Phase — to the follow-up change that actually needs an answer.

**Domain ordering is unchanged.** The Arrivelist continues to order entries by the FEI sort estimate (first non-null of Fast, Average, Slow), with null-estimate entries last and ties broken by Combination number.

**Ordering under the Regional selection happens in the component.** When the selector is on Regional, the table re-sorts the entries by the equivalent Regional sort estimate. The selected mode does not flow into the domain — passing it there would force a presentation argument onto a value object that several call sites construct, including an integration assertion that does not care. Consequence to document in code: the domain's entry order and the rendered order differ under the Regional selection. The existing sort-estimate helper is internal to the domain assembly, so the component computes the Regional sort key inline rather than widening domain visibility.

**The selector is three-way and lives on the table component's behind class**, exposed as a property per the project's Blazor rules. It defaults to FEI. State is component-local and resets when the official navigates away and back; this is accepted as a diagnostic control rather than a persisted preference. It is deliberately not placed on the Arrivelist service, which handles socket lifecycle and notification handling and should not acquire view state.

**Both mode stacks two labelled lines per column.** The table keeps its six columns — number, Fast, Average, Slow, athlete, horse. In Both mode each of the three time cells renders two lines, FEI above Regional, each prefixed with its ruleset label. Six separate time columns were rejected: the countdown list stylesheet sets a fixed table layout with horizontal overflow hidden, so additional columns can only squeeze the existing ones, and the comparison reads better vertically within a cell than four columns apart. Row height roughly doubles in Both mode; this is accepted because Both is opt-in.

**Labels name the ruleset, not the arithmetic** — "FEI" and "Regional", matching the enum the eventual decision will be made on. Neither string exists in the localization resources today; both are added across the default, Bulgarian and Turkish resource files, along with the selector's own label.

**Colour coding is untouched.** Fast, Average and Slow keep their existing success, warning and error colours in every mode. Colour continues to encode urgency; the ruleset is encoded by the text label only.

## Testing Decisions

A good test here asserts on the estimates a constructed Arrivelist produces from constructed Participations, and says nothing about how they were produced. It fixes the inputs — Phase lengths, start times, arrive times, present times, Combination speed restrictions — and asserts the resulting Timestamps. It does not reach into private helpers, does not assert on intermediate speeds, and does not know whether the two models share code.

**Unit tests — Arrivelist construction, in the domain unit test namespace.** This is the existing seam and the primary one; prior art is the current Arrivelist test class, whose helpers already build Participations with configurable Phase timings and speed restrictions. All existing tests in it are updated for the reshaped entry and otherwise keep their assertions, which is itself the regression guard on the FEI path. New tests:

- Regional Fast and Slow ignore ride history: a Participation with a completed first loop and an active second loop yields Regional Fast and Slow equal to the active Phase's start plus its own length over the restriction, with no contribution from the completed loop. Asserted on a participation whose completed loop was ridden at a speed that would visibly shift the FEI values, so the test fails if the cumulative terms leak in.
- Regional Average is the last completed loop's speed: same shape, asserting the Average lands where the active loop's length over the previous loop's speed puts it.
- Regional Average is absent without a usable completed loop: no completed Phase yields null; a completed Phase with a zero-length loop interval also yields null.
- First-loop identity: on a Participation with no completed Phase, the Regional and FEI sets are equal. This pins a fact that will otherwise cause confusion in the field — an official looking at a live first loop in Both mode sees two identical lines and has no way to tell whether the Regional path is wired up at all. The test makes "identical here is correct" permanent.

**Integration — the Arrivelist service's entries, in the end-to-end event tests.** The existing assertion that compares the Witness's live Arrivelist against one recomputed from persisted Participations is extended to flatten both estimate sets rather than one. This is a cross-check between two computations of the same thing, not a golden snapshot, so no stored snapshot needs regenerating. Per the project's rules, features should be represented in the integration tests where reasonable; this covers the new path end to end for the cost of extending one string projection.

**No test seam for the selector.** It is component-local state on a Blazor behind class with a single consumer, and the repository has no precedent for rendering tests. Testing it would mean creating a seam rather than using one.

## Out of Scope

- **The Total value object and its average speed.** Total divides total distance by ride time plus recovery time regardless of ruleset, so under Regional rules the total average speed shown on the results screens is computed on the FEI model. This is a real inconsistency and it is knowingly left in place: fixing it changes numbers officials currently read, which this change explicitly refuses to do. It should be revisited when the Arrivelist comparison decides which model wins.
- **Phase-level average speed.** The Phase's ruleset-aware average speed and the static region option that gates it are correct and untouched. This spec does not change how any Phase, loop or total interval is calculated or displayed.
- **Speed restriction enforcement.** The domain declares a failed-to-qualify reason for speed restriction but never raises it — a violated minimum or maximum average speed currently eliminates nobody, and the constant is read only by the restore path. Wiring enforcement is a substantially larger change: it needs a decision about when a minimum-speed breach is even knowable, since a rider can make up time until the finish, and it changes event outcomes rather than a display. Out of scope here and worth its own spec once the speed model is settled.
- **Any ruleset-driven behaviour in the Arrivelist**, including deriving the selector's default from the event's competitions. There is no event-level ruleset — Ruleset lives on Competition only — so a mixed event has no single answer, and any derived default risks showing a Regional event nothing but never-validated numbers.
- **Persisting the selector across navigation or sessions.**
- **Ranking, results documents and the FEI export**, none of which consume Arrivelist estimates.

## Further Notes

**The two models are identical on the first loop.** With no completed Phase the FEI formula reduces to start plus length over speed, which is the Regional formula. Divergence begins at the second loop and grows with each subsequent one. Anyone validating this on a real event needs a multi-loop competition; a single-Phase event will show nothing.

**Direction of divergence.** Because the FEI model's denominator includes vet-gate recovery while the Regional model's excludes it, and because the FEI model carries banked time forward, the FEI Fast deadline drifts later than the Regional one for a rider who has been slow, and earlier for one who has been fast. That drift is the whole point of the comparison and should not be treated as a bug when first observed.

**Why the Average column changed meaning under Regional.** It was considered whether the Regional Average should be a cumulative ride-only average — total loop distance over total loop time, recoveries excluded — rather than the last loop's speed. The last loop was chosen so that the entire Regional column family is loop-scoped and consistent with the per-loop restrictions beside it. The known cost is volatility: one anomalous loop, a long walk-in or a lost shoe, swings the Average hard, and the basis re-bases at every gate. If the column proves too jumpy in the field, cumulative ride-only is the alternative and it is a contained change to a single calculation.

**Intended follow-up.** Once live comparison settles which model the Regional ruleset should use, the selector and the losing estimate set are deleted, and the surviving set is chosen per Participation from the Competition's Ruleset — routed through the same static region option the Phase already uses, so that the Arrivelist and the Phase never disagree for a future non-Bulgarian region. That follow-up should also revisit the Total average speed inconsistency noted above, since the Arrivelist and the results screens would otherwise continue to describe the same rider by different rules.
