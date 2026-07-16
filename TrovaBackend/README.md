# Trova API

Lean .NET 9 Web API skeleton — Supabase (Postgres) via EF Core, JWT auth. Structured the same way as GoFix, trimmed down for a 2-week timeline: no email service, no file storage, no notifications yet. Add those back only if the frontend actually needs them.

## Structure

```
TrovaBackend/
├── Controllers/       # HTTP endpoints
├── Services/           # Business logic (interface + implementation per feature)
│   └── Auth/
├── Data/               # EF Core DbContext
├── Models/             # Database entities
├── DTOs/                # Request/response shapes, grouped by feature
│   ├── Auth/
│   └── Common/          # Shared ApiResponse<T> wrapper
├── Middleware/          # Global exception handling
└── Program.cs           # App wiring: DB, JWT, Swagger, CORS
```

## Setup

1. **Create the Supabase project**, then grab the connection pooler string from
   Project Settings → Database → Connection string (use "Transaction" mode pooler for serverless-friendly connections, or "Session" mode if you need prepared statements).

2. **Fill in `appsettings.Development.json`** (already gitignored):
   ```json
   "DefaultConnection": "Host=...;Port=5432;Database=postgres;Username=postgres.xxxx;Password=...;SSL Mode=Require;Trust Server Certificate=true"
   ```

3. **Restore & run**:
   ```bash
   dotnet restore
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   dotnet run
   ```
   Swagger UI opens automatically at `/swagger`.

4. **Test it's alive**: `GET /api/auth/ping` → "Trova API is running"

## What's already built

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me` (requires Bearer token)
- `POST /api/auth/change-password` (requires Bearer token)

## What's intentionally missing (add when needed)

- Email service (verification codes, password reset emails) — GoFix's `EmailService.cs` (MailKit) can be ported over in ~20 min if needed
- File storage — consider Supabase Storage buckets directly instead of Azure Blob, simpler for this timeline
- Forgot/reset password flow — needs the email service first
- Domain entities — waiting on frontend/feature spec

## CORS

Currently wide open (`AllowAnyOrigin`) since the frontend origin isn't finalized. **Tighten this before shipping** — restrict to the actual frontend domain in `Program.cs`.
