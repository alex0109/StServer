# StServer

StServer is the backend API for **StudyTracker** — a study/learning platform where users turn their materials into quizzes, take attempts, and track progress over time. It handles materials, questions, assessments, attempts, tags, and statistics, and is built as a .NET 10 Web API following **Clean Architecture**.

> This is the counterpart to the `studytracker` Next.js client, which consumes this API and uses Supabase directly only for authentication.

## Tech stack

- **Runtime:** .NET 10, ASP.NET Core Minimal APIs
- **Database:** PostgreSQL via Entity Framework Core (Npgsql)
- **Auth:** JWT bearer validated against Supabase's JWKS endpoint (no shared secret — keys are fetched and validated at startup)
- **Background jobs:** Hangfire (PostgreSQL storage) — e.g. a daily job that cleans up stale attempts
- **Fuzzy matching:** FuzzySharp, used to grade open-ended question answers
- **Resilience:** built-in rate limiting, a global exception handler returning RFC 7807 Problem Details, HSTS/HTTPS redirection in non-dev environments
- **Testing:** xUnit-style unit tests and end-to-end API tests (custom `WebApplicationFactory` + test auth handler)
- **CI/CD:** GitHub Actions → Azure Web App

## Architecture

The solution follows **Clean Architecture**, with dependencies pointing inward (`Api` → `Application`/`Infrastructure` → `Domain`):

```
StServer/
├── src/
│   ├── Domain/            # Entities and enums — no external dependencies
│   │   ├── Entities/        # Material, Question, Assessment, Attempt, Result, Tag, Option...
│   │   └── Utility/         # Enums: MaterialType, MaterialStatus, QuestionDifficulty, AttemptStatus...
│   ├── Application/        # Business logic, framework-agnostic
│   │   ├── DTOs/             # Request/response contracts per entity
│   │   ├── Interfaces/       # Service and repository contracts
│   │   ├── Services/         # Use-case implementations
│   │   ├── Mappers/          # Entity <-> DTO mapping
│   │   ├── Evaluators/       # Answer validation steps (exact, fuzzy)
│   │   ├── ScoreCalculators/  # Scoring strategies (average, non-linear difficulty, success bonus)
│   │   ├── Jobs/             # Hangfire background jobs
│   │   └── Constants/         # e.g. per-user limits
│   ├── Infrastructure/     # EF Core DbContext, migrations, repository implementations
│   └── Api/                # Composition root: endpoints, middleware, auth, DI wiring
├── tests/
│   ├── UnitTests/          # Services, mappers, evaluators, score calculators
│   └── E2ETests/           # Endpoint-level tests against an in-memory test host
└── .github/workflows/      # CI/CD to Azure Web App
```

### Key domain concepts

| Concept | Description |
|---|---|
| `Material` | A study item (link or rich-text content), owned by a user, with tags and a status |
| `Question` | Open-ended or multiple-choice, attached to a material, with a difficulty level |
| `Assessment` / `Attempt` | A quiz session; attempts are scored and can be cleaned up automatically if abandoned |
| `Result` | Per-question outcome of an attempt, including confidence level and score |
| `Tag` | User-defined labels for organizing materials |
| Statistics | Aggregated views: attempt trends, study streak, difficulty breakdown, confidence calibration, tag performance |

### Answer evaluation & scoring

Open-ended answers are graded through a pipeline of validation steps — an exact match check first, then a fuzzy string-similarity check (via FuzzySharp) as a fallback — so minor typos don't cost a user a correct answer. Scoring supports multiple strategies (simple average, a non-linear difficulty-weighted score using exponential weights, and a success-bonus variant), selected via `IScoreCalculator`.

## Getting started

### Requirements

- .NET 10 SDK
- PostgreSQL instance
- A Supabase project (for JWT issuance and the JWKS endpoint)

### Environment variables

Create `src/Api/.env` based on `src/Api/.env.example`:

```env
SUPABASE_URL=""
DATABASE_URL=""
JWKS_URL=""
```

- `SUPABASE_URL` — used to validate the JWT issuer (`{SUPABASE_URL}/auth/v1`)
- `DATABASE_URL` — PostgreSQL connection string (used for both EF Core and Hangfire storage)
- `JWKS_URL` — Supabase's JSON Web Key Set endpoint, fetched at startup to validate token signatures

In development, `DotNetEnv` loads this file automatically. In production, set these as real environment variables/App Service settings instead.

### Running locally

```bash
cd src/Api
dotnet run
```

Apply database migrations:

```bash
dotnet ef database update --project ../Infrastructure --startup-project .
```

The app exposes a health-check-style root endpoint (`GET /`) returning status, name, and environment. In development, the Hangfire dashboard is also available for inspecting background jobs.

### Running tests

```bash
dotnet test
```

This runs both `UnitTests` (services, mappers, evaluators, score calculators) and `E2ETests` (full HTTP pipeline against a test host with a stubbed auth handler).

## API surface

All endpoints are grouped under `/api/*`, require authentication, and are rate-limited:

- `api/materials` — CRUD for materials, tag association, statistics data
- `api/questions` — question management (open/options types)
- `api/assessments` — assessment lifecycle
- `api/attempts` — starting, submitting answers, finishing attempts
- `api/tags` — tag CRUD
- `api/statistics` — overview, trends, streaks, difficulty breakdown, confidence calibration, tag performance

## Deployment

CI/CD is handled by GitHub Actions (`.github/workflows/main_stserver.yml`): on push to `main`, the API is built and published, then deployed to an **Azure Web App** via `azure/webapps-deploy`.

## Known issues / TODO

- `Api.csproj` still references a leftover `net9.0` build output folder (`bin\Debug\net9.0\.env`) despite `TargetFramework` being `net10.0` — safe to clean up.
- OpenAPI/Swagger UI isn't explicitly mapped in `Program.cs` yet, even though `Microsoft.AspNetCore.OpenApi` is referenced — worth wiring up for easier manual testing and client generation.
- Several EF Core migrations have informal names (`fastFix`, `FinalPreDeployChanges`) — fine during active development, but consider squashing before a stable 1.0 release.
- `.env` and `.idea` are correctly excluded via `.gitignore`; only `.env.example` is tracked, so no secrets are committed.

## License

Not specified.
