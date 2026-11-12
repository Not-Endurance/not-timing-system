// The connection string comes from the environment: MONGO_CONNECTION_STRING, the same setting the app reads.
// CD sets it from the environment's GitHub secret. Never print it here.
const connectionString = process.env.MONGO_CONNECTION_STRING;

if (!connectionString) {
  throw new Error("MONGO_CONNECTION_STRING is not set. Migrations need the target environment's connection string.");
}

module.exports = {
  mongodb: {
    url: connectionString,
    databaseName: "nts",
    options: {},
  },
  migrationsDir: "migrations",
  changelogCollectionName: "changelog",
  migrationFileExtension: ".js",
  useFileHash: false,
  moduleSystem: "commonjs",
};
