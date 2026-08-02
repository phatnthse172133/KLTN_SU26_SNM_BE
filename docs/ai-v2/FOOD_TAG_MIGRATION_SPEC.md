# AI V2 FoodTag migration specification

## Scope and source of truth

`FoodTagMigrationMapCatalog` is the machine-readable, code-based migration
specification. Display names are carried only for reports; no migration lookup
may use a display name when a stable code exists.

The reviewed set contains:

- 102 codes from `SystemFoodTaxonomyCatalog`.
- 20 inactive legacy codes created by the old AI seed path.
- 14 active non-system/custom codes observed in the current PostgreSQL database.

The database inspection was read-only and rolled back. At inspection time it
contained 136 FoodTag rows: 102 system rows, 116 active/non-deleted rows, and 34
non-system rows. Non-system rows still had 184 FoodItemTag relations and 10
CustomerPreference relations, so inactive status is not evidence that the data
can be deleted.

## Mapping policy

| Legacy code family | Target | Policy |
|---|---|---|
| `COURSE_*` | `FoodCourse` | Migrate with the same stable code. |
| `ING_*` | `Ingredient` / `FoodIngredient` | Migrate with the same stable code. |
| `DIET_*` | `DietaryRestriction` / `FoodDietaryAttribute` | Migrate as suitability metadata only. Never infer an allergen declaration. Existing rows remain unconfirmed until an authoritative source confirms them. |
| `METHOD_*` | `PreparationMethod` | Migrate with the same stable code. |
| `TASTE_*` | `TasteProfile`, except explicit spicy levels to `SpiceLevel` | Migrate with stable codes. |
| `TEMP_*` | `ServingTemperature` | Migrate with the same stable code. |
| `PURPOSE_*` | `DiningPurpose` | Migrate with the same stable code. |
| `BUDGET_*`, `BUDGETFRIENDLY`, `MIDRANGE`, `PREMIUM` | Current effective price | DERIVE at query time; never copy as static food metadata. |
| `OTHER_SIGNATURE`, `OTHER_LOCAL_SPECIALTY`, `OTHER_SEASONAL`, `OTHER_CUSTOMIZABLE` | Search facets | Migrate explicit declarations. |
| `OTHER_SHARING`, `SHAREABLE` | Serving profile | Migrate as a serving attribute, not a course. |
| `OTHER_BEST_SELLER`, `OTHER_NEW_ITEM` | Sales/creation-time derived facets | DERIVE from authoritative data. |
| Cuisine/origin custom codes | Search facets | Preserve searchable cuisine/origin meaning without promoting them to categories. |

Allergen tables in the additive model intentionally receive no automatic rows
from the current FoodTag catalog. Tags such as `DIET_DAIRY_FREE` and
`DIET_PEANUT_FREE` describe an existing suitability claim; they are not proof
of ingredient absence, cross-contamination controls, or medical safety.

## Approved manual-review exceptions

### `DRINK` and `DESSERT`

- Disposition: `AMBIGUOUS_REQUIRES_MANUAL_REVIEW` until the structured customer
  profile target is fixed in Sub-phase B.
- Current references: `DRINK` has 10 food items and 1 preference; `DESSERT` has
  4 food items and 1 preference.
- Reason: the same legacy code has relation-specific meaning. FoodItemTag links
  indicate `COURSE_DRINK`/`COURSE_DESSERT`, while CustomerPreference links mean
  `PURPOSE_REFRESHMENT`/`PURPOSE_DESSERT`. A single target would lose behavior.

### `SOUP`

- Disposition: `AMBIGUOUS_REQUIRES_MANUAL_REVIEW`.
- Current references: 12 food items, 0 customer preferences.
- Reason: the legacy inference assigned this code from words for noodles,
  broth and water. A row may represent a food category, preparation method or
  meal course. Each linked food must be reviewed; no automatic target is safe.

### `OTHER_QUICK_SERVE`

- Disposition: `ARCHIVE` / derive-not-available.
- Current references: 0 food items, 0 customer preferences.
- Reason: there is no authoritative preparation/service-duration field from
  which quick service can be proven or derived. It is excluded from search and
  scoring and remains visible only in reconciliation until legacy cleanup.

These exceptions block automatic backfill for their affected relations. They
do not block the additive schema work, provided the backfill reports them and
does not fabricate a mapping.

## Safety rules for the future backfill

- Look up mappings by exact stable code using ordinal comparison.
- Unknown codes must be reported as unmapped and must stop destructive cleanup.
- `MIGRATE` rows require a non-empty target type and target code.
- `DERIVE` rows do not create static metadata relations.
- Manual-review rows are never silently skipped or auto-mapped.
- Hard customer constraints and soft preferences must remain distinct.
- No FoodTag, FoodItemTag, CustomerPreference or AIRecommendationLog table may
  be dropped during the additive phases.

## Sub-phase A acceptance status

- 102/102 system tag codes have a disposition.
- 34/34 non-system codes currently present have a disposition.
- 3 relation-sensitive codes are explicitly held for manual review; `OTHER_QUICK_SERVE` is archive-only with a documented reason.
- 0 dietary tags are mapped to `Allergen`.
- Mapping is based on stable codes, not display names.
- No migration or database write is part of this sub-phase.
