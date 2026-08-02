# Safe Boundary 3A–3B — Meal Plan V2

## Scope and public API

This boundary adds authenticated customer Meal Plan V2 create/detail and deterministic mutations:

- `POST /api/customer/ai/v2/meal-plans`
- `GET /api/customer/ai/v2/meal-plans/{planId}`
- `GET /api/customer/ai/v2/meal-plans/{planId}/items/{itemId}/alternatives`
- `PUT /api/customer/ai/v2/meal-plans/{planId}/items/{itemId}`
- `DELETE /api/customer/ai/v2/meal-plans/{planId}/items/{itemId}?expectedPlanVersion={version}`
- `POST /api/customer/ai/v2/meal-plans/{planId}/courses/{course}/regenerate`

There is deliberately no add-to-cart route, service, placeholder, or direct CartItem write. Boundary 3C is blocked because `ConcurrentCheckout_GuaranteesHoldThroughRealOrderService` failed 10/10 with `Placed/Paid/Consumed` at `PostgresBusinessConcurrencyTests.cs:251`, exposing a PayOS webhook versus `OrderCleanupBackgroundService` race.

## Authoritative behavior

The backend validates active market/booth/food state, current effective price, normalized metadata, hard exclusions, distance, course and serving feasibility. Missing course or serving data is not inferred. Every persisted plan has one `MarketId`; domain checks reject booth-market mismatch and duplicate active food. Generation is bounded and deterministic, returns at most three feasible plans, and never creates a synthetic plan to fill the response.

Create is idempotent by `(CustomerId, IdempotencyKey)` plus a canonical request SHA-256. Same payload returns the existing session; a different payload returns `AI_IDEMPOTENCY_CONFLICT`. Sessions are editable for 120 minutes by default and retained read-only for 30 days. Cleanup is cancellation-aware and deletes one bounded batch per cycle.

All mutations take a PostgreSQL row lock, validate `expectedPlanVersion`, operate only in the plan market, and use one authoritative recalculation service for total, remaining budget, main/shared serving coverage, required-course completeness, booth composition, warnings, score and version. Remove may make a plan incomplete; alternatives do not mutate; failed regenerate leaves the transaction unchanged.

## Provider boundary

`AIProviderV2:Enabled` remains `false` by default. Meal-plan intent extraction sends only the current request text, the authoritative dining-style context, bounded normalized taxonomy codes, the strict response schema, and the injection-resistant system instruction. It does not send customer ID, email, authorization data, party size, budget, coordinates, database rows, or candidates. Gemini never selects foods or authors IDs, price, rating, distance, availability, serving, score, quantities or allergen safety. Disabled, timeout, throttling, transient, or invalid output uses the deterministic parser.

## V1 and legacy compatibility

AI V1 routes, FoodTag, FoodItemTag, CustomerPreference, AIRecommendationLog and historical migrations remain intact. V1 reads and writes continue unchanged. Meal Plan V2 uses normalized ingredient/allergen/dietary/preparation/taste/course/purpose data and does not use FoodTag as source of truth. No current application database is migrated by this implementation; migrations are verified only against disposable PostgreSQL databases.
