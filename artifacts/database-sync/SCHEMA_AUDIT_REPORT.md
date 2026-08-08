# SmartNightMarket schema audit report

## Scope and safety rules

The audit was performed against a verified backup and an isolated restored clone of the
`SmartNightMarket` development database. No development database reset, `EnsureDeleted`, history
rewrite, or migration removal was used.

The development backup is stored outside the Git repository under `.database-sync` and must remain
local. Only schema-only and migration artifacts are included in this directory.

## Baseline findings

- Source and development database initially contained the same 71 migration IDs.
- Twenty-three migration roots existed locally but were not tracked by Git; the development database
  already recorded all of them as applied. They must be reviewed and committed as one canonical chain.
- The EF model snapshot had drift caused by missing model metadata and duplicate entity configuration.
- `UserStatusHistory` and `EmailOutbox` were configured twice in `SNMDbContext`; the duplicate blocks
  were removed without changing their intended schema.

## Canonical reconciliation migrations

1. `20260803072406_ReconcileDevelopmentSchema`
   - Intentionally executes no DDL.
   - Records the reconciled model snapshot after comparing the existing database and a fresh schema.
2. `20260803073753_ReconcileModerationSchema`
   - Guards against null moderation-history values before enforcing required columns.
   - Normalizes the primary-key name when necessary.
   - Creates `idx_nightmarket_moderation_status` only if missing.
3. `20260803073914_RemoveConfirmedLegacyNightMarketColumns`
   - Guards all six legacy NightMarket columns against data loss.
   - Removes `ModerationNotes`, `RejectedReason`, `HasLiveStream`, `LiveStreamUrl`,
     `LiveStreamStartedAt`, and `LiveStreamEndedAt` only when their data is empty/default.

No table was dropped. Empty-table counts were not treated as proof that a table is unused.

## Published-chain compatibility exceptions

Two older migration source files contain documented compatibility corrections because the original
chain could not safely migrate real/fresh data without them:

- `20260712101701_EditForPaymentAndOrder`: replaces non-numeric order codes with unique numeric values
  before converting to bigint, avoiding collisions and data loss.
- `20260717113421_AddAdminUserStatusManagement`: uses guarded table/index creation because an earlier
  migration already creates `EmailOutbox` and `UserStatusHistories` in some histories.

These are explicit, hash-manifested exceptions. Team members must use the exact files in
`SmartNightMarket_migration_hashes.sha256`; do not independently edit them.

Two manual migrations (`AddPhysicalLayoutGeometry` and `AddSupportTickets`) intentionally keep their
`DbContext` and `Migration` attributes in the main migration file, so EF discovers them without a
separate designer file.

## Verification evidence

- Fresh database migrated from empty to 78/78, including all four AI V2 migrations from `dev`: passed.
- Restored development clone migrated from the reconciled 74-migration state to 78: passed.
- Repeat migration on both databases: no migrations applied.
- Clone users before/after: 126/126.
- Business row-count changes (excluding migration history): 0.
- Fresh versus migrated-clone semantic column differences: 0.
- Fresh versus migrated-clone index differences: 0.
- Fresh versus migrated-clone constraint differences: 0.
- PresentationLayer build: 0 errors, 0 warnings.
- AI V2 backfill tool build: 0 errors, 0 warnings.
- EF model drift check: none.
- The `dev` test project currently has two pre-existing compile errors because
  `AdminNotificationServiceTests` and `PostgresCustomerHistoryReviewComplaintVerificationTests`
  construct `NotificationService` without its newly required logger dependency. Test files were not
  changed or committed in this database-sync task, as requested.

## Development apply gate

The exact development clone passed the combined chain without data loss. The real development
database was then advanced to all 78 migrations while the API was stopped. Post-apply checks confirmed:

- final migration count: 78;
- user count: 126 (unchanged);
- business row-count differences: 0;
- legacy NightMarket columns remaining: 0.

Production deployment still requires its own backup and staging rehearsal; development verification
must not be treated as authorization to update production automatically.
