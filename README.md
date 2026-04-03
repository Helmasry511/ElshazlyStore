# ElshazlyStore (الشاذلي) — Backend API

Multi-user ERP/POS backend with server-owned source of truth.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL)
- [dotnet-ef tool](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

---

## Quick Start

### 1. Start PostgreSQL

```bash
docker compose up -d
```

This launches PostgreSQL 16 on `localhost:5432` with user `postgres` / password `postgres`.

### 2. Apply Migrations

```bash
dotnet ef database update \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
```

### 3. Run the API

```bash
dotnet run --project src/ElshazlyStore.Api
```

Swagger UI: [https://localhost:5001/swagger](https://localhost:5001/swagger) (Development only)

Health check: `GET /api/v1/health`

### 4. Run Tests

```bash
dotnet test
```

---

## Project Structure

```
ElshazlyStore.sln
├── src/
│   ├── ElshazlyStore.Api            # ASP.NET Core host, middleware, endpoints
│   ├── ElshazlyStore.Domain         # Entities, value objects, interfaces (no EF)
│   └── ElshazlyStore.Infrastructure # EF Core DbContext, migrations, repositories
├── tests/
│   └── ElshazlyStore.Tests          # xUnit integration & unit tests
├── docs/
│   └── requirements.md              # System requirements & future plans
├── docker-compose.yml               # Local PostgreSQL
└── Directory.Build.props            # Shared MSBuild properties
```

---

## Configuration

Connection string is in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=elshazlystore_dev;Username=postgres;Password=postgres"
  }
}
```

---

## Key Design Decisions

- **Server is the only source of truth** — no direct DB access from UI.
- **All errors** follow RFC 7807 ProblemDetails with stable error codes.
- **Correlation ID** on every request for traceability.
- **Structured logging** via Serilog.
- See [docs/requirements.md](docs/requirements.md) for full requirements.
