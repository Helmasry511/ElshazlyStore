# ElshazlyStore — Operations Guide

> Deployment, backup/restore, monitoring, and maintenance procedures.

---

## Prerequisites

- **.NET 8 SDK** (or runtime for production)
- **PostgreSQL 15+**
- **Docker** (optional, for containerized deployment)

---

## Configuration

### Environment Variables / appsettings.json

| Setting                                     | Description                              | Default              |
|---------------------------------------------|------------------------------------------|----------------------|
| `ConnectionStrings:DefaultConnection`       | PostgreSQL connection string             | *(required)*         |
| `Jwt:Secret`                                | HMAC-SHA256 signing key (≥32 chars)      | *(required)*         |
| `Jwt:Issuer`                                | Token issuer claim                       | `ElshazlyStore`      |
| `Jwt:Audience`                              | Token audience claim                     | `ElshazlyStore.Desktop` |
| `Jwt:AccessTokenExpirationMinutes`          | Access token lifetime                    | `15`                 |
| `Jwt:RefreshTokenExpirationDays`            | Refresh token lifetime                   | `7`                  |
| `RequestLimits:MaxRequestBodyMB`            | Max request body (Kestrel)               | `10`                 |
| `RequestLimits:MaxMultipartMB`              | Max multipart upload (CSV/XLSX imports)  | `10`                 |
| `Performance:CommandTimeoutSeconds`         | EF Core command timeout                  | `30`                 |
| `Performance:SlowRequestThresholdMs`        | Slow request warning threshold           | `500`                |
| `ADMIN_DEFAULT_PASSWORD`                    | Password for seeded admin user           | *(required on first run)* |
| `Serilog:*`                                 | Serilog configuration                    | Console sink         |

### Production Checklist

- [ ] Set a strong `Jwt:Secret` (at least 32 random characters)
- [ ] Set `ADMIN_DEFAULT_PASSWORD` via environment variable, not in config files
- [ ] Change access token expiration to 15 min (`AccessTokenExpirationMinutes: 15`)
- [ ] Change refresh token expiration to 7 days (`RefreshTokenExpirationDays: 7`)
- [ ] Configure `Serilog` to write to file or centralized logging (Seq, ELK, etc.)
- [ ] Set `AllowedHosts` to your domain(s) instead of `*`
- [ ] Enable HTTPS/TLS at the reverse proxy level (nginx, Caddy, etc.)
- [ ] Restrict PostgreSQL access to the application server only

---

## Deployment

### Direct (Kestrel)

```bash
# Build
dotnet publish src/ElshazlyStore.Api -c Release -o ./publish

# Run
cd publish
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=elshazly_store;Username=app;Password=secret"
export Jwt__Secret="your-secure-secret-key-at-least-32-chars"
export ADMIN_DEFAULT_PASSWORD="StrongAdminPassword!"
dotnet ElshazlyStore.Api.dll
```

The API listens on `http://localhost:5000` by default. Use a reverse proxy (nginx/Caddy) for TLS termination.

### Docker Compose

```bash
docker-compose up -d
```

The `docker-compose.yml` in the repository root starts both PostgreSQL and the API.

---

## Database Management

### Run Migrations

Migrations are applied automatically on startup (`db.Database.MigrateAsync()`).

To apply manually:

```bash
dotnet ef database update \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
```

### Create a New Migration

```bash
dotnet ef migrations add "XXXX_description" \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
```

### Rollback a Migration

```bash
dotnet ef database update "PreviousMigrationName" \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
```

---

## Backup & Restore (PostgreSQL)

### Full Backup

```bash
# Custom format (compressed, supports parallel restore)
pg_dump -Fc -h localhost -U app -d elshazly_store -f backup_$(date +%Y%m%d_%H%M%S).dump

# Plain SQL (human-readable)
pg_dump -h localhost -U app -d elshazly_store > backup_$(date +%Y%m%d_%H%M%S).sql
```

### Restore

```bash
# From custom format
pg_restore -h localhost -U app -d elshazly_store --clean --if-exists backup.dump

# From plain SQL
psql -h localhost -U app -d elshazly_store < backup.sql
```

### Automated Daily Backups (cron)

```bash
# /etc/cron.d/elshazly-backup
0 2 * * * postgres pg_dump -Fc -h localhost -U app -d elshazly_store -f /backups/elshazly_$(date +\%Y\%m\%d).dump
# Keep last 30 days
0 3 * * * postgres find /backups -name "elshazly_*.dump" -mtime +30 -delete
```

### Windows (Task Scheduler)

```powershell
# backup.ps1
$date = Get-Date -Format "yyyyMMdd_HHmmss"
& "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe" `
  -Fc -h localhost -U app -d elshazly_store `
  -f "C:\Backups\elshazly_$date.dump"

# Cleanup older than 30 days
Get-ChildItem "C:\Backups\elshazly_*.dump" |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
  Remove-Item
```

---

## Monitoring

### Health Check

```
GET /api/v1/health
```

Returns `200 OK` with system status. Use this for load balancer health probes.

### Structured Logging

All requests include:
- **`X-Correlation-Id`** header (auto-generated UUID per request)
- **Serilog** structured logs with `CorrelationId`, `RequestId`, `RequestPath` properties

### Key Log Events

| Level | Event                                  | Description                        |
|-------|----------------------------------------|------------------------------------|
| INF   | `Starting ElshazlyStore API`           | Application startup                |
| INF   | `Seeded N permissions`                 | Permission seeding on startup      |
| INF   | `Import job {id} committed`            | Successful data import             |
| WRN   | `MultipleCollectionIncludeWarning`     | EF Core query hint (non-critical)  |
| ERR   | `Unhandled exception on {Method} {Path}` | Server error (check detail)      |
| FTL   | `Application terminated unexpectedly`  | Crash / startup failure            |

### Error Detail Masking

In non-Development environments, 500-level errors return a generic message:
```json
{ "detail": "An internal error occurred. Check server logs for details." }
```

Full exception details are logged server-side only.

---

## Security

### Authentication Flow

1. Client sends `POST /api/v1/auth/login` with username/password
2. Server returns short-lived access token (JWT, default 15 min) + opaque refresh token (default 7 days)
3. Client includes `Authorization: Bearer <access_token>` on all requests
4. On expiry, client sends `POST /api/v1/auth/refresh` with the refresh token
5. Server **rotates** the refresh token: revokes old, issues new pair
6. Reuse of a revoked refresh token is rejected with `401 TOKEN_EXPIRED`

### Permission Model

- 32 permission codes (see `docs/api.md` § Permissions)
- Permissions are assigned to **Roles**, roles are assigned to **Users**
- Every protected endpoint declares a required permission via policy
- Seeded `Admin` role automatically receives all permissions

### Request Size Limits

- **Max request body**: configurable via `RequestLimits:MaxRequestBodyMB` (default 10 MB)
- **Max multipart form**: configurable via `RequestLimits:MaxMultipartMB` (default 10 MB)
- File type validation: only `.csv` and `.xlsx` accepted

### Audit Trail

All data changes are automatically logged to `audit_logs` with:
- Who (UserId, Username, IP, UserAgent)
- What (Action, EntityName, PrimaryKey)
- Changes (OldValues, NewValues as JSON — truncated to 4 KB)

---

## Troubleshooting

### Application won't start

1. Check connection string: `ConnectionStrings:DefaultConnection`
2. Verify PostgreSQL is running and accessible
3. Check `Jwt:Secret` is configured (at least 32 characters)
4. Review startup logs for `FTL` level entries

### Migrations fail

```bash
# Check pending migrations
dotnet ef migrations list \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api

# Force re-create (DESTRUCTIVE — dev only)
dotnet ef database drop --force \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
dotnet ef database update \
  --project src/ElshazlyStore.Infrastructure \
  --startup-project src/ElshazlyStore.Api
```

### Performance

- All list endpoints support server-side paging (`page`, `pageSize` max 100)
- Dashboard queries use composite index `IX_sales_invoices_status_posted`
- Stock balances use unique composite index on `(VariantId, WarehouseId)`
- Accounting queries use indexes on `(PartyType, PartyId)`, `CreatedAtUtc`, `PaymentDateUtc`

#### Performance Tuning Settings

| Setting                              | Default | Description                                        |
|--------------------------------------|---------|----------------------------------------------------|
| `Performance:CommandTimeoutSeconds`  | `30`    | EF Core command timeout (seconds). Increase for heavy imports. |
| `Performance:SlowRequestThresholdMs` | `500`   | Requests exceeding this are logged at `Warning` level. |

#### Response Compression

Gzip compression is enabled for all HTTPS responses. Large JSON payloads (product lists, stock grids) are compressed automatically. No client-side configuration needed beyond accepting `Accept-Encoding: gzip`.

#### Caching

- **Barcode lookups** are cached in-memory with a 60-second TTL. Cache is invalidated automatically on expiry. For high-traffic POS, this eliminates repeated DB round-trips for the same barcode scan.

#### Import Batching

- Product, customer, and supplier imports use batched `SaveChanges` (500 entities per flush) to avoid large change-tracker overhead and reduce transaction size.

#### Trigram Search (pg_trgm)

Migration `0012_perf_indexes_trgm` installs the `pg_trgm` PostgreSQL extension and creates GIN indexes on all user-searchable text columns. These indexes accelerate `LIKE '%pattern%'` searches without requiring full table scans.

Covered columns: product `Name`, variant `Sku`, customer `Name`/`Code`, supplier `Name`/`Code`, invoice `InvoiceNumber`, receipt `DocumentNumber`, batch `BatchNumber`, warehouse `Name`/`Code`, profile `Name`, payment `Reference`.

#### Stock Movement Idempotency

A unique filtered index on `stock_movements.Reference` (where not null) prevents duplicate movements with the same reference. Combined with serializable isolation and Npgsql retry-on-failure, this ensures safe concurrent stock operations.

#### Returns & Dispositions Concurrency

All return/disposition posting uses an **atomic claim gate** — `UPDATE ... WHERE Status = Draft` — that guarantees only one request can transition a document from Draft to Posted. Double-posts:
- Return **200 OK** with the existing `StockMovementId` (idempotent) if the first post completed successfully.
- Return **409 POST_CONCURRENCY_CONFLICT** if another post is mid-flight (client should retry).

Posted documents are **immutable** — no edits, no reversals. Void is only possible on Draft documents.

#### Disposition Manager Approval

Dispositions whose reason codes have `RequiresManagerApproval = true` must be approved by a user with `DISPOSITION_APPROVE` permission before posting. Updating lines after approval **clears** the approval, requiring re-approval. Monitor for organizational compliance.

#### Load Testing

See `perf/README.md` for the k6 load test harness. Quick start:

```bash
k6 run perf/load-test.js                          # smoke test
k6 run --vus 20 --duration 60s perf/load-test.js   # sustained load
```

### Running Tests

```bash
# Full suite
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~DashboardTests"

# With verbose output
dotnet test --verbosity normal
```

Tests use an in-memory SQLite database — no PostgreSQL needed for CI.
