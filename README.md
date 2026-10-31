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

Optional:

- `METRICS_TOKEN` — opens `/metrics` for Prometheus (see Monitoring and analytics); the other monitoring variables are listed there too.
- `SUPABASE_SERVICE_ROLE_KEY` — lets account deletion remove the Supabase user too (Auth admin API, HTTPS only). It bypasses every rule of the Supabase project, so it lives only in the backend environment, never in `appsettings.json` or the client. Without it the study data is still deleted, and the background job for the sign-in account fails until the key is set.

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

## Account export and deletion (GDPR)

- `GET /api/account/export` — a zip built on request and sent right away (nothing is stored, so there is no download link to protect): `studytracker-data.json` with everything below, `words.csv` with all words for Anki or a spreadsheet, and a short README. 3 exports per 10 minutes.
- `DELETE /api/account` — body `{ "confirmEmail": "...", "language": "uk" }`. The email must match the token, and the token must come from a sign-in in the last 10 minutes (the `amr` claim, a refresh doesn't renew it); otherwise `403 Reauthentication required`. 5 calls per hour.

What happens on deletion, in one Postgres transaction (`Infrastructure/Account/AccountDataPolicy.cs`; a unit test walks the EF model so a new table with a user id can't be missed):

| Data | What happens | Why |
| --- | --- | --- |
| Materials, notes, drawings, files, questions, options, assessments, attempts, results, tags, dictionaries, words, review history and progress, flashcard and review sessions, share links, library requests, reminders and their settings, plan | Deleted | It is the user's data |
| Feedback counter rows (`user:{id}`) | Deleted | Only a counter, the message text is never stored |
| Published library materials | Kept, author becomes "Deleted user", link to the account and source material removed | Other users already imported copies |
| AI usage rows and monthly totals | Kept with a random id that belongs to nobody | Cost reports; no content is stored there |
| Admin audit log | Kept as is, plus an "Account deleted" entry with the first 8 characters of the id | Security record |

After the commit the Supabase user is deleted by a Hangfire job (retried with growing delays if Supabase is down), a confirmation email is sent, and tokens issued before the deletion are refused for 2 hours (per server, in memory) so an open tab can't create new rows. Logs carry only the short id.

## Plans and billing

The tier in force is decided in one place, `Application/Billing/SubscriptionRules.cs`: limits, AI quotas and the client all read it from there.

| Status | Plan stays until |
| --- | --- |
| Active | `CurrentPeriodEnd` (no date: until changed) |
| Trialing | `TrialEndsAt` |
| PastDue | `CurrentPeriodEnd` + 7 days, while the provider retries the payment |
| Canceled | `CurrentPeriodEnd`, the paid period is kept |
| Expired | ends now |

**Hand-made grants.** The admin panel (Plans page) gives Plus or Pro to a user id, with an optional end date and a reason; it can also take a hand-made grant back. Both are written to the audit log and emailed to the owner. A plan paid through a provider can't be changed there.

**Payments with Stripe (off until configured).** Everything is in place and stays off until the Stripe variables are set; without them `GET /api/billing/status` reports `checkoutAvailable: false` and the client shows no plans.

- `GET /api/billing/plans` (public): Free, Plus and Pro with their limits and AI quotas, and the prices read from Stripe (cached for an hour). Without prices the client shows no plan cards.
- `POST /api/billing/checkout` `{ plan: "Plus" | "Pro", interval: "Month" | "Year", language }`: a Stripe Checkout page. The server puts the user id into the session and subscription metadata, so a webhook can't be pointed at another account from the browser. A user with a running paid plan gets 409 and manages it in the portal instead; a hand-made grant doesn't block buying.
- `POST /api/billing/portal`: Stripe's customer portal (card, invoices, switching Plus/Pro, cancelling).
- `POST /api/billing/webhook/stripe`: `customer.subscription.created/updated/deleted`. `Stripe-Signature` is checked against the endpoint secret with a 5 minute window (several `v1` values are accepted while a secret is rolled). Statuses: `active`, `trialing`, `past_due` give the plan (past due with the grace above); `incomplete`, `incomplete_expired`, `unpaid`, `paused` and `canceled` give nothing. `cancel_at_period_end` or `cancel_at` keep the plan to the end of the period.
- Requests use the API version in `StripeBillingProvider.ApiVersion`; the period is read from the subscription item (API 2025-03-31 and later) with a fallback for older payloads.
- Deleting an account closes the Stripe customer in a Hangfire job (it cancels the subscription); invoices stay in Stripe for bookkeeping.

| Variable | What |
| --- | --- |
| `STRIPE_SECRET_KEY` | `sk_…` or, better, a restricted `rk_…` key (write: Checkout Sessions, Customer portal, Customers; read: Prices) |
| `STRIPE_WEBHOOK_SECRET` | `whsec_…` of the webhook endpoint |
| `STRIPE_PRICE_PLUS_MONTHLY`, `STRIPE_PRICE_PLUS_YEARLY`, `STRIPE_PRICE_PRO_MONTHLY`, `STRIPE_PRICE_PRO_YEARLY` | `price_…` ids; any missing one is simply not offered |
| `STRIPE_TAX_MODE` | `managed` (Managed Payments: Stripe is the merchant of record and handles VAT), `automatic` (Stripe Tax; you register and file VAT) or `none` (default) |
| `STRIPE_TRIAL_DAYS` | Optional free trial for someone who never paid before (0–90) |

The return addresses come from `Email:AppUrl` (HTTPS, or `http://localhost` for local runs): `/limits?checkout=success`, `/limits?checkout=cancelled` and `/limits` after the portal.

Setting it up in Stripe: create the products Plus and Pro with monthly and yearly prices (tax code `txcd_10103000`, SaaS for personal use, which Managed Payments requires), configure the customer portal (cancel at period end, switch between the Plus and Pro prices, update the payment method, invoice history), add a webhook endpoint `https://<api>/api/billing/webhook/stripe` with the three `customer.subscription.*` events, then set the variables above in the API's environment. Try it in test mode first: `stripe listen --forward-to localhost:5089/api/billing/webhook/stripe` prints a local `whsec_…`, and the card `4242 4242 4242 4242` pays.

Webhooks: the raw body's signature is checked, the event is stored once by its provider id (`SubscriptionEvents`, a repeat is answered 200 and dropped), and it is applied by a Hangfire job; an older event never undoes a newer state. Card data never reaches the app: Stripe's pages collect it, and the event log keeps the parsed event, not the request body. Deleting an account removes the subscription row and anonymizes its billing events.

## Monitoring and analytics

Everything is off until its environment variables are set, and keys live only in the environment (never in `appsettings.json` or the client).

**Metrics for Prometheus.** The API serves Prometheus metrics at `GET /metrics` when `METRICS_TOKEN` is set (without it the endpoint returns 404). The scraper sends the token as `Authorization: Bearer <token>` or as the basic-auth password. Published:

- `http_requests_received_total`, `http_request_duration_seconds` (labels `code`, `method`, `endpoint` = route pattern), `http_requests_in_progress`
- `process_*`, `dotnet_*` and `system_runtime_dotnet_*` (memory, CPU, GC, thread pool)
- business counters from `Application/Observability/AppMetrics.cs`: `studytracker_ai_requests` and `studytracker_ai_tokens` (feature, provider, outcome/direction), `studytracker_reviews` (kind, source), `studytracker_attempts_finished`, `studytracker_materials_created` (source), `studytracker_imports` (outcome), `studytracker_account_actions` (export/delete), `studytracker_billing_webhooks` (outcome). Labels never hold user ids, titles or URLs.

The `monitoring/` folder has a ready Prometheus + Grafana (`docker compose up -d`): scrape config for the job `studytracker-api`, the Prometheus data source and the dashboard `monitoring/grafana/dashboards/studytracker-api.json` (it can also be imported into Grafana Cloud; it asks for the Prometheus data source).

**Admin panel.** `GET /api/admin/monitoring` (Prometheus, Grafana, Sentry) and `GET /api/admin/analytics` (Google Analytics) read the outside services with these variables; answers are cached for a minute and a failing service shows its own note without hiding the others:

| Variable | What |
| --- | --- |
| `PROMETHEUS_URL` | Query URL: `http://localhost:9090`, or Grafana Cloud `https://prometheus-prod-…grafana.net/api/prom` (HTTPS required except on localhost) |
| `PROMETHEUS_USER`, `PROMETHEUS_TOKEN` | Grafana Cloud: instance id and an access policy token with `metrics:read`; a token alone is sent as a bearer token |
| `GRAFANA_URL`, `GRAFANA_TOKEN` | Grafana and a service account token with the Viewer role |
| `SENTRY_API_URL`, `SENTRY_ORG`, `SENTRY_API_TOKEN` | `https://de.sentry.io` for EU organizations, the org slug, an internal integration token with `org:read`, `project:read`, `event:read` |
| `GA_PROPERTY_ID`, `GA_CREDENTIALS_JSON` | Numeric GA4 property id; the service account key file (JSON, or base64 of it) of an account with Viewer access to the property |

**Google Analytics on the site** is set in the client with `NEXT_PUBLIC_GA_MEASUREMENT_ID` (`G-…`). It loads only after the visitor allows it, sends page views without query strings and with share tokens removed, and has advertising features off.

## Deployment

CI/CD is handled by GitHub Actions (`.github/workflows/main_stserver.yml`): on push to `main`, the API is built and published, then deployed to an **Azure Web App** via `azure/webapps-deploy`.

## Known issues / TODO

- `Api.csproj` still references a leftover `net9.0` build output folder (`bin\Debug\net9.0\.env`) despite `TargetFramework` being `net10.0` — safe to clean up.
- OpenAPI/Swagger UI isn't explicitly mapped in `Program.cs` yet, even though `Microsoft.AspNetCore.OpenApi` is referenced — worth wiring up for easier manual testing and client generation.
- Several EF Core migrations have informal names (`fastFix`, `FinalPreDeployChanges`) — fine during active development, but consider squashing before a stable 1.0 release.
- `.env` and `.idea` are correctly excluded via `.gitignore`; only `.env.example` is tracked, so no secrets are committed.

## License

Not specified.
