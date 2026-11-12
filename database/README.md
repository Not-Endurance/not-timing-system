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

Each migration exports `up(db)` and `down(db)`. A migration that changes stored data ships in the same change as the code that reads the new shape, and it runs before that code deploys. There is no version check yet: nothing stops old code from reading migrated data, which is why the deploy window is accepted.

## Running against a database locally

Set `MONGO_CONNECTION_STRING` to the target connection string in your shell for this session, then run:

```bash
npm run migrate:status --prefix database
npm run migrate:up --prefix database
```

Don't point local runs at production unless you mean to change production.
