// Moves stored data to the shape the code now reads:
//
// - Speed limits: the old min and max values become the phase average limit (PhaseAverageSpeed) in a Restrictions
//   object. Competition, participation and combination values are all converted the same way, so live Arrivelist
//   output keeps its meaning.
// - Ruleset is removed. Each competition, ranking and snapshot gets EnglishNames: true for FEI, false otherwise.
// - Past events are finalized when every entry already has a stored placing. The rest are finalized by the finalization
//   job (03:00 and 15:00 UTC) once they are placed, so nothing is placed here with the old rules.
// - Every persisted document gets DomainVersion. The code refuses to load a document without the current version.
// - User sessions lose their Ruleset. They have no domain class, so they are only handled here.
//
// The migration stops if any event is live, because live events would be written against the new rules mid-event.
// Set MIGRATION_DRY_RUN=1 to count what would change without writing anything.
//
// Keep DOMAIN_VERSION in step with the entity default in NC.Domain.Entity and the DTOs' DomainVersion defaults.

const DOMAIN_VERSION = 1;
const DRY_RUN = process.env.MIGRATION_DRY_RUN === "1";

const EVENT_COLLECTION = "event_informations";
const STORED_COLLECTIONS = [
  "configure_events",
  EVENT_COLLECTION,
  "event_participations",
  "event_rankings",
  "event_handouts",
  "event_officials",
  "event_operators",
  "countries",
  "users",
  "settings",
];

// The documents that carry a DomainVersion, recognised by their stored fields. Athlete, horse and club are embedded
// and are not versioned. Keep in step with the DTOs in NoTiming.Application.Contracts that declare DomainVersion.
function persistedKind(o) {
  const has = (...keys) => keys.every((key) => Object.prototype.hasOwnProperty.call(o, key));
  const count = Object.keys(o).length;
  if (has("IsoCode", "NfCode")) return "Country";
  if (has("Distance") && count <= 3 && !has("Phases", "Gate", "Loop")) return "Loop";
  if (has("Loop", "Recovery")) return "SetupPhase";
  if (has("Gate", "MaxRecovery")) return "Phase";
  if (has("Entries", "EventId", "Category")) return "Ranking";
  if (has("Participation", "Rank", "IsNotRanked")) return "RankingEntry";
  if (has("Competitions", "Loops")) return "ConfigureEvent";
  if (has("EnglishNames", "Restrictions", "Phases")) return "SetupCompetition";
  if (has("Competition", "Phases", "Combination")) return "Participation";
  if (has("IsNotRanked", "Combination", "StartTimeOverride", "Restrictions")) return "SetupParticipation";
  if (has("Athlete", "Horse", "Restrictions", "Number")) return "Combination";
  if (has("StartDay", "EndDay", "IsActive")) return "EventInformation";
  if (has("Role") && (has("Names") || has("Name")) && (has("User") || has("UserId"))) return "Official";
  if (has("Role", "User")) return "Operator";
  if (has("EventId", "UserId", "Role") && count <= 6) return "Operator";
  if (has("Email", "Roles")) return "User";
  if (has("DetectionMode")) return "Setting";
  if (has("Participation", "EventId") && !has("Rank")) return "Handout";
  return null;
}

// Copies stored values. Dates are copied; other objects, such as ObjectIds, are kept as they are.
function clone(value) {
  if (Array.isArray(value)) {
    return value.map(clone);
  }
  if (value instanceof Date) {
    return new Date(value.getTime());
  }
  if (value !== null && typeof value === "object" && Object.getPrototypeOf(value) === Object.prototype) {
    const copy = {};
    for (const key of Object.keys(value)) {
      copy[key] = clone(value[key]);
    }
    return copy;
  }
  return value;
}

function limits(min, max) {
  return {
    LoopSpeedMin: null,
    LoopSpeedMax: null,
    PhaseSpeedMin: null,
    PhaseSpeedMax: null,
    LoopAverageSpeedMin: null,
    LoopAverageSpeedMax: null,
    PhaseAverageSpeedMin: min ?? null,
    PhaseAverageSpeedMax: max ?? null,
  };
}

// Converts a stored document in place, including every embedded document, and returns it.
function convert(value) {
  if (Array.isArray(value)) {
    value.forEach(convert);
    return value;
  }
  if (value === null || typeof value !== "object") {
    return value;
  }

  if ("MinAverageSpeed" in value || "MaxAverageSpeed" in value) {
    value.Restrictions = limits(value.MinAverageSpeed, value.MaxAverageSpeed);
    delete value.MinAverageSpeed;
    delete value.MaxAverageSpeed;
  }
  if ("MinSpeedRestriction" in value || "MaxSpeedRestriction" in value) {
    value.Restrictions = limits(value.MinSpeedRestriction, value.MaxSpeedRestriction);
    delete value.MinSpeedRestriction;
    delete value.MaxSpeedRestriction;
  }
  if ("MinSpeedOverride" in value || "MaxSpeedOverride" in value) {
    value.Restrictions = limits(value.MinSpeedOverride, value.MaxSpeedOverride);
    delete value.MinSpeedOverride;
    delete value.MaxSpeedOverride;
  }

  if ("Ruleset" in value) {
    const kind = persistedKind(value);
    if (kind !== "Phase" && kind !== "Handout") {
      value.EnglishNames = value.Ruleset === "FEI";
    }
    delete value.Ruleset;
  }

  if (persistedKind(value) !== null) {
    value.DomainVersion = DOMAIN_VERSION;
  }

  for (const key of Object.keys(value)) {
    convert(value[key]);
  }
  return value;
}

async function ensureNoLiveEvent(db, now) {
  const live = await db.collection(EVENT_COLLECTION).findOne({ IsActive: true, EndDay: { $gt: now } });
  if (live) {
    throw new Error(
      `Event ${live._id} is live. Deactivate it or wait until it ends, then run the migration again. Nothing was changed.`
    );
  }
}

async function writeChanged(collection, documents, label, counts) {
  let changed = 0;
  for (const { original, converted } of documents) {
    if (JSON.stringify(original) === JSON.stringify(converted)) {
      continue;
    }
    changed++;
    if (!DRY_RUN) {
      await collection.replaceOne({ _id: original._id }, converted);
    }
  }
  counts.push(`${label}: ${changed} of ${documents.length} changed`);
}

module.exports = {
  async up(db) {
    const now = new Date();
    const counts = [];
    await ensureNoLiveEvent(db, now);

    // Rankings first, so that event finalization can see their placings.
    const rankings = db.collection("event_rankings");
    const storedRankings = await rankings.find({}).toArray();
    const rankingDocs = storedRankings.map((original) => ({
      original,
      converted: convert(clone(original)),
    }));
    const unplacedEvents = new Set(
      storedRankings
        .filter((ranking) => (ranking.Entries ?? []).some((entry) => entry.Rank === undefined || entry.Rank === null))
        .map((ranking) => ranking.EventId)
    );

    for (const name of STORED_COLLECTIONS) {
      if (name === EVENT_COLLECTION || name === "event_rankings") {
        continue;
      }
      const collection = db.collection(name);
      const documents = await collection.find({}).toArray();
      await writeChanged(
        collection,
        documents.map((original) => ({ original, converted: convert(clone(original)) })),
        name,
        counts
      );
    }
    await writeChanged(rankings, rankingDocs, "event_rankings", counts);

    const events = db.collection(EVENT_COLLECTION);
    const storedEvents = await events.find({}).toArray();
    const eventDocs = storedEvents.map((original) => {
      const converted = convert(clone(original));
      const ended = new Date(original.EndDay) < now;
      converted.IsFinalized = ended && !unplacedEvents.has(original._id);
      return { original, converted };
    });
    const finalized = eventDocs.filter((doc) => doc.converted.IsFinalized).length;
    await writeChanged(events, eventDocs, `${EVENT_COLLECTION} (finalized ${finalized})`, counts);

    const sessions = db.collection("event_user_sessions");
    const sessionCount = await sessions.countDocuments({ Ruleset: { $exists: true } });
    if (!DRY_RUN && sessionCount > 0) {
      await sessions.updateMany({ Ruleset: { $exists: true } }, { $unset: { Ruleset: "" } });
    }
    counts.push(`event_user_sessions: ${sessionCount} without Ruleset`);

    console.log(`${DRY_RUN ? "Dry run, nothing written. " : ""}${counts.join("; ")}`);
    if (DRY_RUN) {
      // Failing on purpose keeps migrate-mongo from recording the migration as applied.
      throw new Error("Dry run finished and wrote nothing. Run again without MIGRATION_DRY_RUN to apply it.");
    }
  },

  async down() {
    throw new Error("This migration can't be reversed. Restore the database from the backup taken before it ran.");
  },
};
