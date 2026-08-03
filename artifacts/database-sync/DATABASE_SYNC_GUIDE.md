# SmartNightMarket database synchronization guide

This package is the canonical database synchronization bundle for the current backend model.
It contains schema artifacts only. It does **not** contain development data, user accounts,
credentials, PayOS keys, AI secrets, or Cloudinary secrets.

## Canonical state

- Canonical migration count: **74**
- Canonical latest migration: `20260803073914_RemoveConfirmedLegacyNightMarketColumns`
- EF model drift: **none** (`dotnet ef migrations has-pending-model-changes` exits successfully)
- PostgreSQL fresh-database migration: **passed**
- Restored development-clone migration: **passed**
- Idempotency: a second update reports `No migrations were applied`
- Fresh and migrated-clone catalogs match for columns, indexes, and constraints
- Development-clone business row counts were unchanged; 126 users were preserved
- Development database applied to 74 migrations; 126 users and all business row counts were preserved

## Files in this directory

- `SmartNightMarket_schema_idempotent.sql`: canonical idempotent migration script.
- `SmartNightMarket_schema_only.sql`: schema exported from a fresh database after all 74 migrations.
- `SmartNightMarket_migration_history.txt`: ordered migration IDs.
- `SmartNightMarket_migration_hashes.sha256`: SHA-256 hashes of migration and snapshot source files.
- `SmartNightMarket_columns.csv`: canonical public columns.
- `SmartNightMarket_indexes.csv`: canonical public indexes.
- `SmartNightMarket_constraints.csv`: canonical public constraints.
- `SCHEMA_AUDIT_REPORT.md`: audit decisions, compatibility exceptions, and verification evidence.

## New team member or empty database

1. Pull the exact backend revision containing all migration files in the manifest.
2. Configure `ConnectionStrings:DefaultConnection` through local secrets or environment variables.
3. Confirm the target database is empty and is not a shared development/production database.
4. Run:

   ```powershell
   dotnet build PresentationLayer/PresentationLayer.csproj
   dotnet ef database update `
     --project InfrastructureLayer/InfrastructureLayer.csproj `
     --startup-project PresentationLayer/PresentationLayer.csproj
   ```

5. Verify the final migration:

   ```sql
   SELECT "MigrationId"
   FROM "__EFMigrationsHistory"
   ORDER BY "MigrationId" DESC
   LIMIT 1;
   ```

   Expected: `20260803073914_RemoveConfirmedLegacyNightMarketColumns`.

## Existing team database

1. Stop every API instance that writes to the target database.
2. Create and verify a PostgreSQL custom-format backup.
3. Compare `__EFMigrationsHistory` with `SmartNightMarket_migration_history.txt`.
4. If the database contains migration IDs not present in the manifest, **stop** and investigate.
5. Apply the canonical idempotent script using `psql` with `ON_ERROR_STOP`:

   ```powershell
   psql -v ON_ERROR_STOP=1 -d <database_name> `
     -f artifacts/database-sync/SmartNightMarket_schema_idempotent.sql
   ```

6. Run the same command again. The second execution must make no schema changes.
7. Re-run the final-migration query above.

The reference development database completed this exact process on 2026-08-03. Its migration count
changed from 72 to 74, its user count remained 126, and its business row-count diff was zero.

Do not use `EnsureCreated`, delete `__EFMigrationsHistory`, drop the database, or copy a developer's
data dump to another machine as a schema synchronization method.

## Guarded cleanup behavior

`RemoveConfirmedLegacyNightMarketColumns` removes six obsolete `NightMarket` columns only after
checking that no legacy data is present. If any value exists, the migration raises an exception and
stops before dropping the columns. The correct response is to inspect and migrate that data; never
delete it just to make the migration pass.

## Seeds and sample accounts

Migrations synchronize schema, not the 126 development users. Deterministic seeders must be run
separately and must be idempotent. Never put real user data, passwords, payment credentials, or API
secrets in a migration or committed SQL file.

## Troubleshooting

- **History differs:** stop; do not force-insert migration history rows.
- **Cleanup guard fails:** back up and inspect legacy column values before deciding a conversion.
- **Pending model changes:** generate and review a new forward-only migration; do not edit published
  migration IDs silently.
- **A table appears unused:** do not drop it from row count alone. Require zero source references,
  ownership confirmation, data classification, backup, and a guarded forward migration.
- **Integration test creates an Order without BoothId:** this is stale test setup against the current
  required Order schema, not a reason to modify or weaken the production foreign key.
