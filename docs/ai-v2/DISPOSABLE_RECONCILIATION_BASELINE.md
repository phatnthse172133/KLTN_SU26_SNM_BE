# Disposable PostgreSQL reconciliation baseline

This is the verified Sub-phase D integration dataset, not the developer's
current database. The test creates and drops a fresh database whose name starts
with `snm_aiv2_backfill_`; no local application database is updated.

## Legacy input

- 13 tags: 0 system, 13 non-system, 12 active, 1 inactive/archived.
- 38 `FoodItemTag` relations.
- 3 `CustomerPreference` relations.
- 41/41 relations covered by a migration/derive/archive/manual disposition.
- 12 exact SOUP rows: 2 manual review, 10 not applicable, 0 auto-approved.

## Dry-run and execute

Dry-run and execute produced the same write plan:

- 77 catalog rows.
- 12 food metadata relations.
- 2 food scalar updates.
- 1 customer profile and 3 customer relations.
- 12 `FoodAiProfile` rows; embeddings remained null.
- 0 unmapped relations; 2 ambiguous/manual relations.

Final normalized counts were 30 ingredients, 13 dietary attributes, 15
preparation methods, 7 taste profiles, 12 search facets, 0 allergens, 3 food
ingredients, 1 food preparation method, 8 food courses, 3 customer relations
and 12 AI search profiles. Legacy relation counts remained 38 and 3.

The execute report set `TransactionCommitted=true`. A second execute with a
different batch size planned zero writes in every category, proving safe rerun
and duplicate prevention on PostgreSQL.
