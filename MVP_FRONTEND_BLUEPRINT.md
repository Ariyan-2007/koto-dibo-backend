# Koto Dibo? (কত দিবো?) — MVP Frontend Blueprint

> Companion to `MVP_BLUEPRINT.md`. That document tracks the backend phase plan; this one is the client-side build plan against the API surface that plan produced. Every endpoint listed below is live — Phases 2–4 of the backend blueprint (fair-split domain, bill-split API, unified settlement) are done, so nothing here is blocked on backend work. Organized by module, in build order, with the exact request/response shape and the edge cases the backend already enforces (so the UI can mirror them instead of relying on round-trips to discover them).

**Stack** (per `MVP_BLUEPRINT.md` §6): Vite + React + TypeScript, installable as a PWA. All endpoints are under `/api`, JWT bearer auth (`Authorization: Bearer <AccessToken>`), JSON in/out. Errors follow one envelope shape everywhere (see §0.3).

---

## 0. Cross-cutting foundation (build before any feature module)

Nothing below is a "screen" — it's infrastructure every module depends on. Get it right once.

### 0.1 API client & auth
- Central HTTP client with a request interceptor attaching `Authorization: Bearer <AccessToken>`.
- Access tokens are short-lived (15 min in dev config) — a 401 must trigger a **single-flight** silent refresh (`POST /api/auth/refresh`), queue concurrent requests behind it, then retry. Don't fire N parallel refresh calls when N requests 401 at once.
- Refresh tokens **rotate on every use** — always persist the new `RefreshToken` from the response, discard the old one. If a refresh call itself fails (expired/reused/revoked token), the backend has reuse-detection that invalidates the whole token family — treat any refresh failure as "force full logout," not "retry."
- Persist the refresh token somewhere that survives a PWA relaunch (IndexedDB, not just an in-memory variable) — access token can stay in memory only.
- All `/api/auth/*` endpoints are IP rate-limited (10 req/min) — surface a friendly "too many attempts" state on 429, don't silently retry.

### 0.2 Household context
- Almost every endpoint is household-scoped (`/api/households/{householdId}/...`). The app needs a persisted "currently selected household" concept (e.g. in the route or a top-level store) that every feature module reads from — there is no "default household" from the API.
- `HouseholdDto.CallerRole` (`Owner`/`Manager`/`Member`/`Viewer`) is returned on every household read — cache it alongside the selected household and gate UI affordances (Add/Edit/Cancel buttons) against it client-side, mirroring the permission matrix in §0.4. This avoids "click submit → 403" dead ends.

### 0.3 Error envelope
Every error response (via `ExceptionHandlingMiddleware`) has the same shape:
```
{ "status": 400, "title": "Validation failed.", "errors": { "fieldName": ["message"] } | null }
```
- `400` — validation failure (`errors` populated, keyed by field — map straight to form field errors) or a domain rule violation (`errors: null`, show `title` as a toast/inline banner).
- `401` — not authenticated (trigger refresh flow, or redirect to login if refresh also fails).
- `403` — authenticated but not permitted (`title` is human-readable — show it, but this shouldn't normally be reachable if §0.4 gating is correct).
- `404` — not found (also returned when the household simply doesn't exist *or* the caller isn't a member — the API deliberately doesn't distinguish these to prevent household-ID enumeration).
- `409` — conflict (duplicate key, e.g. re-adding a member who's already active).
- `429` — rate limited.
- `500` — unexpected; show a generic retry state.

### 0.4 Client-side permission matrix
Mirror `HouseholdRolePolicy` so the UI never offers an action the API will reject. At MVP scope, the practical rule is uniform across Bazar/Contributions/Meals/BillSplit:

| Action | Owner | Manager | Member | Viewer |
|---|---|---|---|---|
| View lists / settlements | ✅ | ✅ | ✅ | ✅ |
| Add own entry | ✅ | ✅ | ✅ | ❌ |
| Edit/cancel **own** entry | ✅ | ✅ | ✅ | ❌ |
| Edit/cancel **anyone's** entry | ✅ | ✅ | ❌ | ❌ |
| Record meal count for **another** member | ✅ | ✅ | ❌ | ❌ |
| Household settings, members, roles | ✅ | ✅ (can't touch Owner/Manager removal) | ❌ | ❌ |

Encode this once as a shared helper (`canEdit(role, ownerId, currentUserId)`), reused by every module below instead of re-deriving it per screen.

### 0.5 Known API gaps to design around
Two real gaps surfaced while building the backend that the frontend has to work around (or that should feed back into a future backend ticket):

1. **No bulk "set household default meal count" endpoint.** `MVP_BLUEPRINT.md` §1.1 describes a day's meal count as having a "household default that applies to everyone unless overridden" — but mechanically, `DailyMealEntry` is just one row per `(household, user, date)` with no fallback semantics. A user with **no row at all for a date contributes 0 meal units that day**, full stop. So "set today's default to 2x for everyone" has no single API call — the day-grid UI must either (a) loop and call `PUT .../meals/{date}/{userId}` once per active member, or (b) request a bulk endpoint from backend. Plan for (a) at MVP; flag (b) as a fast-follow if the per-member loop proves too chatty on slow connections.
2. **No tariff-config lookup endpoint.** The `TariffCountry` field on a `TariffMetered` bill split isn't validated against a browsable list — the only seeded value at MVP is `"BD"`. The create-bill-split form should hardcode `"BD"` as the only option (matching the backend blueprint's explicit deferral of a country picker) rather than building a picker against an endpoint that doesn't exist yet.

---

## Phase 0 — Foundation & Auth

**Screens:** Register, Login, (silent) refresh — no dedicated screen, just the interceptor — Logout / Logout-all-devices.

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/auth/register` | `{ Name, Email, Password, DeviceId?, DeviceName? }` | `AuthResponse` |
| POST | `/api/auth/login` | `{ Email, Password, DeviceId?, DeviceName? }` | `AuthResponse` |
| POST | `/api/auth/refresh` | `{ RefreshToken }` | `AuthResponse` |
| POST | `/api/auth/logout` | `{ RefreshToken }` | `204` |
| POST | `/api/auth/logout-all` | *(Authorize, no body)* | `204` — revokes every refresh token for the user (all devices) |

`AuthResponse`: `{ AccessToken, ExpiresAt, RefreshToken, UserId, Name, Email }`.

Notes: account lockout exists server-side after repeated failed logins (per `BLUEPRINT.md`) — a login 400/403 after several attempts may mean locked-out, not just wrong password; surface the `title` message as-is rather than a generic "wrong password." `DeviceId`/`DeviceName` are optional but worth sending (stable per-install UUID + a human label like "Ariyan's Pixel") since `logout-all` and any future "manage devices" screen depend on refresh tokens being distinguishable per device.

---

## Phase 1 — Household Management

Everything else is household-scoped, so this has to exist before any other module is usable end-to-end.

**Screens:** Household switcher/list, Create household, Household settings, Members list, Add member, Change role, Leave household.

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/households` | `{ Name, Description?, Type? }` | `HouseholdDto` |
| GET | `/api/households` | — | `HouseholdDto[]` (mine) |
| GET | `/api/households/{id}` | — | `HouseholdDto` |
| PATCH | `/api/households/{id}` | `{ Name?, Description?, Type? }` | `HouseholdDto` |
| POST | `/api/households/{id}/archive` | — | `HouseholdDto` |
| POST | `/api/households/{id}/restore` | — | `HouseholdDto` |
| GET | `/api/households/{id}/members` | — | `HouseholdMemberDto[]` |
| POST | `/api/households/{id}/members` | `{ Email, Role }` | `HouseholdMemberDto` |
| DELETE | `/api/households/{id}/members/{userId}` | — | `204` |
| PATCH | `/api/households/{id}/members/{userId}/role` | `{ Role }` | `HouseholdMemberDto` |
| POST | `/api/households/{id}/leave` | — | `204` |

`HouseholdDto`: `{ Id, Name, Description?, Type?, Status, OwnerUserId, MemberCount, CallerRole, CreatedAt, UpdatedAt, ArchivedAt? }`.
`HouseholdMemberDto`: `{ UserId, Name, Email, Role, JoinedAt }`. `Role` is one of `Owner | Manager | Member | Viewer`. `Type` is free text (no fixed vocabulary server-side — e.g. "Bachelor Mess", "Family" — treat as a suggested-values combobox, not a hard enum).

Notes:
- **Add member requires an existing account.** `Email` must belong to an already-registered user, or the call 400s ("No account found with this email") — there's no invite-by-email-for-non-users flow yet (`HouseholdMembershipStatus.Invited` is explicitly deferred per the backend blueprint). Copy should say "invite an existing Koto Dibo user," not "invite by email."
- The Owner role can't be removed or role-changed via these endpoints (`DomainException` if attempted) — ownership transfer isn't implemented yet either, so hide "remove"/"change role" controls entirely for the Owner row.
- A Manager can't remove another Manager (only the Owner can) — disable that action client-side for Manager-viewing-Manager rows.
- If the Owner leaves while other active members remain, the call 400s ("Transfer ownership first") — if the Owner is the *last* active member, leaving auto-archives the household instead. Surface both outcomes distinctly in the confirmation dialog copy.
- Archiving a household should be a confirm-with-consequences dialog (blocks membership changes until restored, per `RequireActive` checks across every membership mutation).

---

## Phase 2 — Money Ledgers: Bazar & Contributions

The two cash-flow ledgers that feed meal-rate calculation. Same CRUD shape twice — build one generic "ledger entry" form component and parametrize it.

**Screens:** Bazar list (filter by date range / status) + Add/Edit/Cancel; Contributions list + Add/Edit/Cancel.

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/households/{householdId}/bazar` | `{ Date, Amount, Currency, Note? }` | `BazarPurchaseDto` |
| GET | `/api/households/{householdId}/bazar?from=&to=&status=` | — | `BazarPurchaseDto[]` |
| GET | `/api/households/{householdId}/bazar/{purchaseId}` | — | `BazarPurchaseDto` |
| PATCH | `/api/households/{householdId}/bazar/{purchaseId}` | `{ Date?, Amount?, Currency?, Note? }` | `BazarPurchaseDto` |
| POST | `/api/households/{householdId}/bazar/{purchaseId}/cancel` | — | `BazarPurchaseDto` |
| POST | `/api/households/{householdId}/contributions` | `{ Date, Amount, Currency, Notes? }` | `ContributionDto` |
| GET | `/api/households/{householdId}/contributions?from=&to=&status=` | — | `ContributionDto[]` |
| GET | `/api/households/{householdId}/contributions/{contributionId}` | — | `ContributionDto` |
| PATCH | `/api/households/{householdId}/contributions/{contributionId}` | `{ Date?, Amount?, Currency?, Notes? }` | `ContributionDto` |
| POST | `/api/households/{householdId}/contributions/{contributionId}/cancel` | — | `ContributionDto` |

`BazarPurchaseDto`: `{ Id, HouseholdId, PurchasedByUserId, Date, Amount, Currency, Note?, Status, CreatedAt, UpdatedAt }`.
`ContributionDto`: same shape with `ContributedByUserId` and `Notes` instead of `Note`. `Status` is `Active | Cancelled`.

Notes:
- `Date` cannot be in the future (validate client-side before submit to save a round trip; server 400s on `date` field otherwise). `Amount` must be `> 0`. `Currency` is a free 3-letter code validated as `^[A-Za-z]{3}$` — default the field to a constant (e.g. `"BDT"`) with the option to override, since there's no household-level base-currency setting yet.
- `status` filter query param takes the literal enum string (`Active`/`Cancelled`); omit it to get both.
- Cancelled entries are **soft-deleted, never removed** — the list endpoint returns them too (unless filtered out), so render them struck-through/greyed rather than expecting them to disappear.
- Edit/Cancel buttons: gate per §0.4 — a Member only sees them on their own entries; Owner/Manager see them on all.
- `PurchasedByUserId`/`ContributedByUserId` are set server-side from the caller's token on create — never a form field.

---

## Phase 3 — Meal Module

The MVP's first anchor feature. **Read §0.5.1 before building the day-grid** — there is no default/fallback semantics server-side.

**Screens:** Meal day-grid (month view: rows = members, columns = days, editable cell = meal count for that member/day), Meal settlement view (per-member table + a simple "who owes whom" strip).

| Method | Path | Request | Response |
|---|---|---|---|
| PUT | `/api/households/{householdId}/meals/{date}/{userId}` | `{ Count, Notes? }` | `DailyMealEntryDto` |
| DELETE | `/api/households/{householdId}/meals/{date}/{userId}` | — | `204` |
| GET | `/api/households/{householdId}/meals?from=&to=&userId=` | — | `DailyMealEntryDto[]` |
| GET | `/api/households/{householdId}/meals/rate?from=&to=` | — | `MealCalculationDto` |

`DailyMealEntryDto`: `{ Id, HouseholdId, UserId, Date, Count, Notes?, Status, CreatedAt, UpdatedAt }`.
`MealCalculationDto`: `{ From, To, FoodCost, TotalMealUnits, MealRate, TotalContributions, Members: MealMemberCostDto[], CalculationVersion }` where `MealMemberCostDto` = `{ UserId, MealUnits, MealCost, Contribution, GiveTake }`.

Notes:
- `PUT` is an **upsert** — same call whether creating or editing that member/day's count, keyed by `(household, date, userId)`. A Member can only target their own `userId` (`RecordOwnMealCount`); Owner/Manager can target anyone (`RecordAnyMealCount`) — gate the "set for other members" UI accordingly.
- `Count` is decimal, not integer — supports `0.5`, `1.5x` etc. `Count = 0` explicitly means "excluded that day," which is different from *no entry at all* (also 0 units, but for a different reason — "not recorded" vs. "recorded as zero"). Render these as visually distinct grid states: empty cell = no entry yet; a `0` chip = explicitly excluded.
- `DELETE` clears a cell back to "no entry" (distinct from setting `Count: 0`) — expose this as a separate "clear" action, not the same as typing 0.
- Date cannot be future — disable future columns in the grid entirely rather than letting a submit fail.
- `MealRate` is `null` when `TotalMealUnits` is 0 for the period (no entries at all) — handle this explicitly in the settlement view ("no meals recorded yet" empty state), don't divide-by-zero-render `NaN`.
- `GiveTake` sign convention: positive = household owes this member, negative = this member owes the household. Use this consistently in the "who owes whom" strip (and again in Phase 5's unified view, which reuses the same field name).
- Per §0.5.1: a Manager setting "today's household default" has to loop `PUT` once per active member (fetch the member list from Phase 1's `GET .../members` first). Debounce/batch these client-side and show one combined progress state, not N separate spinners.

---

## Phase 4 — Bill Split Module

The MVP's second anchor feature — the harder one. `SplitMethod` fully determines which fields are relevant; build the create/edit form as three sub-forms behind one method picker rather than one form with everything optional.

**Screens:** Bill split list (filter by period/status), Create bill split (method-aware form), Bill split detail + settlement (band-breakdown table for `TariffMetered`; simple per-member share list for `EqualSplit`/`WeightedSplit`), Edit, Cancel.

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/households/{householdId}/bill-splits` | `CreateBillSplitRequest` | `BillSplitDto` |
| GET | `/api/households/{householdId}/bill-splits?from=&to=&status=` | — | `BillSplitDto[]` |
| GET | `/api/households/{householdId}/bill-splits/{id}` | — | `BillSplitDto` |
| PATCH | `/api/households/{householdId}/bill-splits/{id}` | `UpdateBillSplitRequest` | `BillSplitDto` |
| POST | `/api/households/{householdId}/bill-splits/{id}/cancel` | — | `BillSplitDto` |
| GET | `/api/households/{householdId}/bill-splits/{id}/settlement` | — | `BillSplitSettlementDto` |

**`CreateBillSplitRequest`**: `{ Title, SplitMethod, PeriodFrom, PeriodTo, Currency, TariffCountry?, TariffProvider?, MainMeterUsage?, TotalAmount?, MemberInputs: [{ UserId, Value }], Notes? }`. `SplitMethod` is one of `TariffMetered | EqualSplit | WeightedSplit`.

**Per-method required fields** (validated server-side — mirror in the form so invalid combos never reach submit):

| Field | TariffMetered | EqualSplit | WeightedSplit |
|---|---|---|---|
| `TariffCountry` | required | — | — |
| `MainMeterUsage` | required, ≥ 0 | — | — |
| `TotalAmount` | *not accepted* (computed from tariff bands at settlement time) | required, > 0 | required, > 0 |
| `MemberInputs` | required; `Value` = sub-meter usage per member, ≥ 0, **sum must be ≤ `MainMeterUsage`** | ignored (splits across current active members automatically) | required; `Value` = fixed weight per member, > 0 |

**`UpdateBillSplitRequest`** (PATCH, partial): `{ Title?, MainMeterUsage?, TotalAmount?, MemberInputs?, Notes? }`. **`SplitMethod`, `PeriodFrom`/`PeriodTo`, `Currency`, `TariffCountry`/`TariffProvider` are immutable** — disable those fields entirely in the edit form; a method change means creating a new bill split, not editing this one.

**`BillSplitDto`**: `{ Id, HouseholdId, CreatedByUserId, Title, SplitMethod, PeriodFrom, PeriodTo, Currency, TariffCountry?, TariffProvider?, MainMeterUsage?, TotalAmount?, MemberInputs, Notes?, Status, CreatedAt, UpdatedAt }`.

**`BillSplitSettlementDto`** (computed fresh on every call — nothing is cached server-side, refetch after any edit):
```
{
  BillSplitId, TotalAmount, AttributedCost, SharedCost,
  Bands: [{ FromUnits, ToUnits, RatePerUnit, UnitsInBand, AttributedUnits, SharedUnits, Cost }],
  Members: [{ UserId, Usage, AttributedCost, SharedCost, TotalOwed }],
  CalculationVersion
}
```

Notes:
- `AttributedCost` = the portion driven by members' own sub-meter usage (allocated proportionally to `Usage`); `SharedCost` = common-area usage no sub-meter captures, split **equally across the household's current active members** (not just those with a sub-meter reading) — a member with `Usage: 0`/no reading still gets a `SharedCost` share and appears in `Members`.
- The band table is the "why did this cost so much" visualization and the actual value proposition of `TariffMetered` — for each band show rate, total units consumed, and the attributed-vs-shared split within that band (a stacked bar or the reference repo's "flip card" pattern both work). The most expensive bands get attributed to metered usage first — expect (and design for) most/all of the top band's cost landing on `AttributedCost`, with only the cheapest band's leftover typically falling into `SharedCost`.
- Only `"BD"` is seeded as a valid `TariffCountry` at MVP (§0.5.2) — hardcode it as the only option, don't build a country picker yet.
- Ownership/edit rules match Phase 2 exactly (creator, or Owner/Manager, can edit/cancel; `Status: Cancelled` entries are never edit-able again).

---

## Phase 5 — Unified Settlement Dashboard

The household's home screen once selected — composes Phases 3 and 4 into one number per member.

| Method | Path | Response |
|---|---|---|
| GET | `/api/households/{householdId}/settlement?from=&to=` | `HouseholdSettlementDto` |

```
{
  HouseholdId, From, To,
  TotalMealGiveTake, TotalBillSplitOwed,
  Members: [{ UserId, MealGiveTake, BillSplitOwed, NetBalance }],
  CalculationVersion
}
```

`NetBalance = MealGiveTake − BillSplitOwed` (same sign convention as Phase 3: positive = household owes them, negative = they owe the household).

Notes:
- This endpoint internally recomputes the meal rate **and** every active bill split's settlement for the period server-side — it's the most expensive call in the API. Show a skeleton, not a spinner, and don't poll it aggressively.
- It gives **totals only** — no day-level or band-level detail. Every member row should link out to the Phase 3 meal settlement and the relevant Phase 4 bill splits for the "why" breakdown; don't try to cram that detail into this screen.
- Natural default landing route: after login → household selected → this dashboard, defaulting `from`/`to` to the current calendar month.

---

## Phase 6 — PWA & offline polish

Cross-cutting, layered in once Phases 0–5 work online. Highest offline value is meal entry (per `MVP_BLUEPRINT.md` §6) — bazar/meal logging happens in-flat with patchy connectivity.

- Installable manifest + service worker (Vite PWA plugin).
- Offline queue specifically for `PUT/DELETE .../meals/{date}/{userId}` — optimistic UI update immediately, queue the request, replay in order on reconnect. Because the endpoint is a pure upsert keyed by `(household, date, userId)`, replay is naturally last-write-wins with no server-side conflict resolution needed — just replay queued mutations in the order they were made.
- Persist the refresh token in IndexedDB (not memory-only) so auth survives a PWA process kill/relaunch (§0.1).
- Everything else (ledgers, bill splits, settlement) can stay online-only at MVP — don't over-invest in offline support for screens that aren't the "in-flat, patchy connectivity" use case.

---

## Phase 7 — Deferred (blocked on backend)

**Do not build against these yet.** `ExpensesController`/`BudgetController` are `501 Not Implemented` stubs backing non-household-scoped, unspecified entities (`Expense`/`Budget` currently have no `HouseholdId`, no lifecycle, no validators) — `MVP_BLUEPRINT.md` §7 explicitly marks this Phase 6 backend work as lowest priority and "not speculative." Revisit this section once that backend work lands with a real shape.

---

## Suggested build order

```
Phase 0 (foundation & auth)
  └─→ Phase 1 (households)
        ├─→ Phase 2 (bazar + contributions)  ─┐
        ├─→ Phase 3 (meals)                   ├─→ Phase 5 (unified settlement)
        └─→ Phase 4 (bill splits)            ─┘
                                                    Phase 6 (PWA/offline) — layer in continuously
                                                    Phase 7 (expenses/budget) — blocked, do not start
```

Phases 2, 3, and 4 don't share components beyond the §0 foundation and the generic ledger-entry pattern (Phase 2 reused loosely for Phase 4's flat-split methods) — they can be built in parallel by different people once Phase 1 lands. Phase 5 is deliberately last: it's a read-only aggregation view with nothing to show until 2–4 have real data flowing through them.
