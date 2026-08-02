# AI V2 Sub-phase E cutover

## Source of truth

- New booth menu writes use normalized metadata through the `/v2` routes.
- Food detail and discovery search use normalized relations and `FoodAiProfile.SearchText`.
- `CustomerFoodProfile` is the source of truth for the new profile API.
- `FoodTag`, `FoodItemTag`, `CustomerPreference` and AI V1 remain present.
- AI V2 must not read the deprecated `tags` compatibility field.

## Backend contracts prepared for clients

### Menu management V2

- `GET /api/food-metadata/catalogs`
- `POST /api/booths/mine/{boothId}/menu/v2`
- `PUT /api/booths/mine/{boothId}/menu/v2/{foodItemId}`

Required fields are `categoryId`, `name`, `price`, `primaryCourse`,
`estimatedServingCount` and availability. Optional semantic fields are
additional courses, catalog IDs, spice, temperature, serving description and
shareability. All enum values are serialized as stable strings.

Allergen declarations made through this API are stored as
`OWNER_DECLARED`, `IsConfirmed=false`. An owner update cannot remove or
downgrade an `ADMIN_VERIFIED` allergen or dietary declaration.

### Customer food discovery

- `GET /api/foods`
- `GET /api/foods/{foodItemId}`

List responses expose primary course, serving count and shareability. Detail
responses expose courses, ingredients, allergen declarations, dietary
attributes, preparation methods, tastes, spice, temperature and serving data.
The legacy `tags` field is deprecated and derived from normalized metadata; no
`FoodItemTag` query is used by Food Detail.

### Customer food profile

- `GET /api/customer/food-profile`
- `PUT /api/customer/food-profile`

The authenticated customer ID is taken only from the access token. The request
does not accept a customer ID. Avoided ingredient/taste conflicts are rejected;
allergen exclusions are never converted to dietary requirements.

## V1 compatibility and sunset gate

- Existing menu and `/api/customers/me/preferences` endpoints remain.
- V1 menu writes keep their legacy relations and add only reviewed normalized
  mappings. They do not delete richer normalized metadata because legacy tags
  cannot express its source or verification state.
- V1 preference GET derives from normalized profile when one exists. V1 PUT
  preserves the legacy rows and only adds safe normalized mappings; it cannot
  erase V2-only constraints.
- This compatibility mode ends only after the old clients have moved to the V2
  contracts and a production-like reconciliation confirms no required legacy
  writes remain. It is not authorization to drop legacy tables.

## Food AI profile

`FoodAiProfile` generation is deterministic and local. It excludes price,
promotion, rating, distance, transient availability, legacy raw tags and
`OTHER_QUICK_SERVE`. Menu writes update SearchText/content hash and mark an old
embedding stale; no provider is called synchronously and no embedding is
fabricated. Single-food and bounded batch rebuild services are registered.

## Boundary

This artifact completes the E1-E4 boundary only. Provider abstraction,
Recommendation V2 and Meal Plan V2 APIs are intentionally not started here.
