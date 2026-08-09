# Koto Dibo? (কত দিবো?) — Backend

Backend API for **Koto Dibo?**, a household finance PWA for bachelors in shared housing and low-income families in Bangladesh. This repo is backend-only; the frontend (React/Vite PWA) lives in a separate repository.

- **Stack**: .NET 10 Web API + MongoDB (via `MongoDB.Driver`, no EF Core)
- **Architecture**: Classic N-Tier (Presentation → Application → Domain, with Infrastructure implementing interfaces defined in Application/Domain)
- **Auth**: Self-hosted JWT
- **Deploy target**: Render free tier (containerless), config via `appsettings.json` + environment variables

This repo is currently a **structural scaffold**: projects, folders, references, and packages are wired up, but feature logic, real MongoDB queries, and real JWT validation are stubbed with `throw new NotImplementedException()` pending feature-by-feature implementation.

## Solution layout

```
KotoDibo.sln
src/
  KotoDibo.Api/                  # Presentation layer — controllers, JWT/Swagger wiring, Program.cs
  KotoDibo.Application/          # Business logic — DTOs, service interfaces + implementations, FluentValidation validators, Mapster mappings
  KotoDibo.Domain/                # Entities, enums, constants, domain exceptions — zero external dependencies
  KotoDibo.Infrastructure/       # MongoDB persistence, JWT/password hashing, email — implements Application/Domain interfaces
  KotoDibo.Common/                # Cross-cutting utilities usable by any layer above Domain
tests/
  KotoDibo.UnitTests/
  KotoDibo.IntegrationTests/
```

## The N-Tier dependency rule

```
Domain            → references nothing
Common             → references nothing
Application        → Domain, Common
Infrastructure      → Application, Domain, Common
Api                 → Application, Infrastructure, Common
```

**`KotoDibo.Api` must never reference `KotoDibo.Infrastructure` types directly inside a `Controllers/*.cs` file.** Controllers depend only on interfaces from `KotoDibo.Application` (e.g. `IHouseholdService`), which are registered against their `Infrastructure` implementations via dependency injection in `Infrastructure/Extensions/ServiceCollectionExtensions.cs` and `Application/DependencyInjection.cs`. `Infrastructure` types (`MongoDbContext`, `JwtSettings`, etc.) are only ever touched from the `Api`'s own DI-wiring code in `Api/Extensions/`, never from a controller action.

If you ever need to reach into `KotoDibo.Infrastructure` from a controller, that's a sign a new interface belongs in `KotoDibo.Application` instead.

## Notable choices made during scaffolding

- **.NET 10 SDK**: only .NET 6/8 were installed locally, so the .NET 10 SDK (`10.0.302`) was installed via the official `dotnet-install.sh` script into `~/.dotnet` (no `sudo` required). Make sure `~/.dotnet` is on your `PATH` (or update your shell profile) before running `dotnet build`/`dotnet run` — the system-wide `dotnet` will otherwise resolve to an older SDK.
- **Solution format**: `KotoDibo.sln` uses the classic `.sln` format (`dotnet new sln -f sln`) rather than .NET 10's new default `.slnx`, per the original spec.
- **OpenAPI/Swagger**: used `Swashbuckle.AspNetCore` (Swagger UI out of the box) rather than the built-in `Microsoft.AspNetCore.OpenApi`, since the latter doesn't ship a UI on its own and this is a controller-based API where Swashbuckle is the well-trodden path.
- **Mapping**: used **Mapster** (`Mapster` + `Mapster.DependencyInjection`) over AutoMapper — lighter, faster, and there was no strong preference stated. Registered via `TypeAdapterConfig` + `IMapper`/`ServiceMapper` in `Application/DependencyInjection.cs`.
- **Health endpoint**: implemented as a minimal API endpoint (`GET /health` in `Api/Extensions/WebApplicationExtensions.cs`) rather than a controller, since it's a single trivial route.
- **`IEmailSender`**: the interface lives in `Application/Common/Interfaces` (not `Infrastructure/Email`) so that Application-layer services can depend on it without violating the dependency rule above; `Infrastructure/Email/NoOpEmailSender.cs` is the current no-op implementation, registered via DI.
- **Password hashing vs JWT**: `Infrastructure/Auth/PasswordHasher.cs` has a real implementation (`BCrypt.Net-Next`) since there's only one reasonable way to hash a password. `Infrastructure/Auth/JwtTokenGenerator.cs` is left as `throw new NotImplementedException()` pending a decision on claims/expiry, per the "stub real logic" scope of this pass.
- **Generic repository**: `IRepository<T>` (`Application/Common/Interfaces`) is implemented by `MongoRepository<T>` (`Infrastructure/Persistence/MongoDb/Repositories`), registered as an open generic in DI. Collection name defaults to `typeof(T).Name`. All methods currently throw `NotImplementedException` — no real Mongo queries have been written yet.
- **BSON class maps**: each entity has its own `*Configuration.cs` in `Infrastructure/Persistence/MongoDb/Configurations` (mapping `Id` to a `string`-represented `ObjectId`), aggregated by `MongoClassMapRegistrar.RegisterAll()`, called once from `MongoDbContext`'s constructor. Domain entities stay free of MongoDB attributes/dependencies this way.

## Configuration

`MongoDb` and `Jwt` sections are bound from configuration (see `appsettings.json` for the shape). Real connection strings and secrets go in `appsettings.Development.json` locally (now gitignored) or environment variables in Render — never commit real credentials to `appsettings.json`.

## Running locally

```bash
dotnet run --project src/KotoDibo.Api
```

- `GET /health` → `200 OK`
- Swagger UI → `/swagger` (Development environment only)

## Running tests

```bash
dotnet test
```
