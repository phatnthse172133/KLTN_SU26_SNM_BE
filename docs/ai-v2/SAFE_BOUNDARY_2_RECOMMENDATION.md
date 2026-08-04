# Safe Boundary 2 — Gemini Adapter and Food Recommendation V2

## Scope

This boundary adds the Gemini V2 provider abstraction and the customer Food Recommendation V2 flow. It does not expose a Meal Plan endpoint, execute a Meal Plan provider call, modify the customer frontend, delete FoodTag data, or replace legacy AI endpoints.

## Public endpoints

- `POST /api/customer/ai/v2/recommendations`
- `POST /api/customer/ai/v2/recommendations/{sessionId}/feedback`

Both endpoints require the `Customer` role and use the per-customer `AIRecommendationV2Policy` fixed-window limit (6 requests/minute, no queue). A rejection uses `AI_RATE_LIMITED`.

Recommendation business statuses returned with HTTP 200 are `SUCCESS`, `NO_STRONG_MATCH`, `NO_SUITABLE_RESULTS`, and `LOCATION_REQUIRED`. An intent that remains insufficient after deterministic fallback returns HTTP 422 with `AI_FALLBACK_PARSE_INSUFFICIENT`.

## Gemini V2 egress boundary

`AIProviderV2:Enabled` is `false` by default. The API key is read by backend configuration only (`AIProviderV2__ApiKey` in environment/secret storage) and is sent only in the `x-goog-api-key` request header. It is never placed in the URI, query string, JSON payload, returned exception, or application log.

The adapter accepts only HTTPS requests whose host is exactly `generativelanguage.googleapis.com`. Tests inject a fake `HttpMessageHandler`; the production allowlist is not relaxed.

Intent extraction sends only:

- the current preference text;
- allowed normalized taxonomy codes;
- a strict JSON schema;
- a system instruction that treats user text and taxonomy as untrusted data.

Explanation generation sends only backend-selected evidence: food name, matched normalized facets, current price and budget evidence, real rating evidence when present, backend-computed distance text, dietary evidence, warnings, and unmatched soft preferences. It omits customer identifiers, email, authorization data, request coordinates, original request summary, category, database rows, and non-selected candidates.

Provider output cannot supply authoritative IDs, price, rating, distance, availability, score, or allergen status. The backend owns those fields. Strict deserialization, enum/range/length/taxonomy normalization, unknown-field rejection, unsupported-number rejection, identifier rejection, and token grounding reject invalid or ungrounded output.

## Failure behavior and call bounds

The adapter applies a linked timeout and propagates `CancellationToken`. Each operation has at most two attempts (`RetryCount` is clamped to 0–1). Only timeout, HTTP 429, HTTP 5xx/network transient failures, and invalid JSON responses are retryable. Cancellation, validation failures, permanent HTTP errors, disabled configuration, and invalid host/model configuration are not retried.

Disabled/missing-secret, timeout, 429, 5xx, invalid response, or ungrounded explanation falls back to the normalized deterministic parser/reason builder. A recommendation remains serviceable whenever fallback has sufficient intent and real candidates.

Meal Plan extraction was inactive when this boundary was completed. It is superseded by `SAFE_BOUNDARY_3_MEAL_PLAN.md`; the shared V2 adapter remains disabled by default and uses deterministic fallback when disabled.

## Deterministic recommendation pipeline

1. Validate and bound query, paging, budget, and coordinates.
2. Load active normalized catalogs and the customer's normalized profile.
3. Extract intent through Gemini V2 or deterministic fallback, then normalize against the catalog allowlist.
4. Read at most the configured candidate cap from real food/price/booth/market/metadata/review data.
5. Apply hard eligibility filters for deletion, availability, operational market/booth, open hours, budget, ingredient/preparation exclusions, confirmed dietary requirements, distance, and allergen safety.
6. Compute deterministic evidence and score, assign configured strong/near/low tiers, and diversity-rerank within a bounded score window.
7. Build deterministic reasons for all returned candidates; optionally attempt Gemini for the top selected candidate only.
8. Persist the session and ranked backend result membership transactionally; raw coordinates are not persisted.

An explicit allergen exclusion currently rejects candidates when the normalized model cannot prove an authoritative safe state. The current schema declares `CONTAINS`/`MAY_CONTAIN` but has no authoritative `FREE_FROM` declaration, so absence is treated as unknown rather than safe.

## Persistence and retention

Migration `AddAiV2RecommendationResults` is additive. It adds provider diagnostics to `AiRecommendationSession`, creates `AiRecommendationResult` with backend-owned rank/score/tier, and creates a partial unique feedback-state index for `LIKED`/`DISLIKED`.

Feedback validates session ownership, expiry, and result membership. `LIKED`/`DISLIKED` uses a parameterized PostgreSQL upsert with latest-timestamp-wins semantics; event actions append. The cleanup worker deletes expired sessions in bounded batches after the configured retention period; cascades remove results and feedback.

The `SmartNightMarket.AI.V2` meter exposes counters for requests, provider success/failure, fallback, invalid parse, candidates, no-result, strong matches, explanation fallback, and feedback actions, plus provider/recommendation duration histograms. Structured logs omit query text, coordinates, provider bodies, headers, and provider URLs.

## Configuration

```json
{
  "AIProviderV2": {
    "Enabled": false,
    "Provider": "Gemini",
    "Model": "gemini-2.5-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
    "TimeoutSeconds": 8,
    "RetryCount": 1
  }
}
```

Do not store a real API key in an appsettings file. Enable through controlled production configuration only after smoke tests and monitoring are ready.

## Legacy boundary note

The pre-existing `GeminiAIProviderService` and legacy `/api/ai/*` endpoints are unchanged by this boundary and remain separately disabled by default. The guarantees in this document apply to the V2 adapter and V2 endpoints. Legacy removal or hardening must be handled by its approved deletion/cutover phase; it must not be enabled as a substitute for V2.

V1 compatibility remains as follows:

- `GET /api/ai/home`, `GET /api/ai/recommendations/me`, `POST /api/ai/food-discovery`, legacy dining-plan routes, and `POST /api/ai/feedback` remain registered and are not routed through V2 scoring.
- V2 reuses commercial visibility/orderability and current-price behavior, but uses its own candidate repository, normalized metadata, parser, scoring, session-result relation, feedback endpoint, and provider contracts.
- Legacy FoodTag reads/writes, `CustomerPreference`, and `AIRecommendationLog` remain for V1. V2 does not use FoodTag as source-of-truth.
- No V1 request/response DTO, route, score, or persistence behavior is replaced in Safe Boundary 2.
- Deprecation requires production parity evidence, client cutover, disabled legacy egress, retention/export decisions, and a separately approved deletion migration. None of those destructive steps occurs here.
