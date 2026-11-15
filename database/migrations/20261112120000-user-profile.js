// Moves the profile fields users had at the top level of their row into the profile document:
// Club to profile.club, FeiId to profile.feiId and CountryRegion to profile.countryIsoCode.
//
// Each row changes in one pipeline update, so a profile saved while this runs is never overwritten. A value already
// in the profile is kept. Only rows that still have a legacy field are matched, so a second run changes nothing.
//
// Keep the field names in step with ProfileDocument in NC.Server.

const COLLECTION = "users";
const LEGACY_FIELDS = ["Club", "FeiId", "CountryRegion"];

// The value already in the profile, or else the legacy top-level field, or nothing when neither exists.
function keepOr(profilePath, legacyField) {
  return {
    $cond: [
      { $ne: [{ $type: `$${profilePath}` }, "missing"] },
      `$${profilePath}`,
      { $ifNull: [`$${legacyField}`, "$$REMOVE"] },
    ],
  };
}

module.exports = {
  async up(db) {
    await db.collection(COLLECTION).updateMany(
      { $or: LEGACY_FIELDS.map((field) => ({ [field]: { $exists: true } })) },
      [
        // A profile that is not a document is replaced by an empty one.
        { $set: { profile: { $cond: [{ $eq: [{ $type: "$profile" }, "object"] }, "$profile", {}] } } },
        {
          $set: {
            "profile.club": keepOr("profile.club", "Club"),
            "profile.feiId": keepOr("profile.feiId", "FeiId"),
            "profile.countryIsoCode": keepOr("profile.countryIsoCode", "CountryRegion"),
          },
        },
        { $unset: LEGACY_FIELDS },
      ]
    );
  },

  // Moves the three fields back to the top level. Other fields in the profile stay where they are.
  async down(db) {
    await db.collection(COLLECTION).updateMany(
      {
        $or: [
          { "profile.club": { $exists: true } },
          { "profile.feiId": { $exists: true } },
          { "profile.countryIsoCode": { $exists: true } },
        ],
      },
      [
        {
          $set: {
            Club: { $ifNull: ["$profile.club", "$$REMOVE"] },
            FeiId: { $ifNull: ["$profile.feiId", "$$REMOVE"] },
            CountryRegion: { $ifNull: ["$profile.countryIsoCode", "$$REMOVE"] },
          },
        },
        { $unset: ["profile.club", "profile.feiId", "profile.countryIsoCode"] },
      ]
    );
  },
};
