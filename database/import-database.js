// import-database.js
//
// Imports every <name>.json file in IMPORT_DIR into collection <name> of DB_NAME,
// creating any collection that does not exist yet. The files are the JSON arrays
// written by export-collections.js (Extended JSON, so BSON types are restored).
//
// Documents are inserted, not upserted: nothing is dropped or overwritten. A
// document whose _id already exists in the target collection is counted as a
// duplicate and skipped, so re-running the import is safe.
//
// Usage:
//   mongosh "mongodb://localhost:27017" --file import-database.js <DB_NAME> [IMPORT_DIR]
//
// Inputs (positional, after the script name):
//   DB_NAME      (required) target database; created by MongoDB on first write if missing
//   IMPORT_DIR   (optional) directory containing the .json files, default ./../tmp

const SCRIPT_NAME = "import-database.js";
const BATCH_SIZE = 1000;

// Returns the arguments that follow this script's name in process.argv.
function scriptArgs() {
  const argv = typeof process !== "undefined" && Array.isArray(process.argv) ? process.argv : [];
  const at = argv.findIndex((a) => String(a).split(/[\\/]/).pop() === SCRIPT_NAME);
  if (at === -1) {
    throw new Error(
      `Could not find ${SCRIPT_NAME} in process.argv, so the inputs cannot be read. ` +
        `process.argv was: ${JSON.stringify(argv)}`
    );
  }
  return argv.slice(at + 1);
}

const [DB_NAME, IMPORT_DIR = "./../tmp"] = scriptArgs();

if (!DB_NAME) {
  throw new Error(`Usage: mongosh <uri> --file ${SCRIPT_NAME} <DB_NAME> [IMPORT_DIR]`);
}

const fs = require("fs");

if (!fs.existsSync(IMPORT_DIR) || !fs.statSync(IMPORT_DIR).isDirectory()) {
  throw new Error(`IMPORT_DIR is not a directory: ${IMPORT_DIR}`);
}

const files = fs
  .readdirSync(IMPORT_DIR)
  .filter((f) => f.endsWith(".json"))
  .sort();

if (files.length === 0) {
  throw new Error(`No .json files found in ${IMPORT_DIR}`);
}

const target = db.getSiblingDB(DB_NAME);
const existing = new Set(target.getCollectionNames());

const totals = { inserted: 0, duplicates: 0, failed: 0 };

// Returns the write errors from a bulk insert, or null if the error is not a write error
// (network, auth, etc.). The driver may report one error as an object rather than an array.
function writeErrorsOf(e) {
  if (Array.isArray(e.writeErrors)) return e.writeErrors;
  if (e.writeErrors) return [e.writeErrors];
  return null;
}

for (const file of files) {
  const name = file.slice(0, -".json".length);
  const path = `${IMPORT_DIR}/${file}`;

  const docs = EJSON.parse(fs.readFileSync(path, "utf8"));
  if (!Array.isArray(docs)) {
    print(`SKIPPED ${file}: expected a JSON array of documents`);
    continue;
  }

  if (!existing.has(name)) {
    target.createCollection(name);
    existing.add(name);
    print(`Created collection ${DB_NAME}.${name}`);
  }

  const coll = target.getCollection(name);
  const stats = { inserted: 0, duplicates: 0, failed: 0 };

  for (let i = 0; i < docs.length; i += BATCH_SIZE) {
    const batch = docs.slice(i, i + BATCH_SIZE);
    try {
      coll.insertMany(batch, { ordered: false });
      stats.inserted += batch.length;
    } catch (e) {
      const errors = writeErrorsOf(e);
      if (!errors) throw e;

      for (const we of errors) {
        if (we.code === 11000) {
          stats.duplicates++;
        } else {
          stats.failed++;
          print(`  ${name}: ${we.errmsg || we.message}`);
        }
      }
      stats.inserted += batch.length - errors.length;
    }
  }

  totals.inserted += stats.inserted;
  totals.duplicates += stats.duplicates;
  totals.failed += stats.failed;

  print(
    `${DB_NAME}.${name}: ${stats.inserted} inserted, ${stats.duplicates} duplicate _id(s) skipped, ${stats.failed} failed`
  );
}

print(
  `Done: ${files.length} file(s) processed. ${totals.inserted} inserted, ${totals.duplicates} duplicate(s) skipped, ${totals.failed} failed.`
);