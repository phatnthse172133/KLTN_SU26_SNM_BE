# AI V2 backfill reconciliation

- Mode: `DryRun`
- Generated (UTC): `2026-08-02T09:21:25.7663332Z`
- Legacy tags: 136 (102 system, 34 non-system, 116 active, 20 archived)
- Legacy relations preserved: 274 FoodItemTag, 28 CustomerPreference
- DESSERT_CUSTOMER: 1
- DESSERT_FOOD: 4
- DRINK_CUSTOMER: 1
- DRINK_FOOD: 10
- OTHER_QUICK_SERVE_CUSTOMER: 0
- OTHER_QUICK_SERVE_FOOD: 0
- SOUP_CUSTOMER: 0
- SOUP_FOOD: 12
- Unmapped: 0; ambiguous/manual: 9
- Legacy data changed: False

## Planned writes
- Catalogs: 77
- CustomerProfiles: 4
- CustomerRelations: 17
- CustomerScalarUpdates: 3
- FoodAiProfileCreates: 56
- FoodAiProfileUpdates: 0
- FoodMetadataRelations: 151
- FoodScalarUpdates: 45

## Exceptions
- `SOUP_MANUAL_REVIEW` / `FoodItemTag` / `99999999-9999-9999-9999-999999990401`: Name and noodle tag may indicate a broth dish, but description does not state broth/soup and category is generic demo data.
- `SOUP_MANUAL_REVIEW` / `FoodItemTag` / `99999999-9999-9999-9999-999999990803`: Description says 'món nước nóng', but the same row also has the contradictory DRINK tag and a generic demo category.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990104`: Name, description and DRINK tag explicitly identify a cold beverage.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990202`: Name and description explicitly describe grilled pork, raw vegetables and crispy spring rolls; no soup/broth evidence.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990301`: Description explicitly identifies a cold dessert and the existing DESSERT tag corroborates it.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990303`: Description explicitly identifies a beverage.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990304`: Description explicitly identifies a cold fruit dessert.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990403`: Name and description explicitly identify grilled chicken; SOUP and DRINK are contradictory legacy inferences.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990603`: Description explicitly identifies a cold, lightly sweet dessert.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990604`: Description explicitly identifies a street beverage.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990903`: Description explicitly identifies a sweet, rich cold dessert.
- `SOUP_NOT_APPLICABLE` / `FoodItemTag` / `99999999-9999-9999-9999-999999990904`: Name and description explicitly identify a cold strawberry drink.
- `UNSUPPORTED_AVOIDED_COURSE` / `CustomerPreference` / `e062a5bb-7216-43a6-bf25-c0bd467c3204`: Legacy avoid-course remains available through dual-read; no positive preference is fabricated.
- `UNSUPPORTED_AVOIDED_SPICE` / `CustomerPreference` / `c82c1ea8-046d-48f6-9fbe-48be1a813ef1`: Avoided spice is not converted into a positive preferred level.
- `UNSUPPORTED_SOFT_DIETARY` / `CustomerPreference` / `33f283e4-4dc4-4b9b-b254-4dcd38c8a50f`: Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained.
- `UNSUPPORTED_SOFT_DIETARY` / `CustomerPreference` / `4a573c19-9b19-4e8f-a2a0-490a26646add`: Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained.
- `UNSUPPORTED_SOFT_DIETARY` / `CustomerPreference` / `70dceb56-5cc8-4124-93c9-b9b199f82448`: Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained.
- `UNSUPPORTED_SOFT_DIETARY` / `CustomerPreference` / `bd35bbfd-eeda-4bf3-b4c0-eba47c4e2256`: Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained.
- `UNSUPPORTED_SOFT_DIETARY` / `CustomerPreference` / `c4bf0dc7-8955-41db-b79f-15fc4329903d`: Only explicit hard required diets migrate to CustomerDietaryRequirement; legacy relation is retained.

## SOUP resolutions
- `99999999-9999-9999-9999-999999990803` Bò viên nóng: **MANUAL_REVIEW** (55%) — Description says 'món nước nóng', but the same row also has the contradictory DRINK tag and a generic demo category.
- `99999999-9999-9999-9999-999999990401` Bún num bò chóc: **MANUAL_REVIEW** (50%) — Name and noodle tag may indicate a broth dish, but description does not state broth/soup and category is generic demo data.
- `99999999-9999-9999-9999-999999990202` Bún thịt nướng chả giò: **NOT_APPLICABLE** (99%) — Name and description explicitly describe grilled pork, raw vegetables and crispy spring rolls; no soup/broth evidence.
- `99999999-9999-9999-9999-999999990301` Chè ba màu: **NOT_APPLICABLE** (99%) — Description explicitly identifies a cold dessert and the existing DESSERT tag corroborates it.
- `99999999-9999-9999-9999-999999990403` Gà nướng sả ớt: **NOT_APPLICABLE** (99%) — Name and description explicitly identify grilled chicken; SOUP and DRINK are contradictory legacy inferences.
- `99999999-9999-9999-9999-999999990904` Nước ép dâu: **NOT_APPLICABLE** (100%) — Name and description explicitly identify a cold strawberry drink.
- `99999999-9999-9999-9999-999999990604` Nước mía tắc: **NOT_APPLICABLE** (100%) — Description explicitly identifies a street beverage.
- `99999999-9999-9999-9999-999999990104` Nước sâm lạnh: **NOT_APPLICABLE** (100%) — Name, description and DRINK tag explicitly identify a cold beverage.
- `99999999-9999-9999-9999-999999990303` Nước sâm rong biển: **NOT_APPLICABLE** (100%) — Description explicitly identifies a beverage.
- `99999999-9999-9999-9999-999999990603` Sữa chua nếp cẩm: **NOT_APPLICABLE** (99%) — Description explicitly identifies a cold, lightly sweet dessert.
- `99999999-9999-9999-9999-999999990903` Sữa chua phô mai: **NOT_APPLICABLE** (99%) — Description explicitly identifies a sweet, rich cold dessert.
- `99999999-9999-9999-9999-999999990304` Trái cây dầm: **NOT_APPLICABLE** (99%) — Description explicitly identifies a cold fruit dessert.
