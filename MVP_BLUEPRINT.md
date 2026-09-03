# Koto Dibo? (কত দিবো?) — MVP Blueprint

> This document extends `BLUEPRINT.md` (a snapshot of what's built) into a phased delivery plan. `BLUEPRINT.md` stays as-is — a point-in-time system description. This file answers "what ships next, in what order, to reach a usable MVP," anchored on the two features that make the product useful on day one: **meal cost accounting** and **fair bill splitting**.

> **Status (2026-09-02):** Phases 2–4 are implemented backend-side — `FairSplitAllocator`, the full `BillSplit` CRUD + settlement API, and the unified `SettlementService` all ship with unit test coverage (see §3–§5 for what landed). Phases 1 and 5 (UI) are done — built in a separate frontend repository, outside this backend codebase, against the API surface documented in `MVP_FRONTEND_BLUEPRINT.md`. Phase 6's `Expenses`/`Budget` controllers are now wired end-to-end (see §7); the integration-test suite and speculative `KotoDibo.Common` work remain the only open items.

## 0. Why these two systems anchor the MVP

A bachelor household's shared-money pain has two distinct shapes, and they should not be forced into one model:

1. **Meal cost** — a *consumption-tracked, cash-pooled* cost. Everyone throws money into a pot (Contributions), someone spends from it (Bazar), and cost is recovered per member by how many meals they actually ate. This is **already fully implemented** (`MealCostAllocator`, `MealCalculationService`, `MealsController`).
2. **Fair bill split** — a *usage-metered, tariff-aware* cost. A shared utility bill (electricity, most commonly) is billed to the whole flat under a **progressive tariff** (cost per unit rises in bands), but each member's sub-meter shows their own usage. Splitting by simple proportion is *wrong* — it ignores the fact that heavy total usage pushes the household into expensive upper bands, and correctly attributing that expensive usage to the person who actually caused it needs a real allocation algorithm. This is the exact problem `FairSplit` (the reference repo) solves in isolation, and it maps directly onto the **currently-unwired `BillSplit` entity** in Koto Dibo's backend.

Both systems ultimately answer the same question — *"who owes/is owed how much, and why"* — from different raw inputs. The phases below build fair-split as a first-class feature using the same architectural pattern the meal system already proved out, then unify both under one settlement view.

## 1. Baseline — what Phase 0 already ships (no new work)

Carried over from `BLUEPRINT.md` §3, restated here as the MVP's foundation:

| Capability | Status |
|---|---|
| Auth (register/login/refresh/logout, lockout, token rotation+reuse detection) | ✅ Done |
| Households (CRUD, archive/restore, membership, roles, permissions) | ✅ Done |
| Bazar purchases (grocery ledger) | ✅ Done |
| Contributions (cash-in ledger) | ✅ Done |
| Daily meal entries (upsert-by-PUT per `(household, user, date)`) | ✅ Done |
| **Meal cost calculation** (`MealCostAllocator`, largest-remainder split) | ✅ Done |

### 1.1 Meal calculation — formalized as the reference algorithm

This is worth writing down explicitly, because Phase 2 (bill split) and Phase 4 (unified settlement) both reuse its shape. Using the worked example from this conversation as the spec:

- **Meal rate** = total food cost (Bazar spend, `Active` status only) over the period ÷ total meals eaten by all members over the same period.
- **Per-member meals eaten** = sum of `DailyMealEntry.Count` for that user across the range. A day marked "2x Meal" is the *household default* — it applies to every member on that day **unless** a member has an explicit override entry for that date (e.g. `0x Ariyan`, `4x Ariyan`, `1x Tanvir`). An explicit `0` means that member is excluded from that day's count entirely, not counted at a reduced rate.
- **Per-member cost owed** = their meal count × meal rate.
- **Balance** = their total cash contributed (Bazar spend they personally covered + Contributions) − cost owed. Positive = household owes them; negative = they owe the household.
- **Rounding**: because `rate × count` rarely lands on a clean currency unit, `MealCostAllocator` uses the **largest-remainder method** so individual balances always sum exactly to zero across the household — never a few cents lost to floating-point drift. Any new allocator (bill split included) must reuse this same rounding rule rather than re-deriving it, so the two ledgers stay numerically consistent.

This is already implemented correctly — Phase 1 below is about *exposing* it well, not re-solving it.

## 2. Phase 1 — Meal system: close the loop, don't leave it backend-only — ✅ Done

The meal engine is solid but currently only reachable via raw API calls. MVP-readiness means a household member can actually *use* it without Swagger.

**Scope:**
- Meal entry UI: a day-grid (like the "Aug 1 → Aug 31" log used in this conversation) where a manager sets the household default meal count per day, and any member can override their own count for that day.
- Settlement view: per-member contributed / meals eaten / cost owed / balance, sourced from `GET .../meals/rate`, rendered as a simple table + a "who owes whom" strip.
- Edge-case validation surfaced to the UI (not just the API): overrides on a day with no default set yet, negative counts, entries outside the household's active membership window for that user.

**Acceptance:** a household can run the exact August-2026 dataset from this conversation through the real UI and get the same meal rate and balances back.

**Status:** built — in a separate frontend repository, not this backend codebase — against the API surface `MVP_FRONTEND_BLUEPRINT.md` §Phase 3 documents.

## 3. Phase 2 — Fair-split domain model (port `TariffEngine` logic into `KotoDibo.Domain`) — ✅ Done

Implemented as specified: `FairSplitAllocator` (`KotoDibo.Domain/Calculations`), `UtilityTariffConfig`/`TariffBand` entities, `BillSplitMethod` enum (`TariffMetered`/`EqualSplit`/`WeightedSplit`), and an extended `BillSplit` entity carrying period, method, tariff reference, sub-meter/weight inputs and lifecycle status. A startup-idempotent `TariffConfigSeeder` seeds one illustrative Bangladesh electricity tariff schedule (clearly marked as reference rates — swap for real published BPDB/DPDC slabs before relying on it for real bills). `FairSplitAllocatorTests` (9 tests) verify the band-walk, expensive-bands-first attribution, and largest-remainder rounding against a hand-worked example.

This is the core new work. `FairSplit`'s `TariffEngine.ts` is a pure, dependency-free calculation module — same shape as `MealCostAllocator` — which is exactly why it ports cleanly into `KotoDibo.Domain` as a new calculator, no architecture change needed.

**New domain types** (`KotoDibo.Domain/Entities` + a new `Calculations/` folder, mirroring where `MealCostAllocator` lives):

- `UtilityTariffConfig` — country/provider-scoped progressive tariff bands (rate, from, to per band). Seed data: Bangladesh electricity bands, same shape as `bangladesh.json` in the reference repo, stored server-side (not hardcoded per-household) so it can be reused and updated centrally.
- `BillSplitRecord` (extends the existing but unwired `BillSplit` entity) — a household + billing-period record holding: total main-meter usage, total bill amount (or computed from tariff), and a per-member sub-meter usage entry.
- `FairSplitAllocator` (new, pure, external-dependency-free — same category as `MealCostAllocator`):
  - Computes the tariff stack for total household usage (progressive band walk).
  - Splits usage into **attributed** (sum of members' sub-meter readings) vs **shared** (main-meter minus sub-metered — common-area usage no one's sub-meter captures).
  - Attributes the *most expensive* bands to sub-metered usage first (ranked by rate, highest first) before falling back to cheaper bands — this is the part simple proportional splitting gets wrong, and it's the actual value `FairSplit` provides.
  - Allocates the attributed cost across members proportional to their sub-meter share, correcting rounding via the same largest-remainder approach as `MealCostAllocator`, and folds the shared/common cost in per the household's chosen split policy (equal split by default; see below).
  - Returns per-member: usage, attributed cost, share of shared cost, total owed.

**Split policy, generalized beyond electricity:** not every shared bill has sub-meters (rent, wifi, gas cylinder). `BillSplitRecord` should support a `SplitMethod` enum:
- `TariffMetered` — the algorithm above (needs sub-meter readings + tariff config).
- `EqualSplit` — flat divide across active members.
- `WeightedSplit` — fixed per-member weights/shares (e.g. room-size-based rent split).

This keeps the tariff engine as the flagship case (it's the hard one) while making `BillSplit` actually usable for the other recurring bills a household has.

**Acceptance:** unit tests (`FairSplitAllocatorTests`, alongside `MealCostAllocatorTests`) reproducing the reference repo's band-walking and proportional-allocation behavior against the same Bangladesh tariff fixture, plus a largest-remainder rounding test proving allocations sum exactly to the total bill.

## 4. Phase 3 — Application + API wiring (mirror the meal feature's shape exactly) — ✅ Done

`BillSplitService`/`BillSplitController` are fully wired, mirroring Bazar/Contribution's ownership-check pattern and Meal's on-demand settlement pattern exactly: `POST/GET/PATCH /api/households/{id}/bill-splits`, `POST .../{id}/cancel`, `GET .../{id}/settlement`. Five new `HouseholdPermission`s (`AddBillSplit`, `ViewBillSplit`, `UpdateBillSplit`, `CancelBillSplit`, `ViewBillSplitSettlement`) are wired into `HouseholdRolePolicy` per role. 12 service-level unit tests cover validation, permission/ownership checks, and settlement correctness (cross-checked against the same worked example as the domain tests).

Follow the established `Application/Features/<Name>/` pattern from `BLUEPRINT.md` §5 — `BillSplitService` already exists per the status table, so this phase is about finishing and wiring it, not building from scratch:

- `DTOs/` — `CreateBillSplitRequest` (period, `SplitMethod`, tariff/country reference, sub-meter readings per member), `BillSplitResultResponse` (mirrors `MealCalculationService`'s response shape: rate/band breakdown, per-member cost, balances).
- `IBillSplitService` / `BillSplitService` — finish wiring against `IHouseholdAccessService` permission checks, same as every other household-scoped service.
- `Validators/` — FluentValidation: sub-meter total ≤ main-meter total, non-negative usage, valid tariff/country reference, required fields per `SplitMethod`.
- `BillSplitController` — replace the `501` stub:
  - `POST /` — create a bill period record (readings + method).
  - `GET /` — list periods (filters: `from`, `to`).
  - `GET /{id}/settlement` — runs `FairSplitAllocator`, same role `GET .../meals/rate` plays for meals.
  - `PATCH /{id}`, `POST /{id}/cancel` — same lifecycle shape as Bazar/Contribution.

**Acceptance:** a household can log an electricity bill period with sub-meter readings and get back a correct per-member settlement via the API, with the same permission and validation rigor as the meal endpoints.

## 5. Phase 4 — Unified settlement view — ✅ Done

Implemented as a thin, additive `SettlementService`/`SettlementController` (`GET /api/households/{id}/settlement?from=&to=`) that composes `MealCalculationService` + `BillSplitService` output into one net-balance-per-member number, per the plan below — it doesn't touch either allocator's internals. 4 unit tests cover the aggregation.

Once both ledgers produce "who owes/is owed" balances independently, add a thin aggregation layer rather than making members reconcile two screens mentally:

- `SettlementService` (new, `Application`-layer, composes `MealCalculationService` + `BillSplitService` + raw `Contribution`/`Bazar` balances for a given period) → one combined per-member net balance across meals, bills, and any direct contributions not yet consumed.
- `GET api/households/{id}/settlement?from=&to=` — single endpoint, single number per member, single source of truth for "what do I actually owe this household right now."

This is additive and low-risk: it doesn't touch either allocator's internals, it just sums their outputs.

## 6. Phase 5 — Frontend / PWA — ✅ Done

> See `MVP_FRONTEND_BLUEPRINT.md` for the detailed module-by-module, API-by-API breakdown of this phase. Every backend endpoint it lists (auth, households, bazar, contributions, meals, bill-splits, settlement) is live as of the Phase 2–4 backend work above, so frontend work can start immediately without waiting on further backend changes.

**Status:** built in a separate frontend repository (Vite + React + TypeScript, per the stack below), outside this backend codebase's scope. `MVP_FRONTEND_BLUEPRINT.md` §Phase 7 documents the newly-wired Expenses/Budget API from §7 below, now that it's no longer blocked.

`BLUEPRINT.md` documents backend only — there's no client yet. Reference repo (`FairSplit`) is a good pattern source for the calculator-style UI specifically, not a template to clone wholesale:

- **Stack:** Vite + React + TypeScript (matches the reference repo, keeps the team on one frontend toolchain), installable as a PWA (offline-capable meal entry is the highest-value offline case — bazar/meal logging happens in-flat with patchy connectivity).
- **Meal module:** day-grid entry + settlement table (Phase 1 scope, UI half).
- **Bill split module:** borrow the reference repo's Calculator page interaction pattern — main-meter input, per-member sub-meter inputs, tariff band visualization (the "flip card" showing the band breakdown is a nice pattern worth keeping) — but wired to Koto Dibo's real API instead of `LocalStorageService`'s client-only session persistence.
- **Household dashboard:** the unified settlement view from Phase 4, plus Bazar/Contribution logs.
- Auth screens against the existing JWT + refresh-rotation flow.

**Explicitly deferred from MVP:** country/tariff picker beyond Bangladesh (reference repo's multi-country `countryApi.ts` is more general than this MVP needs — one seeded tariff config is enough to start).

## 7. Phase 6 — Remaining backend gaps (lower priority than Phases 2–5)

Carried over from `BLUEPRINT.md` §12, sequenced after the two anchor features:

- ~~Wire `Expenses` and `Budget` controllers to their existing (partial) Application services.~~ — **✅ Done.** Both were previously `501` stubs sitting in front of `NotImplementedException`-throwing services with empty validators. Unlike Bazar/Contribution/BillSplit, `Expense` and `Budget` are **personal, per-user records — not household-scoped** (no `HouseholdId` on either entity), so they're wired without `IHouseholdAccessService`/`HouseholdPermission` involvement; ownership is simply "caller's own `UserId`," enforced by `ExpenseService`/`BudgetService` on every read.
  - `ExpensesController` (`api/expenses`, `[Authorize]`): `POST /`, `GET /`, `GET /` accepts optional `from`/`to` query filters (same convention as Bazar/Contribution list endpoints), `GET /{id}`. `CreateExpenseRequestValidator` requires `Amount > 0`, non-empty `Category` (≤100 chars), `Description` ≤500 chars, and a non-default `Date`; the service additionally rejects future-dated entries (mirrors `BazarPurchaseService`'s `RequireNotFuture`, via the same `LocalDate.TodayFor` helper).
  - `BudgetController` (`api/budget`, `[Authorize]`): `POST /`, `GET /`, `GET /{id}`. `CreateBudgetRequestValidator` requires `Period` in `YYYY-MM` format and `Amount > 0`; `BudgetService` additionally rejects a second budget for the same `(UserId, Period)` pair (one budget per person per month).
  - `GetById` on both throws `NotFoundException` (mapped to `404` by the existing `ExceptionHandlingMiddleware`) when the record doesn't exist *or* belongs to another user — same "don't distinguish not-found from not-yours" posture as household resources.
  - Update/Delete were intentionally not added — out of scope for this pass, matching what the original stub controllers exposed. Add them as a follow-up if the frontend needs them.
  - 9 new unit tests (`ExpenseServiceTests`, `BudgetServiceTests`) cover create/validate/ownership-scoping; full suite (143 tests) and solution build both pass.
  - **Superseded (2026-09-03):** the two-endpoint CRUD stub above was rebuilt into a full production Budget & Expenses module — expense categories (seeded system defaults + user-created), tags/merchant/payment-method/receipt fields, soft-delete + PATCH, recurring expenses with idempotent background generation, period-scoped budgets with per-category envelopes (planned/rollover/spent/remaining/variance/usage%, auditable adjustment + transfer history, status lifecycle, rollover-to-next-period), and a single `GET /api/budget-dashboard` entry point (summary, budget-vs-actual, category breakdown, spending trend, top categories/merchants, overspending, upcoming recurring expenses, period comparison, computed insights). Routes moved from `api/budget` to `api/budgets` (plural, matching every other controller's convention) as part of the rewrite. See `MVP_FRONTEND_BLUEPRINT.md` §Phase 7 for the full, current client-facing API reference — treat everything above this note as historical.
- `KotoDibo.IntegrationTests` — first real integration test suite, should target meal settlement and fair-split settlement first since they're the highest-value correctness surfaces.
- `KotoDibo.Common` — fill in as real cross-cutting needs surface from Phases 2–5, not speculatively.
- ~~`HouseholdMembershipStatus.Invited` — token-based invitation flow~~ — **✅ Done**, shipped as a separate `HouseholdInvite` collection (code + QR, redeemed via `POST /api/invites/{code}/accept`) rather than a pending-membership-row/enum value — see `MVP_FRONTEND_BLUEPRINT.md` §1.1 for the full flow and API surface. This also introduced the backend's first CDN-backed storage integration (Cloudflare R2 via `IFileStorageService`, S3-compatible), used to host the generated invite QR PNGs.

## 8. Suggested sequencing

```
Phase 1 (meal UI)  ─┐
                     ├─→ Phase 5 (frontend shell shared by both) ─→ Phase 4 (unified settlement)
Phase 2 (fair-split  │        [built in a separate frontend repo]
  domain) → Phase 3  ┘        [Phases 1, 2, 3, 4, 5 all now ✅ Done]
  (fair-split API)
  [both now ✅ Done]
                                                                    Phase 6 (backend gaps) — Expenses/Budget ✅ Done;
                                                                    IntegrationTests + KotoDibo.Common remain, lowest priority
```

Phases 2→3 (fair-split) and Phase 1 (meal UI) ran in parallel — they don't share code. Phase 5 needed *something* from both to build against, so it trailed. Phase 4 came last: it's a thin layer that only makes sense once both underlying settlements are real and tested.

Backend-side, Phases 2, 3 and 4 are done. Phase 1 and Phase 5 (the UI) are also done — built in a separate frontend repository outside this backend codebase, against the API surface `MVP_FRONTEND_BLUEPRINT.md` documents. Of Phase 6's backend gaps, `Expenses`/`Budget` wiring is now done (§7); the integration test suite and speculative `KotoDibo.Common` work remain, still lowest priority.
