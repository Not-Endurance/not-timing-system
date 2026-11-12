// Stores the time zone of every event and recomputes its span in that zone.
//
// Events used to end at 23:59:59 in whatever offset the browser that created them had,
// so the stored EndDay could fall on a different day in the event's own zone. This sets
// TimeZoneId from the country and moves StartDay and EndDay to local midnight and local
// 23:59:59 in that zone. The date is read from the stored instant in the event's zone,
// which matches the operator's clock for events run in their own country.
//
// Keep TIME_ZONES in step with EventTimeZone.ForCountry in NoTiming.Domain.Core.

const COLLECTION = "event_informations";

const TIME_ZONES = {
  BG: "Europe/Sofia",
  BGR: "Europe/Sofia",
  TR: "Europe/Istanbul",
  TUR: "Europe/Istanbul",
};

function localParts(instant, timeZoneId) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: timeZoneId,
    hourCycle: "h23",
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "numeric",
    second: "numeric",
  }).formatToParts(instant);
  const value = (type) => Number(parts.find((part) => part.type === type).value);
  return {
    year: value("year"),
    month: value("month"),
    day: value("day"),
    hour: value("hour"),
    minute: value("minute"),
    second: value("second"),
  };
}

// The zone's offset from UTC at an instant, in milliseconds.
function offsetAt(instant, timeZoneId) {
  const p = localParts(instant, timeZoneId);
  const wallClockAsUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second);
  const wholeSeconds = instant.getTime() - (instant.getTime() % 1000);
  return wallClockAsUtc - wholeSeconds;
}

// The instant at which the zone's wall clock shows the given local date and time.
function atLocalTime(date, time, timeZoneId) {
  const wallClockAsUtc = Date.UTC(date.year, date.month - 1, date.day, time.hour, time.minute, time.second);
  return new Date(wallClockAsUtc - offsetAt(new Date(wallClockAsUtc), timeZoneId));
}

function startOfDay(instant, timeZoneId) {
  const { year, month, day } = localParts(instant, timeZoneId);
  return atLocalTime({ year, month, day }, { hour: 0, minute: 0, second: 0 }, timeZoneId);
}

function endOfDay(instant, timeZoneId) {
  const { year, month, day } = localParts(instant, timeZoneId);
  return atLocalTime({ year, month, day }, { hour: 23, minute: 59, second: 59 }, timeZoneId);
}

module.exports = {
  async up(db) {
    const events = db.collection(COLLECTION);
    const pending = await events.find({ TimeZoneId: { $exists: false } }).toArray();

    for (const event of pending) {
      const isoCode = event.Country?.IsoCode;
      const timeZoneId = TIME_ZONES[isoCode];
      if (!timeZoneId) {
        throw new Error(
          `Event ${event._id} has country ${isoCode}, which has no time zone. Add it to TIME_ZONES before migrating.`
        );
      }

      await events.updateOne(
        { _id: event._id },
        {
          $set: {
            TimeZoneId: timeZoneId,
            StartDay: startOfDay(event.StartDay, timeZoneId),
            EndDay: endOfDay(event.EndDay, timeZoneId),
          },
        }
      );
    }
  },

  // Removes the zone only. The recomputed dates stay, since the old values can't be restored.
  async down(db) {
    await db.collection(COLLECTION).updateMany({}, { $unset: { TimeZoneId: "" } });
  },
};
