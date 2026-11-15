// export-database.js
//
// Exports every collection in one database to <OUT_DIR>/<collection>.json.
// Each file is a JSON array of documents in canonical Extended JSON, so BSON
// types (ObjectId, Date, Decimal128, Long, Int32, Double, ...) survive a round
// trip through import-collections.js.
//
// Usage:
//   mongosh "mongodb://localhost:27017" --file export-collections.js <DB_NAME> [OUT_DIR]
//
// Inputs (positional, after the script name):
//   DB_NAME   (required) database to export
//   OUT_DIR   (optional) output directory, default ./tmp

const SCRIPT_NAME = "export-database.js";

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

const [DB_NAME, OUT_DIR = "./../tmp"] = scriptArgs();

if (!DB_NAME) {
  throw new Error(`Usage: mongosh <uri> --file ${SCRIPT_NAME} <DB_NAME> [OUT_DIR]`);
}

const fs = require("fs");
const source = db.getSiblingDB(DB_NAME);

// Views hold no documents of their own, and system.* collections are internal.
const collections = source
  .getCollectionInfos()
  .filter((info) => info.type !== "view" && !info.name.startsWith("system."));

fs.mkdirSync(OUT_DIR, { recursive: true });

// Streams documents to disk one at a time, so large collections are not held in memory.
function exportCollection(name) {
  const file = `${OUT_DIR}/${name}.json`;
  const cursor = source.getCollection(name).find({}).sort({ _id: 1 });
  const fd = fs.openSync(file, "w");
  let count = 0;
  try {
    fs.writeSync(fd, "[\n");
    while (cursor.hasNext()) {
      const doc = cursor.next();
      const separator = count === 0 ? "" : ",\n";
      fs.writeSync(fd, separator + EJSON.stringify(doc, null, 2, { relaxed: false }));
      count++;
    }
    fs.writeSync(fd, count === 0 ? "]\n" : "\n]\n");
  } finally {
    fs.closeSync(fd);
  }
  return count;
}

let totalDocs = 0;
let exported = 0;

for (const info of collections) {
  const name = info.name;

  if (/[\\/]/.test(name)) {
    print(`SKIPPED ${name}: collection name contains a path separator`);
    continue;
  }

  const count = exportCollection(name);
  totalDocs += count;
  exported++;
  print(`Exported ${count} document(s) from ${DB_NAME}.${name} -> ${OUT_DIR}/${name}.json`);
}

print(`Done: ${exported} collection(s), ${totalDocs} document(s) written to ${OUT_DIR}`);