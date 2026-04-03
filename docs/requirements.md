# ElshazlyStore — Requirements

> This document captures the non-negotiable system requirements and future integration plans.
> It is the canonical reference for all design decisions.

---

## 1. Core Architecture Principles

### 1.1 Server-Only Source of Truth
- The backend API is the **single authority** over all business data and rules.
- The desktop UI is a thin client: it reads via API, writes via API, and never bypasses validation.
- No direct database access from any client.
- No external HTTP/API calls from the UI (all third-party integrations are server-to-server).

### 1.2 Strict Invariants
- All writes are **transactional** — no partial mutations are ever committed.
- Domain entities enforce their own invariants; the API layer never silently ignores them.
- Every error returned to the client follows **RFC 7807 ProblemDetails** with a stable, machine-readable error code.

### 1.3 Audit & Traceability
- Every request carries a **Correlation ID** (generated or forwarded).
- All mutations are audit-logged via EF Core `SaveChangesInterceptor` with: who, what, when, old/new values (JSONB), correlation ID.
- Entities excluded from audit: `AuditLog`, `RefreshToken`.
- Structured logging (Serilog) with correlation ID in every log scope.

### 1.4 Permissions & Multi-User
- **Permission-based** access control enforced server-side (not just roles).
- Permissions are fine-grained codes: `USERS_READ`, `USERS_WRITE`, `ROLES_READ`, `ROLES_WRITE`, `AUDIT_READ`, `PRODUCTS_READ`, `PRODUCTS_WRITE`, `CUSTOMERS_READ`, `CUSTOMERS_WRITE`, `SUPPLIERS_READ`, `SUPPLIERS_WRITE`, `IMPORT_MASTER_DATA`, `STOCK_READ`, `STOCK_POST`, `WAREHOUSES_READ`, `WAREHOUSES_WRITE`.
- Roles are collections of permissions; users are assigned roles.
- JWT Bearer authentication with access tokens (short-lived) and refresh tokens (rotating).
- Custom `IAuthorizationPolicyProvider` dynamically creates policies from `Permission:CODE` format.
- No client can self-assign elevated permissions.

---

## 2. Technical Stack

| Layer          | Technology                                |
|----------------|-------------------------------------------|
| API            | .NET 8, ASP.NET Core Minimal API          |
| Auth           | JWT Bearer + Permission-based AuthZ       |
| Domain         | Pure C# (no framework dependencies)       |
| Infrastructure | EF Core 8 + Npgsql (PostgreSQL)           |
| Testing        | xUnit + WebApplicationFactory + SQLite    |
| Docs           | Swagger/OpenAPI (dev only)                |
| Logging        | Serilog (structured, console + file)      |
| Database       | PostgreSQL 16                             |

---

## 3. Future UI Requirements (Desktop — WPF or WinUI)

> _Not implemented in Phase 0. Documented here for planning._

- **DPI Scaling**: Must render correctly at 100%, 125%, and 150% scaling.
- **Theme Support**: Dark mode and light mode, switchable at runtime.
- **OS Support**: Windows 10 (1809+) and Windows 11.
- **Packaging**: Separate x86 and x64 builds (or AnyCPU with appropriate native dependencies).
- **Offline**: NO offline mode. The UI must always be connected to the API server.
- **Arabic/RTL**: Full RTL support in layout, text input, and reporting.

---

## 4. Future Integration Requirements

> _Not implemented in Phase 0. Documented here for planning._

### 4.1 Dashboard Module
- Real-time dashboard fed by server-side events (SignalR or SSE).
- Aggregated sales, inventory, and financial KPIs.

### 4.2 AI / Automation Modules
- Demand forecasting, smart reorder suggestions.
- All AI inference runs server-side; UI only displays results.
- Outbox pattern for reliable async event processing.

### 4.3 Server-Side Events / Outbox
- Transactional outbox table for domain events.
- Background processor publishes events to consumers (dashboards, AI, notifications).
- Guarantees at-least-once delivery with idempotent consumers.

---

## 5. Phase Roadmap (High Level)

| Phase | Scope                                             | Status    |
|-------|---------------------------------------------------|-----------|
| 0     | Solution skeleton, middleware, health, EF Core     | ✅ Done    |
| 1     | Auth (JWT), users, roles, permissions, audit       | ✅ Done    |
| 2     | Master data, barcodes, customers/suppliers, import | ✅ Done    |
| 3     | Inventory core, stock movements, ledger            | ✅ Done    |
| 4     | Sales, invoicing, POS                              | ⏳ Next    |
| 5     | Reporting, dashboard, events                       |           |
| 6     | AI modules, automation, outbox                     |           |
| 7     | Desktop UI (WPF/WinUI)                             |           |

---

## 6. Phase 2 — Master Data Requirements

### 6.1 Products & Variants
- Parent `Product` + child `ProductVariant` (color, size, pricing).
- Each variant belongs to exactly one parent product.
- Pricing: `RetailPrice`, `WholesalePrice` (both optional; required for POS later).
- Paging, sorting, and search across name/description/category.

### 6.2 Global Barcode (Critical)
- Every variant must have a barcode (string).
- Barcodes are **globally unique and never reused**.
- `BarcodeReservation` table tracks lifecycle: Reserved → Assigned → **Retired** (terminal).
- DB unique index on barcode column enforces uniqueness at storage level.
- When a variant is deleted, its barcode is retired (never reusable).

### 6.3 Customers & Suppliers
- Minimal strong model: code, name, phone(s), notes, isActive.
- Code is unique (server-generated with `CUST-NNNNNN` / `SUP-NNNNNN` pattern if not provided).
- Search supports `q` parameter across code/name/phone columns.

### 6.4 Import (Excel/CSV)
- Admin-only (`IMPORT_MASTER_DATA` permission).
- Supports CSV and XLSX formats.
- Two-step process: **Preview** (validate) → **Commit** (apply).
- `ImportJob` table tracks jobs with file hash, status, preview results (JSONB).
- Import respects all business rules: barcode uniqueness, code uniqueness.
- If barcode missing, server generates a unique barcode; duplicate barcodes fail.
- Sample templates provided in `docs/templates/`.

---

## 7. Phase 3 — Inventory Core Requirements

### 7.1 Warehouses
- At least one default warehouse is seeded on startup (`MAIN`).
- Warehouse entity: code (unique), name, address, isDefault, isActive.
- CRUD with soft-delete (deactivate).

### 7.2 Stock Movements (Append-Only Ledger)
- **Single posting endpoint**: `POST /api/v1/stock-movements/post`.
- All stock mutations go through this endpoint — no direct balance manipulation.
- Movement header: type, reference, notes, postedAt, createdBy (user).
- Movement lines: variantId, warehouseId, quantityDelta, unitCost (optional), reason.
- Lines are **immutable after posting** — append-only ledger.
- Supported movement types:
  - `OpeningBalance` — initial stock import (positive only)
  - `PurchaseReceipt` — goods received (positive only)
  - `SaleIssue` — goods sold/issued (negative only)
  - `Transfer` — warehouse-to-warehouse (negative from source, positive to destination)
  - `Adjustment` — with reason (any sign)
  - `ProductionConsume` — raw materials consumed (negative only)
  - `ProductionProduce` — finished goods produced (positive only)

### 7.3 StockBalance (Materialized)
- `StockBalance` table for fast reads: one row per (variant, warehouse) pair.
- Updated transactionally within the same posting transaction.
- Unique index on (variantId, warehouseId).

### 7.4 Negative Stock Prevention
- By default, negative stock is **not allowed**.
- If a movement would result in negative balance, the entire posting is rejected.
- Error code: `STOCK_NEGATIVE_NOT_ALLOWED` (HTTP 422).

### 7.5 Concurrency Safety
- Posting uses **Serializable transaction isolation** to prevent race conditions.
- Multiple concurrent stock operations on the same variant/warehouse are serialized by the database.
- Concurrency conflicts result in `CONFLICT` error with retry guidance.

### 7.6 Read Endpoints
- `GET /api/v1/stock/balances` — paged, filtered by warehouseId, searchable by SKU/product name/barcode.
- `GET /api/v1/stock/ledger` — paged, filtered by variantId, warehouseId, date range.

### 7.7 Audit
- All stock movement postings create audit records via the existing `AuditInterceptor`.
- Correlation ID is preserved through the posting pipeline.
