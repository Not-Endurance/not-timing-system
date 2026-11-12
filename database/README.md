# NTS database migrations

MongoDB migrations for the `nts` database, run with [migrate-mongo](https://github.com/seppevs/migrate-mongo) (pinned to 14.0.7).

## How they run

- The CD workflows (`.github/workflows/nts-cd-staging.yaml` and `nts-cd-production.yaml`) run `npm run migrate:up` in a `migrate-mongo` job. The deploy job waits for it, so new code is never deployed ahead of its data.
- Between the migration step and the Web App swap, the previous version can still be serving requests against migrated data. We accept failures in that window; the workflows don't stop the app.
- The `migrate-mongo` job runs in the `staging` or `production` GitHub environment and reads `MONGO_CONNECTION_STRING` from that environment's secrets. Both environments define the same secret name with their own value. A missing secret fails the job before any migration runs.
- Applied migrations are recorded in the `changelog` collection. `migrate:up` skips anything already applied, so running it on every deploy is safe.

## Adding a migration

```bash
npm ci --prefix database
npx --prefix database migrate-mongo create <short-description>
```

Each migration exports `up(db)` and `down(db)`. A migration that changes stored data ships in the same change as the code that reads the new shape, and it runs before that code deploys.

Every persisted document carries a `DomainVersion`. The code refuses to load a document whose version is missing or different, so old code can't read migrated data and new code can't read unmigrated data. A migration that changes a persisted shape must stamp the new version on every document it touches.

## The speed grid and finalization migration

`20261112120000-speed-grid-finalization-domain-version.js` does four things:

- Converts the old min and max speed values to the phase average limit, and replaces `Ruleset` with `EnglishNames`.
- Stamps `DomainVersion` on every persisted document, including embedded ones.
- Finalizes past events whose rankings already have every placing stored. Other past events are finalized by the nightly job once it has placed them.
- Refuses to run while an event is live. Deactivate the event or wait until it ends first.

Before production, take a backup and run the migration against a copy of it first. Set MIGRATION_DRY_RUN=1 to count what would change. The run then fails on purpose, so migrate-mongo doesn't record the migration as applied:

```bash
MIGRATION_DRY_RUN=1 npm run migrate:up --prefix database
```

## Running against a database locally

Set `MONGO_CONNECTION_STRING` to the target connection string in your shell for this session, then run:

```bash
npm run migrate:status --prefix database
npm run migrate:up --prefix database
```

Don't point local runs at production unless you mean to change production.
