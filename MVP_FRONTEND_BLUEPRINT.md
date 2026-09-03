# Koto Dibo? (কত দিবো?) — MVP Frontend Blueprint

> Companion to `MVP_BLUEPRINT.md`. That document tracks the backend phase plan; this one is the client-side build plan against the API surface that plan produced. Every endpoint listed below is live — Phases 2–4 of the backend blueprint (fair-split domain, bill-split API, unified settlement) are done, so nothing here is blocked on backend work. Organized by module, in build order, with the exact request/response shape and the edge cases the backend already enforces (so the UI can mirror them instead of relying on round-trips to discover them).

**Stack** (per `MVP_BLUEPRINT.md` §6): Vite + React + TypeScript, installable as a PWA. All endpoints are under `/api`, JWT bearer auth (`Authorization: Bearer <AccessToken>`), JSON in/out. Errors follow one envelope shape everywhere (see §0.3).

> **Update (2026-09-01):** analyzed a real household's multi-month tracking spreadsheet (`House_No_289.xlsx`, 7 months of live usage) against this plan. It confirmed Phases 1–4 match real usage directly, but surfaced a mechanic the original plan didn't cover: **balances carry forward across months rather than resetting to zero** — both the shopping-fund leftover and each member's settlement position. See §0.6 and the expanded Phase 5 below. This also drove one small backend fix (`BazarPurchase.Amount` now accepts negative values, needed to record a month's leftover cash) — already shipped.
>
> **Update (2026-09-01, later same day):** Bill Split's `TariffMetered` method upgraded backend-side — Equal Split and Weighted Split are **unchanged**. Two things landed, both reflected in Phase 4 below: (1) it must be **displayed** as "Electricity Bill (Postpaid)" everywhere in the UI (the API's `SplitMethod` value stays `TariffMetered` — this is a label-mapping change, not an API contract change); (2) `CreateBillSplitRequest`/`UpdateBillSplitRequest` gained `FixedCharges: [{ Label, Amount }]` — constant, non-usage-based fees (demand charge, VAT, meter rent) that split **equally across active members** instead of by kWh usage, surfaced in the settlement response as `FixedChargesTotal` and per-member `FixedChargeShare`. Already shipped and tested backend-side.
>
> **Update (2026-09-03):** Phase 7 was rebuilt from a two-endpoint CRUD stub into a full personal Budget & Expenses module — categories, tags, payment methods, recurring expenses, category-level budget envelopes with rollover/adjustment history, and a single dashboard endpoint that returns a complete budget-vs-actual picture in one call. **This is a breaking change** to the old `Expense`/`Budget` DTO shapes documented in the previous version of this section (`Category` is now `CategoryId`/`CategoryName`, `Period` is now `StartDate`/`EndDate`/`PeriodType`, routes moved from `/api/budget` to `/api/budgets`) — do not build against the old shapes. These remain **personal, per-user** endpoints (no household scoping, no `HouseholdId`), unlike every other module in this document.

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

### 0.6 The running ledger: how "this month" becomes "carried forward"

This household's real multi-month usage (traced from its own tracking spreadsheet — see analysis below) revealed a mechanic the original API/settlement design didn't account for: **balances are never reset to zero.** Every calendar month computes its own isolated numbers, and the *result* — not the inputs — carries forward into the next month as a starting balance. Two independent running totals exist:

1. **Per-member settlement balance** (who the household owes / who owes the household): `thisMonth.NetBalance = thisMonth's own settlement result + previousMonth's carried NetBalance`. This chains indefinitely — a member's true "settled position" as of today is the sum of every month's isolated result since the household started tracking, not one query spanning the whole history.
2. **Household shopping-fund balance** (unspent Bazar cash held over): `thisMonth.FundBalance = (thisMonth's total contributed − thisMonth's total spent) + previousMonth's carried FundBalance`. Recorded on the backend as an explicit **negative** `BazarPurchase` entry at month-end (e.g. `Amount: -700, Note: "Leftover"` — the API now accepts negative amounts specifically for this) and typically a corresponding positive entry folding that cash back into the next month's groceries.

**Why isolate each month's rate instead of just querying one wide `from`/`to` range?** `GET .../meals/rate?from=&to=` computes **one blended rate** over the entire queried window (`FoodCost / TotalMealUnits` for the whole range). Querying January 1 → February 28 in one call does **not** equal "January's isolated result + February's isolated result" — it mixes both months' food prices into a single average, misattributing cost between members who ate more in the cheaper vs. pricier month. The real household's spreadsheet deliberately avoids this: it computes a fresh rate every calendar month and only carries the *resulting dollar figure* forward, never the underlying rate inputs. **The frontend must replicate this by calling the settlement endpoints once per calendar month and summing results client-side — never by widening `from`/`to` across a month boundary and expecting the totals to match.**

This is exactly the shape a client-side store can own well — see Phase 5 for the concrete `householdLedgerStore` design that makes this the app's actual "who owes what, going all the way back" view instead of a single-period snapshot.

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

**Screens:** Household switcher/list, Create household, Household settings, Members list, Add member (direct), Invite member (QR + code), Join by code / invite landing, Change role, Leave household.

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
- **`POST .../members` (direct add) still requires an existing account** with that exact `Email`, or it 400s ("No account found with this email") — this is the "I know they're already on Koto Dibo" shortcut. For anyone else — the far more common case — use the **invite flow** in §1.1 instead; it's the one to build the primary "add someone to my household" UI against. Copy for the direct-add form should say "add an existing Koto Dibo user by email," not "invite by email."
- The Owner role can't be removed or role-changed via these endpoints (`DomainException` if attempted) — ownership transfer isn't implemented yet either, so hide "remove"/"change role" controls entirely for the Owner row.
- A Manager can't remove another Manager (only the Owner can) — disable that action client-side for Manager-viewing-Manager rows.
- If the Owner leaves while other active members remain, the call 400s ("Transfer ownership first") — if the Owner is the *last* active member, leaving auto-archives the household instead. Surface both outcomes distinctly in the confirmation dialog copy.
- Archiving a household should be a confirm-with-consequences dialog (blocks membership changes until restored, per `RequireActive` checks across every membership mutation).

### 1.1 Household Invites (QR + code) — replaces "just adding an email"

**Why this exists:** the direct `POST .../members` call above only works when the person you're adding already has a Koto Dibo account under that exact email — it can't be used to bring a friend onto the platform. The invite flow decouples "who I mean to invite" from "who actually redeems it": an invite is a **redeemable code**, generated by a household admin, that anyone holding it can exchange for membership — by typing the code in, or by scanning a QR that encodes a deep link containing it. The email on an invite (if any) is just where the inviter *intends* to send it — the backend never checks that the redeeming account's email matches it, because the whole point is supporting "I texted my roommate the code" or "she scanned my screen," not just "she happened to register with the email I typed."

**The two flows this API supports, both converging on the same `Accept` call:**

1. **Code, shared by the inviter through any channel.** Owner/Manager opens "Invite member," optionally types an email (for their own reference + an auto-sent email if provided), picks a role, and calls `Create`. The response has a short `Code` (e.g. `7K3PQXR9`) — show it big and copyable. The inviter sends it however they like (chat, SMS, verbally). The invitee opens the app, goes to "Join a household," types the code, and calls `Accept`.
2. **QR, scanned.** Same `Create` call also returns `QrCodeUrl` (a PNG hosted on Cloudflare R2/CDN — just an `<img src>`, no client-side QR rendering needed) and `InviteLink` (what the QR encodes: `{Invites:BaseUrl}/{code}`, e.g. `https://koto-dibo.ariyan.app/invites/7K3PQXR9`). Show the QR full-screen for the invitee to scan with their phone camera. Scanning opens that link in a browser/the installed PWA; the frontend route at `/invites/:code` should immediately resolve the code (§ below) and, once the user is authenticated, offer one-tap Accept — this is the "auto joins" path from the product spec, though UX-wise it should still be one confirming tap ("Join *The Bachelor House*?"), not silent, so a stray scan can't add someone to a household without them noticing.

Both paths end at the same two endpoints — build one "resolve + confirm + accept" screen and route both the manually-typed-code flow and the `/invites/:code` deep link into it.

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| POST | `/api/households/{id}/invites` | Owner/Manager | `{ Email?, Role, ExpiresInHours? }` | `HouseholdInviteDto` |
| GET | `/api/households/{id}/invites` | Owner/Manager | — | `HouseholdInviteDto[]` (pending only) |
| POST | `/api/households/{id}/invites/{inviteId}/revoke` | Owner/Manager | — | `204` |
| GET | `/api/invites/{code}` | Any authenticated user | — | `InvitePreviewDto` |
| POST | `/api/invites/{code}/accept` | Any authenticated user | — | `AcceptInviteResultDto` |

`HouseholdInviteDto`: `{ Id, HouseholdId, InvitedByUserId, Code, Role, Email?, Status, InviteLink, QrCodeUrl, ExpiresAt, CreatedAt }`. `Status` is `Pending | Accepted | Revoked | Expired`; `Role` excludes `Owner` (validation rejects it — ownership isn't transferable via invite). `ExpiresInHours` defaults to 168 (7 days) and is capped at 720 (30 days) server-side — no need to validate the cap client-side, but do surface the default in the create form.

`InvitePreviewDto`: `{ Code, HouseholdId, HouseholdName, Role, InvitedByName, Status, ExpiresAt, CallerIsAlreadyMember }`. Call this from the `/invites/:code` route (and from the "Join by code" form after the user types a code, before they commit) to render "**{InvitedByName}** invited you to join **{HouseholdName}** as a **{Role}**" plus an Accept button — *don't* call Accept sight-unseen from a raw deep-link open. `Status` here reflects the invite's *effective* status (a `Pending` invite past its `ExpiresAt` previews as `Expired` even though nothing was written yet) — branch UI copy on it: `Accepted` → "already used," `Revoked` → "no longer valid," `Expired` → "expired, ask for a new one," `Pending` with `CallerIsAlreadyMember: true` → "you're already in, go to the household" (no Accept button), `Pending` otherwise → show the Accept button.

`AcceptInviteResultDto`: `{ HouseholdId, HouseholdName, Member: HouseholdMemberDto }` — enough to navigate straight into the joined household without a follow-up `GET`.

**Error handling specific to this flow** (all still the standard §0.3 envelope):
- `404` on `GET/POST /api/invites/{code}...` means the code literally doesn't exist (typo) — distinct from an expired/revoked/accepted code, which 200s from Preview with a status, or 400s from Accept with a specific message (`"This invite has already been used."` / `"has been revoked."` / `"has expired."` / `"You are already a member of this household."`) — show `title` as-is.
- Codes are case-insensitive server-side (normalized to uppercase) — don't force-uppercase the input field, but do trim whitespace before submit (a common paste artifact from sharing over chat).
- The invite-management list (`GET .../invites`) only returns `Pending` rows *as stored* — a row can still be effectively expired (`ExpiresAt` in the past) without its `Status` having flipped yet, since that only happens lazily when someone actually hits Accept. Compute `isExpired = new Date(ExpiresAt) < new Date()` client-side for that list's display instead of trusting `Status === 'Pending'` alone, and hide/disable "Revoke" for rows that are already past `ExpiresAt`.
- Revoking only works on a `Pending` invite (400s otherwise) — hide the Revoke action for any row your own `isExpired` check above already flags.

**Unauthenticated scan handling:** every endpoint above requires a JWT, including `GET /api/invites/{code}`. If `/invites/:code` is opened while logged out (the realistic QR-scan case — a new user's phone has no session yet), route through Register/Login first, **preserving the code** (e.g. a query param or session-storage stash), then resolve the Preview automatically once auth completes and land the user back on the confirm screen instead of the household switcher.

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
- `Date` cannot be in the future (validate client-side before submit to save a round trip; server 400s on `date` field otherwise). `Amount` must be **non-zero** on Bazar entries — it can be negative (see below); on Contributions it must be `> 0`. `Currency` is a free 3-letter code validated as `^[A-Za-z]{3}$` — default the field to a constant (e.g. `"BDT"`) with the option to override, since there's no household-level base-currency setting yet.
- `status` filter query param takes the literal enum string (`Active`/`Cancelled`); omit it to get both.
- Cancelled entries are **soft-deleted, never removed** — the list endpoint returns them too (unless filtered out), so render them struck-through/greyed rather than expecting them to disappear.
- Edit/Cancel buttons: gate per §0.4 — a Member only sees them on their own entries; Owner/Manager see them on all.
- `PurchasedByUserId`/`ContributedByUserId` are set server-side from the caller's token on create — never a form field.

**Bazar's negative-amount "leftover" entry** (the mechanic behind §0.6's fund-balance carry-forward): `Amount` on a Bazar purchase can be negative — this is how unspent shopping cash held over to next month gets recorded, deflating that month's `FoodCost` baseline by exactly the unspent amount. This is a real, load-bearing part of the workflow (traced from the household's own multi-month tracking spreadsheet), not an edge case to merely tolerate. Design for it explicitly:
- Offer a distinct **"Record leftover"** action alongside (not merged into) the normal "Add purchase" flow — same `POST` call underneath, but pre-fills the sign as negative, requires a `Note` (otherwise optional), and uses different copy/icon so it reads as "returning unspent cash," not "a purchase that happened to cost less than zero." A plain amount field where someone can accidentally type a stray minus sign is worse UX than a dedicated toggle.
- Render these differently from ordinary purchases in list/history views (a distinct row style or badge) — a reader should be able to tell "this cut the food-cost baseline" apart from "this added to it" at a glance.
- The natural moment to prompt this is **month-end** — ideally surfaced from the Phase 5 ledger view once the current month is about to close (§0.6), rather than left for someone to remember unprompted.
- Contributions do **not** have this mechanic — `Amount` stays `> 0` there. Carry-forward is a Bazar/food-cost concept only; per-member settlement carry-forward (§0.6) is computed, never recorded as a ledger row.

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

**Display naming — do this in the UI layer only, not the API:** the backend's `SplitMethod` enum value stays `TariffMetered` (it names the algorithm; the API contract doesn't change), but every user-facing label must read **"Electricity Bill (Postpaid)"**, not "Tariff Metered." Maintain a single label-mapping table in the frontend (e.g. `{ TariffMetered: "Electricity Bill (Postpaid)", EqualSplit: "Equal Split", WeightedSplit: "Weighted Split" }`) and route every place a method name is shown — the picker, the list, the detail header, filter dropdowns — through it, rather than string-matching or hardcoding the label in more than one place. "Postpaid" is a deliberate qualifier: it distinguishes this from Bangladesh's common prepaid electricity meters, which this progressive-band, bill-after-usage model doesn't apply to — don't drop it from the label.

**Screens:** Bill split list (filter by period/status), Create bill split (method-aware form — see the fixed-charges sub-section below for the Electricity Bill form specifically), Bill split detail + settlement (band-breakdown table + fixed-charges breakdown for Electricity Bill (Postpaid); simple per-member share list for `EqualSplit`/`WeightedSplit`), Edit, Cancel.

| Method | Path | Request | Response |
|---|---|---|---|
| POST | `/api/households/{householdId}/bill-splits` | `CreateBillSplitRequest` | `BillSplitDto` |
| GET | `/api/households/{householdId}/bill-splits?from=&to=&status=` | — | `BillSplitDto[]` |
| GET | `/api/households/{householdId}/bill-splits/{id}` | — | `BillSplitDto` |
| PATCH | `/api/households/{householdId}/bill-splits/{id}` | `UpdateBillSplitRequest` | `BillSplitDto` |
| POST | `/api/households/{householdId}/bill-splits/{id}/cancel` | — | `BillSplitDto` |
| GET | `/api/households/{householdId}/bill-splits/{id}/settlement` | — | `BillSplitSettlementDto` |

**`CreateBillSplitRequest`**: `{ Title, SplitMethod, PeriodFrom, PeriodTo, Currency, TariffCountry?, TariffProvider?, MainMeterUsage?, TotalAmount?, MemberInputs: [{ UserId, Value }], FixedCharges: [{ Label, Amount }], Notes? }`. `SplitMethod` is one of `TariffMetered | EqualSplit | WeightedSplit` (display names per above).

**Per-method required fields** (validated server-side — mirror in the form so invalid combos never reach submit). **`EqualSplit` and `WeightedSplit` are unchanged from before** — only the `TariffMetered`/"Electricity Bill (Postpaid)" column gained the `FixedCharges` row:

| Field | Electricity Bill (Postpaid) — `TariffMetered` | Equal Split | Weighted Split |
|---|---|---|---|
| `TariffCountry` | required | — | — |
| `MainMeterUsage` | required, ≥ 0 | — | — |
| `TotalAmount` | *not accepted* (computed from tariff bands + fixed charges at settlement time) | required, > 0 | required, > 0 |
| `MemberInputs` | required; `Value` = sub-meter usage per member, ≥ 0, **sum must be ≤ `MainMeterUsage`** | ignored (splits across current active members automatically) | required; `Value` = fixed weight per member, > 0 |
| `FixedCharges` | optional; each `{ Label, Amount > 0 }` | *not accepted — ignored if sent, never persisted* | *not accepted — ignored if sent, never persisted* |

**`UpdateBillSplitRequest`** (PATCH, partial): `{ Title?, MainMeterUsage?, TotalAmount?, MemberInputs?, FixedCharges?, Notes? }`. **`SplitMethod`, `PeriodFrom`/`PeriodTo`, `Currency`, `TariffCountry`/`TariffProvider` are immutable** — disable those fields entirely in the edit form; a method change means creating a new bill split, not editing this one. `FixedCharges` follows `MemberInputs`'s patch semantics: omit the field to leave existing charges untouched, send a full replacement array to change them (there's no add/remove-one-charge endpoint — the client sends the whole list).

**`BillSplitDto`**: `{ Id, HouseholdId, CreatedByUserId, Title, SplitMethod, PeriodFrom, PeriodTo, Currency, TariffCountry?, TariffProvider?, MainMeterUsage?, TotalAmount?, MemberInputs, FixedCharges: [{ Label, Amount }], Notes?, Status, CreatedAt, UpdatedAt }`.

**`BillSplitSettlementDto`** (computed fresh on every call — nothing is cached server-side, refetch after any edit):
```
{
  BillSplitId, TotalAmount, AttributedCost, SharedCost, FixedChargesTotal,
  Bands: [{ FromUnits, ToUnits, RatePerUnit, UnitsInBand, AttributedUnits, SharedUnits, Cost }],
  Members: [{ UserId, Usage, AttributedCost, SharedCost, FixedChargeShare, TotalOwed }],
  CalculationVersion
}
```

Notes:
- `AttributedCost` = the portion driven by members' own sub-meter usage (allocated proportionally to `Usage`); `SharedCost` = common-area usage no sub-meter captures, split **equally across the household's current active members** (not just those with a sub-meter reading) — a member with `Usage: 0`/no reading still gets a `SharedCost` share and appears in `Members`.
- **`FixedChargesTotal`/`FixedChargeShare` are new — the "Add a constant fee" feature.** `FixedCharges` on the request (e.g. `{ Label: "Demand Charge", Amount: 300 }`, `{ Label: "VAT", Amount: 150 }`) are line items that are **not** usage-based — they're billed per connection, so their total is split **equally across active members**, same mechanism as `SharedCost` but tracked as a separate number so the settlement stays itemized. `TotalOwed = AttributedCost + SharedCost + FixedChargeShare` — every member owes their `FixedChargeShare` regardless of how much (or how little) electricity they personally used, including a member with zero metered usage.
- The band table is the "why did this cost so much" visualization for the metered portion — for each band show rate, total units consumed, and the attributed-vs-shared split within that band (a stacked bar or the reference repo's "flip card" pattern both work). The most expensive bands get attributed to metered usage first — expect (and design for) most/all of the top band's cost landing on `AttributedCost`, with only the cheapest band's leftover typically falling into `SharedCost`. **Show `FixedChargesTotal` as a separate line beneath the band table, itemized by `FixedCharges[*].Label`** — don't fold it into the band visualization, since it isn't usage-based and would misleadingly suggest it's part of the kWh cost.
- **Create/edit form for Electricity Bill (Postpaid):** after the sub-meter reading inputs, offer a repeatable "Add a fee" row (label text + amount) for `FixedCharges` — common presets worth offering as quick-add chips: "Demand Charge," "VAT," "Meter Rent" — but keep the field freeform since utility line items vary by provider. Each row needs both a non-empty label and a positive amount before it's included; an empty list is valid (not every bill has extra fees).
- Only `"BD"` is seeded as a valid `TariffCountry` at MVP (§0.5.2) — hardcode it as the only option, don't build a country picker yet.
- Ownership/edit rules match Phase 2 exactly (creator, or Owner/Manager, can edit/cancel; `Status: Cancelled` entries are never edit-able again).
- Bill split settlements feed into the running ledger the same way meal settlements do (§0.6/§5.2) — `BillSplitSettlementDto.Members[*].TotalOwed` (now inclusive of `FixedChargeShare`) is one of the two inputs `cumulativeMemberBalance` sums per period.
- **Out of scope for now, flagged for later:** the real household's spreadsheet actually splits electricity per-flat with separate AC-unit vs. normal-unit rates, and bills some utility line items across only a subset of members (not the whole household) rather than the full active member list `TariffMetered`/`EqualSplit` assume today. Don't build against this — it's not a stated requirement and the spreadsheet's own formulas for it are ad hoc — but if multi-flat billing becomes a real ask, it'll need a "which members does this specific bill apply to" concept that the current `BillSplit` model doesn't have (it always splits shared cost, and now fixed charges too, across *all* active household members).

---

## Phase 5 — Unified Settlement Dashboard & Running Ledger

The household's home screen once selected. Two views, not one: **"this month"** (a single-period snapshot, straight from the API) and **"running ledger"** (the cumulative, carried-forward view described in §0.6 — what members actually mean when they ask "how much do I owe the house *overall*"). The real household's own tracking spreadsheet is built entirely around the second view — don't ship only the first.

### 5.1 This month — single-period snapshot

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

`NetBalance = MealGiveTake − BillSplitOwed` (positive = household owes them, negative = they owe the household). Call this once with `from`/`to` set to the **current calendar month only** — per §0.6, widening the range across a month boundary blends meal rates incorrectly, so this endpoint is only meaningful evaluated one month at a time.

Notes:
- This endpoint internally recomputes the meal rate **and** every active bill split's settlement for the period server-side — it's the most expensive call in the API. Show a skeleton, not a spinner, and don't poll it aggressively.
- It gives **totals only** — no day-level or band-level detail. Every member row should link out to the Phase 3 meal settlement and the relevant Phase 4 bill splits for the "why" breakdown; don't try to cram that detail into this screen.
- Natural default landing route: after login → household selected → this dashboard, defaulting `from`/`to` to the current calendar month.

### 5.2 The running ledger — `householdLedgerStore`

This is the "make the store more awesome" piece: a client-side store that turns a sequence of single-month snapshots into the same running, never-reset balance the household already keeps by hand. No backend change is needed for this — it's pure client-side aggregation over calls the API already supports (see §0.6 for why per-month calls, not one wide-range call, are required for correctness).

**State shape** (per household; adapt the literal syntax to whichever state library you pick):

```
{
  periods: {
    "2026-01": { status: "closed", meal: MealCalculationDto, billSplits: BillSplitSettlementDto[], fetchedAt },
    "2026-02": { status: "closed", meal: MealCalculationDto, billSplits: BillSplitSettlementDto[], fetchedAt },
    "2026-03": { status: "open",   meal: MealCalculationDto, billSplits: BillSplitSettlementDto[], fetchedAt },
  },
  earliestPeriod: "2026-01"   // the household's first tracked month — stop paginating backward here
}
```

**Fetch strategy:**
- One `GET .../meals/rate?from=<month start>&to=<month end>` per calendar month, plus `GET .../bill-splits?from=&to=&status=Active` for that month and one `GET .../{id}/settlement` per result. Never one call spanning multiple months (§0.6).
- Fetch **lazily, backward from the current month**, as the user scrolls into history — don't eagerly fetch a household's entire lifetime on first load. `earliestPeriod` comes from whichever is earliest of: the household's `CreatedAt` (Phase 1's `HouseholdDto`), or simply "stop paginating once a fetched month has zero meal entries and zero bill splits."
- **Cache aggressively for closed months, never for the open one.** A month is "closed" once it's entirely in the past relative to today's date — its numbers can't change (barring a rare late edit to a previously-cancelled entry, which a manual "refresh this month" action can handle — don't build automatic invalidation for it). The current month is always "open": refetch it on screen focus and after every mutation in Phases 2–4 (new bazar/meal/contribution/bill-split entry, edit, or cancel).

**Derived selectors** (pure reductions over cached `periods`, recomputed on read — never stored redundantly):
- `cumulativeMemberBalance(userId, asOfMonth)` = Σ over every cached period up to and including `asOfMonth` of `(period.meal.Members[userId]?.GiveTake ?? 0) − Σ(period.billSplits[*].Members[userId]?.TotalOwed ?? 0)`.
- `cumulativeFundBalance(asOfMonth)` = Σ over every cached period up to `asOfMonth` of `(period.meal.TotalContributions − period.meal.FoodCost)`. This is the number that should match whatever a manually-recorded "Leftover" Bazar entry says at month-end (§Phase 2) — **surface both side by side as a reconciliation check**, not just one or the other; a mismatch means someone forgot to record a leftover entry, or recorded the wrong amount.
- Both selectors are cheap once the relevant months are cached — switching which month the user is "viewing as of" never needs a refetch.

**Screens built on this store:**
- **Household dashboard (default landing route):** this month's snapshot (§5.1) as the headline, plus each member's `cumulativeMemberBalance` as of the current month as a secondary "lifetime" figure — mirrors the spreadsheet's two-numbers-per-person layout (this month's Give/Take, and the running total).
- **Ledger / History screen:** a per-member table or line chart of `cumulativeMemberBalance` across every cached month, scrollable back to `earliestPeriod` — the "how did we get here" view. This is the screen the running-ledger design exists to support; don't skip it in favor of just the dashboard headline.
- **Month-end reconciliation prompt:** when the current month is within its last few days, surface `cumulativeFundBalance` next to a prompt to record it as a Bazar "leftover" entry (§Phase 2) if it hasn't been recorded yet for that month.

**Known gap this exposes:** there's no backend endpoint that returns this cumulative view directly — the store above does real aggregation work client-side, by design, for MVP. If a household's history grows to years of months and the N+1 fan-out (one settlement call per cached month) becomes a real cost, that's the signal to request a backend `GET api/households/{id}/settlement/cumulative?asOf=` that does the same summation server-side — not before.

---

## Phase 6 — PWA & offline polish

Cross-cutting, layered in once Phases 0–5 work online. Highest offline value is meal entry (per `MVP_BLUEPRINT.md` §6) — bazar/meal logging happens in-flat with patchy connectivity.

- Installable manifest + service worker (Vite PWA plugin).
- Offline queue specifically for `PUT/DELETE .../meals/{date}/{userId}` — optimistic UI update immediately, queue the request, replay in order on reconnect. Because the endpoint is a pure upsert keyed by `(household, date, userId)`, replay is naturally last-write-wins with no server-side conflict resolution needed — just replay queued mutations in the order they were made.
- Persist the refresh token in IndexedDB (not memory-only) so auth survives a PWA process kill/relaunch (§0.1).
- Everything else (ledgers, bill splits, settlement) can stay online-only at MVP — don't over-invest in offline support for screens that aren't the "in-flat, patchy connectivity" use case.

---

## Phase 7 — Personal Expenses & Budget

Unlike every other module in this document, this whole phase is **not household-scoped** — nothing here carries a `HouseholdId`; each record belongs to the caller alone, identified from the JWT. There's no household switcher dependency and no `HouseholdRolePolicy`/`CallerRole` gating (§0.4 doesn't apply) — every endpoint just operates on "my own records." Build this as a standalone "Personal Finance" section of the app, not nested under the household context Phases 1–5 share.

Five sub-modules, all under `/api/expense-categories`, `/api/expenses`, `/api/recurring-expenses`, `/api/budgets`, `/api/budget-dashboard`:

### 7.1 Expense categories

A shared, seeded system default tree (`Housing`, `Food` → `Groceries`/`Restaurants`/`Fast Food`/`Coffee`, `Transportation` → `Fuel`/`Ride Sharing`/`Public Transport`/`Maintenance`, `Utilities`, `Healthcare`, `Education`, `Entertainment`, `Shopping`, `Travel`, `Subscriptions`, `Personal Care`, `Insurance`, `Debt Payments`, `Family`, `Other`) that every user sees, plus each user's own custom categories layered on top.

| Method | Path | Request | Response |
|---|---|---|---|
| GET | `/api/expense-categories?includeInactive=` | — | `ExpenseCategoryDto[]` |
| POST | `/api/expense-categories` | `CreateExpenseCategoryRequest` | `ExpenseCategoryDto` |
| PATCH | `/api/expense-categories/{id}` | `UpdateExpenseCategoryRequest` | `ExpenseCategoryDto` |
| DELETE | `/api/expense-categories/{id}` | — | `204` (soft — sets `IsActive: false`; the caller must own it, system defaults can't be deleted) |

`ExpenseCategoryDto`: `{ Id, ParentCategoryId, Name, Icon, IsSystemDefault, IsActive }` — the list comes back flat; reconstruct the parent/child tree client-side via `ParentCategoryId`. `CreateExpenseCategoryRequest`: `{ Name, ParentCategoryId?, Icon? }`. A deactivated category still resolves correctly on old expenses/budgets that reference it (they carry a `CategoryName` snapshot too), so deleting a category is always safe.

### 7.2 Expenses

Full CRUD with server-side filtering, sorting, and **pagination** (the first paginated list endpoint in this API — expect `{ Items, Page, PageSize, TotalCount, TotalPages }`, not a bare array).

| Method | Path | Request | Response |
|---|---|---|---|
| GET | `/api/expenses?...` | query params below | `PagedResult<ExpenseDto>` |
| GET | `/api/expenses/{id}` | — | `ExpenseDto` |
| POST | `/api/expenses` | `CreateExpenseRequest` | `ExpenseDto` |
| PATCH | `/api/expenses/{id}` | `UpdateExpenseRequest` (all fields optional) | `ExpenseDto` |
| DELETE | `/api/expenses/{id}` | — | `ExpenseDto` (soft-deleted, `Status: "Cancelled"`) |

List query params: `categoryId`, `from`, `to`, `minAmount`, `maxAmount`, `merchant` (substring, case-insensitive), `paymentMethod` (`Cash`/`BankAccount`/`CreditCard`/`MobileWallet`/`Other`), `tag`, `isRecurring` (bool), `search` (matches description/merchant/category), `sortBy` (`Date`/`Amount`/`CreatedAt`/`Merchant`/`Category`, default `Date`), `sortDescending` (default `true`), `page` (default `1`), `pageSize` (default `20`, max `100`).

`ExpenseDto`: `{ Id, Amount, Currency, CategoryId, CategoryName, Merchant, Description, Notes, Date, PaymentMethod, Tags[], ReceiptUrl, RecurringExpenseId, IsRecurringGenerated, Status, CreatedAt, UpdatedAt }`. `CreateExpenseRequest`: `{ Amount, Currency?, CategoryId, Merchant?, Description?, Notes?, Date, PaymentMethod?, Tags?, ReceiptUrl? }` — `Currency` defaults to `"BDT"` when omitted; `PaymentMethod` defaults to `"Cash"`.

Notes:
- `Amount` must be `> 0`; `Date` cannot be in the future (same posture as every other ledger in this app).
- `CategoryId` is required and validated against the caller's visible categories (system defaults + their own) — there's no uncategorized-expense concept at MVP.
- `Tags` are free-text, deduplicated case-insensitively server-side, max 20 per expense, ≤50 chars each.
- Edit/delete both 404 the same way as everywhere else when the expense doesn't exist or belongs to someone else; editing/deleting an already-deleted expense 400s.

### 7.3 Recurring expenses

A template a background sweep (every 30 min server-side) turns into real `Expense` rows as they come due — generation is idempotent, so a client-triggered catch-up never double-creates.

| Method | Path | Request | Response |
|---|---|---|---|
| GET | `/api/recurring-expenses?includeInactive=` | — | `RecurringExpenseDto[]` |
| GET | `/api/recurring-expenses/{id}` | — | `RecurringExpenseDto` |
| POST | `/api/recurring-expenses` | `CreateRecurringExpenseRequest` | `RecurringExpenseDto` |
| PATCH | `/api/recurring-expenses/{id}` | `UpdateRecurringExpenseRequest` | `RecurringExpenseDto` |
| POST | `/api/recurring-expenses/{id}/deactivate` | — | `RecurringExpenseDto` |
| POST | `/api/recurring-expenses/generate-due` | — | `ExpenseDto[]` (newly materialized occurrences, if any) |

`CreateRecurringExpenseRequest`: `{ Amount, Currency?, CategoryId, Merchant?, Description?, Notes?, PaymentMethod?, Tags?, Frequency, StartDate, EndDate? }` — `Frequency` is one of `Daily`/`Weekly`/`Biweekly`/`Monthly`/`Quarterly`/`Yearly`. `RecurringExpenseDto` additionally exposes `NextOccurrenceDate` and `LastGeneratedDate` — useful for a "next charge" chip on the recurring-expenses list without waiting on the dashboard. Frequency and StartDate are immutable after creation (deactivate and create a new one to change cadence); everything else is PATCH-able.

### 7.4 Budgets

A `Budget` is one period's envelope (e.g. "January 2026"), not a single ongoing record — create a new one each month/week/year. Each carries zero or more `BudgetCategoryAllocation` "envelopes," and every planned/spent/remaining/variance/usage% number is computed live from the caller's `Expense` records within that budget's date range — nothing here is a stale cached total.

| Method | Path | Request | Response |
|---|---|---|---|
| GET | `/api/budgets?status=&from=&to=` | — | `BudgetSummaryDto[]` |
| GET | `/api/budgets/{id}` | — | `BudgetDto` (full, with per-category breakdown) |
| POST | `/api/budgets` | `CreateBudgetRequest` | `BudgetDto` |
| PATCH | `/api/budgets/{id}` | `UpdateBudgetRequest` | `BudgetDto` |
| POST | `/api/budgets/{id}/categories` | `AddBudgetCategoryRequest` | `BudgetDto` |
| POST | `/api/budgets/{id}/categories/{allocationId}/adjust` | `AdjustBudgetCategoryRequest` | `BudgetDto` |
| POST | `/api/budgets/{id}/categories/{allocationId}/transfer` | `TransferBudgetCategoryRequest` | `BudgetDto` |
| GET | `/api/budgets/{id}/categories/{allocationId}/adjustments` | — | `BudgetAdjustmentDto[]` (full history, newest first) |
| POST | `/api/budgets/{id}/rollover` | `RolloverBudgetRequest` | `BudgetDto` (the new, next-period budget) |

`CreateBudgetRequest`: `{ Name, Description?, Currency?, PeriodType, StartDate, EndDate?, Notes?, Categories?: [{ CategoryId, PlannedAmount, RolloverEnabled, Notes? }] }` — `PeriodType` is `Weekly`/`Monthly`/`Yearly`/`Custom`; `EndDate` is auto-derived from `StartDate` for the first three and **required** for `Custom`. New budgets start in `Draft` status.

`BudgetDto`: `{ Id, Name, Description, Currency, PeriodType, StartDate, EndDate, Status, Notes, TotalPlanned, TotalRollover, TotalAvailable, TotalSpent, TotalRemaining, TotalOverspent, UtilizationPercentage, Health, Categories: BudgetCategoryDto[], CreatedAt, UpdatedAt }`. Each `BudgetCategoryDto`: `{ Id, CategoryId, CategoryName, PlannedAmount, RolloverEnabled, RolloverAmount, TotalAvailable, Spent, Remaining, Variance, UsagePercentage, Status, Notes }` — `Status` is one of `NoBudget`/`OnTrack`/`Warning`/`Overspent`; the budget-level `Health` is one of `NoBudget`/`Healthy`/`Warning`/`Overspending`/`Critical`. `UsagePercentage`/`UtilizationPercentage` are `null` (not `0`) when nothing was ever allocated — render "—" rather than "0%".

`Status` transitions are enforced server-side: `Draft → Active|Archived`, `Active → Completed|Archived`, `Completed → Archived`, and `Archived` is terminal — gate the status dropdown accordingly rather than discovering the 400 at submit time. An `Archived` budget also rejects any category add/adjust/transfer call.

`AdjustBudgetCategoryRequest`: `{ Delta, Reason? }` — a signed delta (positive = increase, negative = decrease) against the category's `PlannedAmount`, not an absolute new value; every adjustment is appended to that category's history rather than silently overwriting it. `TransferBudgetCategoryRequest`: `{ ToCategoryAllocationId, Amount, Reason? }` — moves `Amount` from the URL's category into another category in the same budget (both legs show up in each side's adjustment history as `TransferOut`/`TransferIn`).

`RolloverBudgetRequest`: `{ Name?, StartDate?, EndDate? }` (all optional — omitted, the next period is auto-computed from the current budget's `PeriodType`) creates the next period's budget, copying every category's base `PlannedAmount` forward and, for categories with `RolloverEnabled: true`, carrying that category's current `Remaining` (which **can be negative** — an overspent envelope's deficit follows into the next period rather than vanishing) into the new period's `RolloverAmount`. Categories with `RolloverEnabled: false` roll the base amount forward with `RolloverAmount: 0`.

### 7.5 Dashboard — the main entry point

One call returns everything a personal finance dashboard screen needs — no client-side sum/percentage/variance math required.

| Method | Path | Response |
|---|---|---|
| GET | `/api/budget-dashboard?preset=&from=&to=&budgetId=&currency=&comparisonPeriod=` | `DashboardResponse` |

Query params (all optional): `preset` (`Today`/`ThisWeek`/`ThisMonth`/`LastMonth`/`ThisYear`/`Custom`, default `ThisMonth`) is overridden by explicit `from`/`to` if either is given; `budgetId` pins the `budget`/`categoryBreakdown` sections to one specific budget instead of whichever one overlaps the resolved period; `currency` filters expenses to one currency; `comparisonPeriod` (`None`/`PreviousPeriod`/`SamePeriodLastYear`, default `PreviousPeriod`) controls the `comparison` section.

`DashboardResponse` shape:
```
{
  period: { from, to, preset },
  summary: { totalBudget, totalAllocated, totalSpent, totalRemaining, totalOverspent, budgetUtilizationPercentage, expenseCount, averageExpense },
  budget: { hasBudget, id, name, status, health },
  expenses: { count, totalAmount, averageAmount, recentExpenses: ExpenseDto[] },  // last 5
  budgetVsActual: [{ label, budget, actual }],       // one point per calendar month the range touches
  categoryBreakdown: [{ categoryId, categoryName, budget, spent, remaining, variance, utilizationPercentage, status, percentageOfTotalSpending }],
  spendingTrend: [{ date, amount }],                 // daily (<=31d span), weekly (<=182d), else monthly buckets
  topCategories: [{ categoryId, categoryName, amount, percentageOfTotal }],   // top 10
  topMerchants: [{ merchant, amount, transactionCount, percentageOfTotal }], // top 10
  overspending: [ ...same shape as categoryBreakdown, filtered to status "Overspent" ],
  upcomingExpenses: [{ recurringExpenseId, description, merchant, categoryName, amount, nextOccurrenceDate, daysUntilDue }], // next 30 days, max 10
  comparison: { previousFrom, previousTo, currentSpending, previousSpending, spendingChange, spendingChangePercentage, currentBudget, previousBudget, budgetChange, budgetChangePercentage, trend } | null,
  insights: { highestSpendingCategory, mostFrequentCategory, highestExpenseAmount, highestExpenseDescription, averageExpense, overspendingCategoriesCount, categoriesApproachingLimit: string[], categoriesSignificantlyUnderBudget: string[], recurringExpensesTotal, fixedExpensesTotal, variableExpensesTotal },
}
```

Notes:
- Every section respects the **same resolved `[from, to]` window** — there's no hidden second date range. If you query "This Week," `categoryBreakdown` shows this week's spend against the matched budget's envelope totals (a week-to-date progress bar against a monthly target — the standard pattern, not a bug).
- `budget.hasBudget: false` (no budget overlaps the period, or none exists yet) is a normal response, not an error — render an empty/"set up a budget" state rather than treating it as a failure. Same for zero expenses: every numeric field comes back as `0`, never `null`, except percentage fields which are `null` specifically when nothing was ever budgeted (`totalAvailable == 0`).
- `comparison` is `null` only when `comparisonPeriod=None` was explicitly requested.
- `trend` is `"Increased"`/`"Decreased"`/`"Stable"` — a swing within ±2% reads as `"Stable"` rather than a false-positive trend arrow.

---

## Suggested build order

```
Phase 0 (foundation & auth)
  └─→ Phase 1 (households)
        ├─→ Phase 2 (bazar + contributions)  ─┐
        ├─→ Phase 3 (meals)                   ├─→ Phase 5.1 (this-month snapshot) ─→ Phase 5.2 (running ledger store)
        └─→ Phase 4 (bill splits)            ─┘
                                                    Phase 6 (PWA/offline) — layer in continuously
                                                    Phase 7 (personal expenses/budget) — standalone, no longer blocked;
                                                    only depends on Phase 0 auth, can build any time after that
```

Phases 2, 3, and 4 don't share components beyond the §0 foundation and the generic ledger-entry pattern (Phase 2 reused loosely for Phase 4's flat-split methods) — they can be built in parallel by different people once Phase 1 lands. Phase 5.1 is a thin aggregation view with nothing to show until 2–4 have real data flowing through them, so it's deliberately last among the "first pass" phases. Phase 5.2 (the `householdLedgerStore` running ledger from §0.6) is its own follow-on slice — it only becomes useful once at least two calendar months of real data exist, so it's reasonable to ship 5.1 for the first month of real usage and land 5.2 shortly after, rather than blocking the whole dashboard on it. Phase 7 doesn't depend on Phase 1's household context at all (it's per-user, not per-household) — it can be built in parallel with Phases 1–5 by whoever has spare capacity, rather than sequenced after them.
