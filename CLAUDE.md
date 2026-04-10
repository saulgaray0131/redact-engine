# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RedactEngine is a prompt-driven video redaction engine. Users upload videos and describe via natural language what should be redacted. It's a distributed .NET system with a Python inference service, orchestrated locally via .NET Aspire.

## Build & Run Commands

```bash
# Build
dotnet build RedactEngine.sln

# Run everything (requires Docker Desktop running + Dapr initialized)
dotnet run --project RedactEngine.AppHost

# Watch mode
dotnet watch run --project RedactEngine.AppHost

# Architecture tests (only test project)
dotnet test RedactEngine.Architecture.Tests

# Frontend (must use pnpm, not npm/yarn)
cd RedactEngine.Web && pnpm install
cd RedactEngine.Web && pnpm dev          # Dev server on :5173
cd RedactEngine.Web && pnpm build        # Production build (runs tsc -b then vite build)
cd RedactEngine.Web && pnpm lint         # ESLint
cd RedactEngine.Web && pnpm openapi-ts   # Regenerate API client from OpenAPI spec

# Database migrations (from repo root)
dotnet ef migrations add <Name> --project RedactEngine.Infrastructure --startup-project RedactEngine.ApiService
dotnet ef migrations remove --project RedactEngine.Infrastructure --startup-project RedactEngine.ApiService
```

## Architecture

**System flow:** Web (React+Vite) -> API Service (ASP.NET Core) -> PostgreSQL + Azure Blob Storage. API publishes events via Dapr pub/sub -> Worker Service processes jobs -> calls Inference Service (Python FastAPI) for video processing.

**Layering (enforced by architecture tests):**
- **Domain** - Entities, value objects, domain events. No EF Core or external package dependencies.
- **Application** - Services, validation (FluentValidation), service contracts/DTOs. Orchestrates domain + infrastructure.
- **Infrastructure** - EF Core `ApplicationDbContext`, migrations, blob storage, outbox pattern. All persistence lives here.
- **ApiService / Worker** - Thin entry points. Controllers and workers delegate to application/infrastructure services.
- **AppHost** - Aspire orchestration only. No business logic.
- **Shared** - Cross-service pub/sub message contracts only.
- **ServiceDefaults** - Shared hosting config: telemetry (OpenTelemetry), health checks, resilience.

## Key Architectural Rules

- **No repository pattern.** Use `ApplicationDbContext`/`IApplicationDbContext` directly via DI.
- **No CQRS.** Use straightforward service methods, not request/handler layers.
- **MediatR is for domain events only.** Do not expand it into general command/query.
- **Dapr queues only** (Azure Service Bus Basic tier). Component is `pubsub.azure.servicebus.queues` - do not use `pubsub.azure.servicebus`.
- **Domain event outbox pattern** for transactional event capture. Preserve this pattern.
- **Migrations auto-apply on startup** via `DatabaseMigrationRunner`. Never run `dotnet ef database update` manually.
- **`RedactEngine.Web/src/client/` is auto-generated.** Never edit manually. Regenerate with `pnpm openapi-ts`.
- **Architecture tests in `RedactEngine.Architecture.Tests`** enforce layer separation via NetArchTest. Run them to verify changes don't violate dependency rules.

## Tech Stack

- .NET 10 / ASP.NET Core / EF Core with PostgreSQL (Npgsql)
- .NET Aspire for local orchestration
- Dapr for pub/sub messaging
- React 19 + TypeScript + Vite + Tailwind CSS + shadcn/ui + TanStack Query
- Python 3.12 + FastAPI for inference service (Grounding DINO + SAM 2, defaults to mock mode)
- Terraform for infrastructure provisioning (in `RedactEngine.AppHost/terraform/`)

## Deployment

CI/CD is fully automated via GitHub Actions. Merging to `main` triggers build, test, and deploy to Azure (Container Apps for backend, Static Web Apps for frontend).
