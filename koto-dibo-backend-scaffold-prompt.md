# Claude Code Prompt — Koto Dibo? Backend Scaffold (.NET 10, N-Tier, MongoDB)

Paste everything below into Claude Code in your cloned repo root.

---

## Context

I'm building the backend for **"Koto Dibo?" (কত দিবো?)** — a household finance PWA for bachelors in shared housing and low-income families in Bangladesh. This repo is the **backend only** (frontend is a separate React/Vite PWA repo, not part of this codebase).

- **Stack**: .NET 10 (LTS) Web API + MongoDB (via MongoDB.Driver, no EF Core — this is a NoSQL project, no relational ORM)
- **Architecture**: Classic N-Tier — Presentation (API) → Application → Domain, with Infrastructure implementing interfaces defined in Application/Domain
- **Auth**: JWT-based, self-hosted (no third-party auth provider)
- **Deploy target**: containerless deploy to Render's free tier, so keep `Program.cs` minimal-hosting-model friendly and configuration driven by environment variables + `appsettings.json`

## Domain model (for context — don't fully implement business logic yet, just scaffold)

- **User** — account, belongs to zero or one Household
- **Household** — group of Users, has an invite code/link
- **MealEntry** — a household member's daily meal count (household feature)
- **BazarEntry** — a shopping/grocery purchase logged against the household (household feature)
- **MealRate** — computed from MealEntries + BazarEntries over a period (household feature)
- **BillSplit** — splits a bill (e.g. electricity, fairly under progressive tariffs) either as a saved household record or a one-off anonymous calculation (no login required)
- **Expense** — individual (not household-scoped) expense tracking
- **Budget** — individual budget analysis, derived from Expenses

## What I need from you right now

**Scope: structure only.** Create the solution, all projects, all folders (with `.gitkeep` placeholders where a folder would otherwise be empty), correct project references, required NuGet packages, and minimal skeleton files (empty interfaces, a working `Program.cs`, DI registration stubs, placeholder configs). Do **not** implement real business logic, real MongoDB queries, or real JWT validation logic yet — stub methods with `throw new NotImplementedException()` or return placeholder types where a real implementation would go. I'll come back for feature-by-feature implementation afterward.

### 1. Solution & project layout

Create this exact structure (adjust only if you have a clearly better convention for .NET 10, but ask me first if you want to deviate):

```
KotoDibo.sln
src/
  KotoDibo.Api/                  # Presentation layer
    Controllers/
    Middleware/
    Extensions/                  # ServiceCollectionExtensions, WebApplicationExtensions
    Program.cs
    appsettings.json
    appsettings.Development.json

  KotoDibo.Application/          # Business logic layer, no dependency on Infrastructure or Api
    Common/
      Interfaces/                # IRepository<T>, ICurrentUserService, IDateTimeProvider, etc.
      Results/                   # Result<T> / OperationResult pattern
      Exceptions/
      Behaviors/                 # e.g. validation pipeline behavior, if using MediatR-style — otherwise skip
    Features/
      Auth/
        DTOs/
        Interfaces/
        Services/
        Validators/
      Households/
        DTOs/
        Interfaces/
        Services/
        Validators/
      Meals/
        DTOs/
        Interfaces/
        Services/
        Validators/
      Bazar/
        DTOs/
        Interfaces/
        Services/
        Validators/
      BillSplit/
        DTOs/
        Interfaces/
        Services/
        Validators/
      Expenses/
        DTOs/
        Interfaces/
        Services/
        Validators/
      Budget/
        DTOs/
        Interfaces/
        Services/
        Validators/
    Mappings/                    # Mapster/AutoMapper profiles (pick one, tell me which you chose)

  KotoDibo.Domain/               # Innermost layer, zero external dependencies
    Entities/                    # User, Household, MealEntry, BazarEntry, MealRate, BillSplit, Expense, Budget
    Enums/
    Constants/
    Exceptions/                  # Domain-specific exceptions

  KotoDibo.Infrastructure/       # Implements Application/Domain interfaces
    Persistence/
      MongoDb/
        MongoDbContext.cs
        MongoDbSettings.cs
        Repositories/            # Generic + entity-specific repository implementations
        Configurations/          # BSON class map configs per entity
    Auth/
      JwtTokenGenerator.cs
      PasswordHasher.cs
    Email/                       # for invite links / verification — stub provider interface + a no-op implementation
    Extensions/
      ServiceCollectionExtensions.cs   # AddInfrastructure(this IServiceCollection, IConfiguration)

  KotoDibo.Common/                # Cross-cutting utilities usable by any layer above Domain
    Extensions/
    Helpers/
    Constants/

tests/
  KotoDibo.UnitTests/
    Application/
    Domain/
  KotoDibo.IntegrationTests/
    Api/

.editorconfig
README.md
```

### 2. Project references (enforce N-Tier dependency direction — this matters, don't let it drift)

- `KotoDibo.Domain` → references nothing
- `KotoDibo.Application` → references `KotoDibo.Domain`, `KotoDibo.Common`
- `KotoDibo.Infrastructure` → references `KotoDibo.Application`, `KotoDibo.Domain`, `KotoDibo.Common`
- `KotoDibo.Api` → references `KotoDibo.Application`, `KotoDibo.Infrastructure`, `KotoDibo.Common`
- `KotoDibo.Common` → references nothing
- Test projects reference the layer(s) they test

`KotoDibo.Api` must **not** reference `KotoDibo.Infrastructure` types directly in Controllers — only through interfaces registered via DI in `Infrastructure`'s `ServiceCollectionExtensions`.

### 3. NuGet packages to add, per project

- **KotoDibo.Api**: `Swashbuckle.AspNetCore` (or built-in .NET 10 OpenAPI if you recommend it instead — your call, tell me which and why), `Microsoft.AspNetCore.Authentication.JwtBearer`
- **KotoDibo.Application**: `FluentValidation`, plus your choice of `Mapster` or `AutoMapper` (pick one — Mapster is lighter/faster, I have no strong preference, just be consistent)
- **KotoDibo.Infrastructure**: `MongoDB.Driver`, `BCrypt.Net-Next` (password hashing)
- **KotoDibo.UnitTests / IntegrationTests**: `xUnit`, `Moq` (or `NSubstitute`, your call), `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing` for integration tests

### 4. Skeleton behavior to implement now (structure-level only)

- `Program.cs`: minimal hosting model, calls `builder.Services.AddInfrastructure(builder.Configuration)` and an equivalent `AddApplication()`, registers controllers, Swagger/OpenAPI, JWT auth middleware pipeline (stubbed config), and a `/health` endpoint that just returns 200 OK.
- One empty `Controllers/HealthController.cs` (or minimal API health endpoint — your call) so I can verify the API runs immediately after scaffold.
- `MongoDbSettings.cs` bound from `appsettings.json` (`ConnectionString`, `DatabaseName`) — leave placeholder values, real ones go in a gitignored `appsettings.Development.json` or environment variables for Render.
- One empty controller stub per feature area (`AuthController`, `HouseholdsController`, `MealsController`, `BazarController`, `BillSplitController`, `ExpensesController`, `BudgetController`) with route attributes set up (`[ApiController] [Route("api/[controller]")]`) but action methods returning `StatusCode(501)` (Not Implemented) as placeholders.
- Generic `IRepository<T>` interface in `Application/Common/Interfaces` and its MongoDB implementation in `Infrastructure/Persistence/MongoDb/Repositories`.
- `.gitignore` already exists (dotnet template) — just confirm `appsettings.Development.json` and any `.env` file are covered; add entries if missing.

### 5. Deliverable at the end

- Solution builds and runs (`dotnet run --project src/KotoDibo.Api`) with `/health` returning 200 and Swagger UI reachable.
- A short `README.md` at repo root explaining the folder structure and the N-Tier dependency rule above, so future-me (or a collaborator) doesn't violate it.
- Don't touch the frontend — this repo is backend-only.

Ask me before making any architectural choice not specified above (e.g. Mapster vs AutoMapper if you have no preference — otherwise just pick and note why).
