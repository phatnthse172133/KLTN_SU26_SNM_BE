# AI V2 additive backfill runbook

The backfill is never registered in application startup. It can run only through
the explicit `AiV2BackfillTool` command or by directly invoking the service in a
controlled test/admin process.

## Safety gates

1. Create a fresh backup or a disposable PostgreSQL database.
2. Apply EF migrations explicitly and verify there are no pending migrations.
3. Put the connection string in a process environment variable. Do not pass it
   on the command line or commit it to configuration.
4. Run dry-run and review both JSON (stdout) and Markdown (stderr) reports.
5. Execute with the exact database-name allow-list and confirmation flag.
6. Run dry-run again; every planned-write count must be zero.

```powershell
$env:SNM_AI_V2_BACKFILL_CONNECTION = '<connection string for disposable database>'
dotnet run --project AiV2BackfillTool -- --dry-run --allow-database '<database>' --batch-size 200 --json-out report.dry.json --markdown-out report.dry.md
dotnet run --project AiV2BackfillTool -- --execute --confirm-execute --allow-database '<database>' --batch-size 200 --json-out report.execute.json --markdown-out report.execute.md
dotnet run --project AiV2BackfillTool -- --dry-run --allow-database '<database>' --batch-size 200 --json-out report.rerun.json
```

The command refuses to run when the environment variable is absent, the
database name differs from `--allow-database`, execute confirmation is absent,
or EF reports pending migrations. Exceptions are not swallowed. Execute mode
uses a serializable transaction; catalog and relation batches are committed
together or rolled back together.

## Reconciliation gates

- `CoveredLegacyRelations` equals `FoodItemTagBefore + CustomerPreferenceBefore`.
- `LegacyDataChanged` is false.
- Unknown codes appear as `UNMAPPED_TAG` / `REJECT_WITH_REASON`.
- `SOUP_MANUAL_REVIEW` remains approved exception data.
- `OTHER_QUICK_SERVE` is `ARCHIVE`, absent from SearchText and scoring evidence.
- Allergen rows are never derived from dietary tags.
- Rerun planned-write counts are all zero.

