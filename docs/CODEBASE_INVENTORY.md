# ElshazlyStore — Complete Codebase Inventory

> Generated from a full deep-read of every source file.  
> Stack: **.NET 8 Minimal API · PostgreSQL 16 · EF Core 8 · JWT Bearer Auth · xUnit + SQLite**

---

## Table of Contents

1. [Solution Structure](#1-solution-structure)
2. [Entity Classes (Domain)](#2-entity-classes-domain)
3. [Enums](#3-enums)
4. [Interfaces](#4-interfaces)
5. [Service Implementations (Infrastructure)](#5-service-implementations-infrastructure)
6. [DbContext](#6-dbcontext)
7. [EF Configurations (29 files)](#7-ef-configurations)
8. [Endpoints (API)](#8-endpoints-api)
9. [Program.cs (Pipeline)](#9-programcs-pipeline)
10. [Authorization](#10-authorization)
11. [Middleware](#11-middleware)
12. [Seeding](#12-seeding)
13. [DependencyInjection.cs](#13-dependencyinjectioncs)
14. [Migrations](#14-migrations)
15. [Tests](#15-tests)
16. [Docs](#16-docs)
17. [Configuration](#17-configuration)
18. [Performance / Load Testing](#18-performance--load-testing)
19. [Docker](#19-docker)

---

## 1. Solution Structure

```
ElshazlyStore.sln
├── Directory.Build.props          (net8.0, nullable, implicit usings, TreatWarningsAsErrors)
├── docker-compose.yml             (PostgreSQL 16 Alpine)
│
├── src/
│   ├── ElshazlyStore.Domain/      (0 dependencies — pure C#)
│   │   ├── Entities/              (29 entity classes)
│   │   ├── Interfaces/            (3 interfaces)
│   │   └── Common/                (ErrorCodes, Permissions)
│   │
│   ├── ElshazlyStore.Infrastructure/  (→ Domain)
│   │   ├── DependencyInjection.cs
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/    (29 IEntityTypeConfiguration files)
│   │   │   └── Interceptors/      (AuditInterceptor)
│   │   ├── Extensions/            (SearchExtensions)
│   │   ├── Seeding/               (AdminSeeder)
│   │   └── Services/              (10 service classes + JwtSettings)
│   │
│   └── ElshazlyStore.Api/         (→ Domain, Infrastructure)
│       ├── Program.cs
│       ├── Endpoints/             (18 endpoint classes + EndpointMapper)
│       ├── Middleware/            (3 middlewares)
│       ├── Authorization/         (3 classes)
│       └── Services/              (CurrentUserService)
│
├── tests/
│   └── ElshazlyStore.Tests/       (→ all 3 projects)
│       ├── TestWebApplicationFactory.cs
│       ├── IntegrationTestCollection.cs
│       ├── Api/                   (12 integration test classes)
│       ├── Domain/                (1 test class)
│       └── Infrastructure/        (1 test class)
│
├── docs/
│   ├── api.md, db.md, operations.md, requirements.md
│   └── templates/                 (CSV + XLSX samples)
│
└── perf/
    ├── load-test.js               (k6 harness)
    └── README.md
```

### NuGet Dependencies

| Project | Package | Purpose |
|---------|---------|---------|
| **Api** | `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.*` | JWT auth |
| **Api** | `Microsoft.EntityFrameworkCore.Design 8.0.*` | Migrations CLI |
| **Api** | `Serilog.AspNetCore 8.0.3` | Structured logging |
| **Api** | `Swashbuckle.AspNetCore 6.9.0` | Swagger/OpenAPI |
| **Infrastructure** | `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.*` | PostgreSQL provider |
| **Infrastructure** | `Microsoft.EntityFrameworkCore 8.0.*` | ORM |
| **Infrastructure** | `ClosedXML 0.102.*` | Excel import |
| **Infrastructure** | `Microsoft.Extensions.Identity.Core 8.0.*` | PasswordHasher |
| **Infrastructure** | `Microsoft.IdentityModel.Tokens 7.1.*` | JWT signing |
| **Infrastructure** | `System.IdentityModel.Tokens.Jwt 7.1.*` | JWT creation |
| **Tests** | `xunit 2.5.3` | Test framework |
| **Tests** | `Microsoft.AspNetCore.Mvc.Testing 8.0.*` | WebApplicationFactory |
| **Tests** | `Microsoft.EntityFrameworkCore.Sqlite 8.0.*` | In-memory test DB |
| **Tests** | `coverlet.collector 6.0.0` | Code coverage |

---

## 2. Entity Classes (Domain)

29 entity classes in `src/ElshazlyStore.Domain/Entities/`:

### 2.1 Identity & Auth

| Entity | File | Key Properties |
|--------|------|----------------|
| **User** | `User.cs` | `Guid Id`, `string Username`, `string PasswordHash`, `bool IsActive`, `DateTime CreatedAtUtc`. Nav: `ICollection<UserRole>`, `ICollection<RefreshToken>` |
| **Role** | `Role.cs` | `Guid Id`, `string Name`, `string? Description`, `DateTime CreatedAtUtc`. Nav: `ICollection<UserRole>`, `ICollection<RolePermission>` |
| **Permission** | `Permission.cs` | `Guid Id`, `string Code`, `string? Description`. Nav: `ICollection<RolePermission>` |
| **UserRole** | `UserRole.cs` | `Guid UserId`, `Guid RoleId` (composite PK). Nav: `User`, `Role` |
| **RolePermission** | `RolePermission.cs` | `Guid RoleId`, `Guid PermissionId` (composite PK). Nav: `Role`, `Permission` |
| **RefreshToken** | `RefreshToken.cs` | `Guid Id`, `Guid UserId`, `string TokenHash` (HMAC-SHA256), `DateTime ExpiresAtUtc/CreatedAtUtc`, `DateTime? RevokedAtUtc`, `string? ReplacedByTokenHash`. Computed: `IsExpired`, `IsRevoked`, `IsActive`. Nav: `User` |
| **AuditLog** | `AuditLog.cs` | `long Id`, `DateTime TimestampUtc`, `Guid? UserId`, `string? Username/IpAddress/UserAgent/CorrelationId`, `string Action/EntityName`, `string? Module/PrimaryKey`, `string? OldValues/NewValues` (JSONB) |
| **SystemInfo** | `SystemInfo.cs` | `int Id`, `string Key`, `string Value`, `DateTime UpdatedAtUtc` |

### 2.2 Master Data (Phase 2)

| Entity | File | Key Properties |
|--------|------|----------------|
| **Product** | `Product.cs` | `Guid Id`, `string Name`, `string? Description/Category`, `bool IsActive`, `DateTime CreatedAtUtc/UpdatedAtUtc?`. Nav: `ICollection<ProductVariant>` |
| **ProductVariant** | `ProductVariant.cs` | `Guid Id`, `Guid ProductId`, `string Sku`, `string? Color/Size`, `decimal? RetailPrice/WholesalePrice`, `bool IsActive`, `decimal? LowStockThreshold`, `DateTime CreatedAtUtc/UpdatedAtUtc?`. Nav: `Product`, `BarcodeReservation?` |
| **BarcodeReservation** | `BarcodeReservation.cs` | `Guid Id`, `string Barcode`, `DateTime ReservedAtUtc`, `Guid? VariantId`, `BarcodeStatus Status` (Reserved→Assigned→Retired). Nav: `ProductVariant?` |
| **Customer** | `Customer.cs` | `Guid Id`, `string Code/Name`, `string? Phone/Phone2/Notes`, `bool IsActive`, `DateTime CreatedAtUtc/UpdatedAtUtc?` |
| **Supplier** | `Supplier.cs` | `Guid Id`, `string Code/Name`, `string? Phone/Phone2/Notes`, `bool IsActive`, `DateTime CreatedAtUtc/UpdatedAtUtc?` |
| **ImportJob** | `ImportJob.cs` | `Guid Id`, `string Type/FileName/FileHash`, `Guid UploadedByUserId`, `ImportJobStatus Status` (Previewed/Committed/Failed), `string? PreviewResultJson/ErrorSummary`, `byte[] FileContent`, `DateTime CreatedAtUtc` |

### 2.3 Inventory (Phase 3)

| Entity | File | Key Properties |
|--------|------|----------------|
| **Warehouse** | `Warehouse.cs` | `Guid Id`, `string Code/Name`, `string? Address`, `bool IsDefault/IsActive`, `DateTime CreatedAtUtc/UpdatedAtUtc?` |
| **StockMovement** | `StockMovement.cs` | `Guid Id`, `MovementType Type`, `string? Reference/Notes`, `DateTime PostedAtUtc`, `Guid CreatedByUserId`. Nav: `User CreatedBy`, `ICollection<StockMovementLine>` |
| **StockMovementLine** | `StockMovementLine.cs` | `Guid Id`, `Guid StockMovementId/VariantId/WarehouseId`, `decimal QuantityDelta`, `decimal? UnitCost`, `string? Reason`. Nav: `StockMovement`, `ProductVariant`, `Warehouse` |
| **StockBalance** | `StockBalance.cs` | `Guid Id`, `Guid VariantId/WarehouseId`, `decimal Quantity`, `DateTime LastUpdatedUtc`. Nav: `ProductVariant`, `Warehouse` |

### 2.4 Procurement (Phase 4)

| Entity | File | Key Properties |
|--------|------|----------------|
| **PurchaseReceipt** | `PurchaseReceipt.cs` | `Guid Id`, `string DocumentNumber`, `Guid SupplierId/WarehouseId`, `string? Notes`, `PurchaseReceiptStatus Status` (Draft/Posted), `Guid? StockMovementId`, `DateTime CreatedAtUtc/PostedAtUtc?`, `Guid CreatedByUserId`. Nav: `Supplier`, `Warehouse`, `User CreatedBy`, `StockMovement?`, `ICollection<PurchaseReceiptLine>` |
| **PurchaseReceiptLine** | `PurchaseReceiptLine.cs` | `Guid Id`, `Guid PurchaseReceiptId/VariantId`, `decimal Quantity/UnitCost`. Nav: `PurchaseReceipt`, `ProductVariant` |
| **SupplierPayable** | `SupplierPayable.cs` | `Guid Id`, `Guid SupplierId/PurchaseReceiptId`, `decimal Amount`, `bool IsPaid`, `DateTime CreatedAtUtc`. Nav: `Supplier`, `PurchaseReceipt` |

### 2.5 Production (Phase 5)

| Entity | File | Key Properties |
|--------|------|----------------|
| **ProductionBatch** | `ProductionBatch.cs` | `Guid Id`, `string BatchNumber`, `Guid WarehouseId`, `string? Notes`, `ProductionBatchStatus Status` (Draft/Posted), `Guid? ConsumeMovementId/ProduceMovementId`, `DateTime CreatedAtUtc/PostedAtUtc?`, `Guid CreatedByUserId`. Nav: `Warehouse`, `User CreatedBy`, `StockMovement? ConsumeMovement/ProduceMovement`, `ICollection<ProductionBatchLine>` |
| **ProductionBatchLine** | `ProductionBatchLine.cs` | `Guid Id`, `Guid ProductionBatchId`, `ProductionLineType LineType` (Input/Output), `Guid VariantId`, `decimal Quantity`, `decimal? UnitCost`. Nav: `ProductionBatch`, `ProductVariant` |

### 2.6 Sales / POS (Phase 6)

| Entity | File | Key Properties |
|--------|------|----------------|
| **SalesInvoice** | `SalesInvoice.cs` | `Guid Id`, `string InvoiceNumber`, `DateTime InvoiceDateUtc`, `Guid? CustomerId`, `Guid WarehouseId/CashierUserId`, `string? Notes`, `SalesInvoiceStatus Status` (Draft/Posted), `Guid? StockMovementId`, `decimal TotalAmount`, `DateTime CreatedAtUtc/PostedAtUtc?`. Nav: `Customer?`, `Warehouse`, `User Cashier`, `StockMovement?`, `ICollection<SalesInvoiceLine>` |
| **SalesInvoiceLine** | `SalesInvoiceLine.cs` | `Guid Id`, `Guid SalesInvoiceId/VariantId`, `decimal Quantity/UnitPrice/DiscountAmount/LineTotal`. Nav: `SalesInvoice`, `ProductVariant` |
| **CustomerReceivable** | `CustomerReceivable.cs` | `Guid Id`, `Guid CustomerId/SalesInvoiceId`, `decimal Amount`, `bool IsPaid`, `DateTime CreatedAtUtc`. Nav: `Customer`, `SalesInvoice` |

### 2.7 Accounting & Payments (Phase 7)

| Entity | File | Key Properties |
|--------|------|----------------|
| **LedgerEntry** | `LedgerEntry.cs` | `Guid Id`, `PartyType PartyType` (Customer/Supplier), `Guid PartyId`, `LedgerEntryType EntryType` (OpeningBalance/Invoice/Payment), `decimal Amount`, `string? Reference/Notes`, `Guid? RelatedInvoiceId/RelatedPaymentId`, `Guid CreatedByUserId`, `DateTime CreatedAtUtc`. Nav: `User CreatedBy` |
| **Payment** | `Payment.cs` | `Guid Id`, `PartyType PartyType`, `Guid PartyId`, `decimal Amount`, `PaymentMethod Method` (Cash/InstaPay/EWallet/Visa), `string? WalletName/Reference`, `DateTime PaymentDateUtc/CreatedAtUtc`, `Guid CreatedByUserId`. Nav: `User CreatedBy` |

### 2.8 Printing Policy (Phase 9)

| Entity | File | Key Properties |
|--------|------|----------------|
| **PrintProfile** | `PrintProfile.cs` | `Guid Id`, `string Name`, `bool IsDefault/IsActive`, `DateTime CreatedAtUtc/UpdatedAtUtc?`, `Guid? CreatedByUserId`. Nav: `ICollection<PrintRule>` |
| **PrintRule** | `PrintRule.cs` | `Guid Id`, `Guid PrintProfileId`, `string ScreenCode`, `string ConfigJson`, `bool Enabled`, `DateTime CreatedAtUtc/UpdatedAtUtc?`, `Guid? CreatedByUserId`. Nav: `PrintProfile` |

---

## 3. Enums

All in `src/ElshazlyStore.Domain/Entities/` (declared within entity files):

| Enum | Values | Used By |
|------|--------|---------|
| `BarcodeStatus` | `Reserved`, `Assigned`, `Retired` | BarcodeReservation |
| `ImportJobStatus` | `Previewed`, `Committed`, `Failed` | ImportJob |
| `PartyType` | `Customer`, `Supplier` | LedgerEntry, Payment |
| `LedgerEntryType` | `OpeningBalance`, `Invoice`, `Payment` | LedgerEntry |
| `PaymentMethod` | `Cash`, `InstaPay`, `EWallet`, `Visa` | Payment |
| `PurchaseReceiptStatus` | `Draft`, `Posted` | PurchaseReceipt |
| `SalesInvoiceStatus` | `Draft`, `Posted` | SalesInvoice |
| `ProductionBatchStatus` | `Draft`, `Posted` | ProductionBatch |
| `ProductionLineType` | `Input`, `Output` | ProductionBatchLine |
| `MovementType` | `OpeningBalance`, `PurchaseReceipt`, `SaleIssue`, `Transfer`, `Adjustment`, `ProductionConsume`, `ProductionProduce` | StockMovement |

---

## 4. Interfaces

3 interfaces in `src/ElshazlyStore.Domain/Interfaces/`:

| Interface | File | Members |
|-----------|------|---------|
| **ICurrentUserService** | `ICurrentUserService.cs` | `Guid? UserId`, `string? Username`, `string? IpAddress`, `string? UserAgent`, `string? CorrelationId` |
| **IPasswordHasher** | `IPasswordHasher.cs` | `string Hash(string password)`, `bool Verify(string password, string hash)` |
| **ITokenService** | `ITokenService.cs` | `string GenerateAccessToken(User user, IReadOnlyList<string> permissionCodes)`, `(string RawToken, RefreshToken Entity) GenerateRefreshToken(Guid userId)`, `string HashToken(string rawToken)` |

---

## 5. Service Implementations (Infrastructure)

10 service classes + 1 settings POCO in `src/ElshazlyStore.Infrastructure/Services/`:

### 5.1 AccountingService (491 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `CreateInvoiceEntryAsync` | `(AppDbContext, PartyType, Guid partyId, decimal amount, Guid? invoiceId, Guid userId)` | Creates positive LedgerEntry (increases outstanding) |
| `CreateOpeningBalanceEntryAsync` | `(AppDbContext, PartyType, Guid partyId, decimal amount, string? ref, string? notes, Guid userId)` | Creates OpeningBalance LedgerEntry |
| `CreatePaymentAsync` | `(AppDbContext, PaymentRequest)` → `Payment` | Validates party exists, validates method (EWallet needs walletName), checks overpay, creates Payment + negative LedgerEntry |
| `ComputeOutstandingAsync` | `(AppDbContext, PartyType, Guid partyId)` → `decimal` | DB-side SUM of ledger entries |
| `GetBalancesAsync` | `(AppDbContext, PartyType, paging)` → paged result | GroupBy aggregation with double casts for SQLite compat |
| `GetLedgerAsync` | `(AppDbContext, PartyType, Guid partyId, paging)` → paged | Ordered by CreatedAtUtc desc |
| `GetPaymentsAsync` | `(AppDbContext, filters, paging)` → paged | Search on reference, filter by party/method/date |
| `GetPaymentByIdAsync` | `(AppDbContext, Guid)` → `Payment?` | Single lookup |

### 5.2 DashboardService (248 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `GetSummaryAsync` | `(AppDbContext, from, to, topN, threshold)` → summary | Aggregates sales, top products, low stock, cashier KPIs. Client-side aggregation for SQLite. |
| `GetSalesSummaryAsync` | `(AppDbContext, from, to)` → sales metrics | TotalSales, InvoiceCount, AverageTicket |
| `GetTopProductsAsync` | `(AppDbContext, from, to, topN)` → list | By quantity and revenue |
| `GetLowStockAlertsAsync` | `(AppDbContext, threshold)` → list | Uses variant-level threshold or global fallback |
| `GetCashierPerformanceAsync` | `(AppDbContext, from, to)` → list | Per-cashier invoice count, total, average |

### 5.3 ImportService (762 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `PreviewAsync` | `(AppDbContext, type, file, userId)` → preview | Parses CSV/XLSX, validates rows, stores ImportJob with FileContent and PreviewResultJson |
| `CommitAsync` | `(AppDbContext, jobId, userId, IPasswordHasher)` → result | Re-validates, persists in batch (500/flush). Supports: Products, Customers, Suppliers, OpeningBalances, Payments |

**Import types**: `Products` (auto-generates barcodes if missing), `Customers` (auto-generates CUST-NNNNNN codes), `Suppliers` (auto-generates SUP-NNNNNN codes), `OpeningBalances` (creates LedgerEntries), `Payments` (creates Payments + LedgerEntries).

### 5.4 StockService (332 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `PostAsync` | `(AppDbContext, request, userId)` → `Guid movementId` | **Serializable isolation**. Validates all variants/warehouses exist. Validates sign rules per MovementType. Transfer: per-variant net-zero + ≥2 warehouses. Creates StockMovement + lines + upserts StockBalance. **Negative stock prevention.** |
| `GetBalancesAsync` | `(AppDbContext, filters, paging)` → paged | Search by SKU/name/barcode/color. Sort by sku/quantity/product/updated. |
| `GetLedgerAsync` | `(AppDbContext, filters, paging)` → paged | Filter by variant/warehouse/date range |

### 5.5 PurchaseService (489 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `CreateAsync` | `(AppDbContext, request, userId)` → receipt | Validates supplier/warehouse/variants, auto-generates doc number via PostgreSQL sequence `PR-{:D6}` |
| `GetAsync` | `(AppDbContext, Guid)` → receipt with lines | Includes supplier, warehouse, lines with variant |
| `ListAsync` | `(AppDbContext, filters, paging)` → paged | Search by document number, filter by status/supplier/date |
| `UpdateAsync` | `(AppDbContext, Guid, request)` → receipt | Draft only — replaces all lines |
| `DeleteAsync` | `(AppDbContext, Guid)` | Draft only |
| `PostAsync` | `(AppDbContext, Guid, userId)` → `Guid movementId` | **Atomic**: `ExecuteUpdateAsync WHERE Status=Draft` (TOCTOU). Creates StockMovement(PurchaseReceipt) + SupplierPayable + AP LedgerEntry. **Idempotent** (returns existing movementId if already posted). |

### 5.6 ProductionService (525 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `CreateAsync` | `(AppDbContext, request, userId)` → batch | Validates warehouse/variants, auto-generates `PB-{:D6}` |
| `GetAsync/ListAsync/UpdateAsync/DeleteAsync` | CRUD | Same patterns as PurchaseService |
| `PostAsync` | `(AppDbContext, Guid, userId)` → `(consumeId, produceId)` | Creates **TWO** movements: ProductionConsume (negative) + ProductionProduce (positive). Same TOCTOU pattern. |

### 5.7 SalesService (600 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| `CreateAsync` | `(AppDbContext, request, userId)` → invoice | Optional customer (null=walk-in), auto-generates `INV-{:D6}`, computes TotalAmount from lines |
| `GetAsync/ListAsync/UpdateAsync/DeleteAsync` | CRUD | Same patterns |
| `PostAsync` | `(AppDbContext, Guid, userId)` → `Guid movementId` | Creates SaleIssue movement (negative) + CustomerReceivable + AR LedgerEntry (if customer provided). Same TOCTOU + idempotent pattern. |

### 5.8 PrintingPolicyService (300 lines)

| Method | Signature | Behavior |
|--------|-----------|----------|
| Profile CRUD | `ListProfiles/GetProfile/CreateProfile/UpdateProfile/DeleteProfile` | Standard CRUD with name uniqueness, cascade-delete rules, set/unset default |
| Rule CRUD | `ListRules/GetRule/CreateRule/UpdateRule/DeleteRule` | One rule per (profile, screenCode) — unique constraint |
| `GetPolicyByScreenCodeAsync` | `(AppDbContext, screenCode, profileId?)` → rule | Lookup from default profile or specific profile |

### 5.9 Other Services

| Service | File | Lines | Lifetime | Behavior |
|---------|------|-------|----------|----------|
| **AppPasswordHasher** | `AppPasswordHasher.cs` | 25 | Singleton | Wraps ASP.NET Core Identity `PasswordHasher<object>` (PBKDF2) |
| **TokenService** | `TokenService.cs` | 84 | Singleton | JWT generation with `permission` claims. HMAC-SHA256 refresh token hashing. 64-byte random tokens. |
| **JwtSettings** | `JwtSettings.cs` | 16 | (bound from config) | `Secret`, `Issuer`, `Audience`, `AccessTokenExpirationMinutes` (15), `RefreshTokenExpirationDays` (7) |

---

## 6. DbContext

**File**: `src/ElshazlyStore.Infrastructure/Persistence/AppDbContext.cs`

- **27 DbSets** covering all entities
- `OnModelCreating`: inline SystemInfo config + `ApplyConfigurationsFromAssembly` for all 29 configuration files
- Constructor accepts `DbContextOptions<AppDbContext>`
- AuditInterceptor added via DI (not hardcoded)

---

## 7. EF Configurations

29 `IEntityTypeConfiguration<T>` files in `Persistence/Configurations/`:

| File | Table | Key Config |
|------|-------|------------|
| `AuditLogConfiguration.cs` | `audit_logs` | JSONB for OldValues/NewValues, indexes on Timestamp/UserId/EntityName |
| `BarcodeReservationConfiguration.cs` | `barcode_reservations` | Unique Barcode, 1:1 with ProductVariant (SetNull on delete), Status as string |
| `CustomerConfiguration.cs` | `customers` | Unique Code, indexes on Name/Phone, MaxLength 300 for Name |
| `CustomerReceivableConfiguration.cs` | `customer_receivables` | numeric(18,4) Amount, unique SalesInvoiceId, cascade from SalesInvoice |
| `ImportJobConfiguration.cs` | `import_jobs` | Status as string, JSONB PreviewResultJson, indexes on FileHash/Status |
| `LedgerEntryConfiguration.cs` | `ledger_entries` | PartyType/EntryType as string, numeric(18,4) Amount, composite index (PartyType,PartyId) |
| `PaymentConfiguration.cs` | `payments` | PartyType/Method as string, numeric(18,4) Amount, composite index (PartyType,PartyId) |
| `PermissionConfiguration.cs` | `permissions` | Unique Code |
| `PrintProfileConfiguration.cs` | `print_profiles` | Unique Name, indexes on IsDefault |
| `PrintRuleConfiguration.cs` | `print_rules` | Unique (ProfileId,ScreenCode), ConfigJson as text, cascade from Profile |
| `ProductConfiguration.cs` | `products` | Indexes on Name/Category |
| `ProductionBatchConfiguration.cs` | `production_batches` | Unique BatchNumber, composite (Status,CreatedAtUtc), FKs to Warehouse/User/ConsumeMovement/ProduceMovement |
| `ProductionBatchLineConfiguration.cs` | `production_batch_lines` | LineType as string, numeric(18,4) Quantity/UnitCost, cascade from batch |
| `ProductVariantConfiguration.cs` | `product_variants` | Unique Sku, numeric(18,2) RetailPrice/WholesalePrice, cascade from Product |
| `PurchaseReceiptConfiguration.cs` | `purchase_receipts` | Unique DocumentNumber, composite (Status,CreatedAtUtc), FKs to Supplier/Warehouse |
| `PurchaseReceiptLineConfiguration.cs` | `purchase_receipt_lines` | numeric(18,4) Quantity/UnitCost, cascade from receipt |
| `RefreshTokenConfiguration.cs` | `refresh_tokens` | Unique TokenHash, cascade from User, ignores computed IsExpired/IsRevoked/IsActive |
| `RoleConfiguration.cs` | `roles` | Unique Name |
| `RolePermissionConfiguration.cs` | `role_permissions` | Composite PK (RoleId,PermissionId), bidirectional cascade |
| `SalesInvoiceConfiguration.cs` | `sales_invoices` | Unique InvoiceNumber, composite (Status,PostedAtUtc) for dashboard, FKs to Customer(optional)/Warehouse/Cashier |
| `SalesInvoiceLineConfiguration.cs` | `sales_invoice_lines` | numeric(18,4) for all monetary fields, cascade from invoice |
| `StockBalanceConfiguration.cs` | `stock_balances` | Unique (VariantId,WarehouseId), numeric(18,4) Quantity |
| `StockMovementConfiguration.cs` | `stock_movements` | Type as string, composite (Type,PostedAtUtc), Reference index |
| `StockMovementLineConfiguration.cs` | `stock_movement_lines` | numeric(18,4) QuantityDelta, composite (VariantId,WarehouseId), cascade from movement |
| `SupplierConfiguration.cs` | `suppliers` | Same structure as CustomerConfiguration |
| `SupplierPayableConfiguration.cs` | `supplier_payables` | numeric(18,4) Amount, unique PurchaseReceiptId, cascade from receipt |
| `UserConfiguration.cs` | `users` | Unique Username, MaxLength 512 PasswordHash |
| `UserRoleConfiguration.cs` | `user_roles` | Composite PK (UserId,RoleId), bidirectional cascade |
| `WarehouseConfiguration.cs` | `warehouses` | Unique Code |

---

## 8. Endpoints (API)

18 endpoint classes + 1 mapper in `src/ElshazlyStore.Api/Endpoints/`:

### Route Map (all under `/api/v1`)

| Group | File | Routes | Permission(s) |
|-------|------|--------|----------------|
| **Health** | `HealthEndpoints.cs` | `GET /health` | Anonymous |
| **Auth** | `AuthEndpoints.cs` | `POST /auth/login`, `POST /auth/refresh` (anon); `POST /auth/logout`, `GET /auth/me` (authorized) | None / Authenticated |
| **Users** | `UserEndpoints.cs` | `GET/POST /users`, `GET/PUT/DELETE /users/{id}` | `USERS_READ` / `USERS_WRITE` |
| **Roles** | `RoleEndpoints.cs` | `GET/POST /roles`, `GET/PUT/DELETE /roles/{id}`, `GET/PUT /roles/{id}/permissions`, `GET /roles/permissions/all` | `ROLES_READ` / `ROLES_WRITE` |
| **Products** | `ProductEndpoints.cs` | `GET/POST /products`, `GET/PUT/DELETE /products/{id}` | `PRODUCTS_READ` / `PRODUCTS_WRITE` |
| **Variants** | `VariantEndpoints.cs` | `GET/POST /variants`, `GET/PUT/DELETE /variants/{id}` | `PRODUCTS_READ` / `PRODUCTS_WRITE` |
| **Barcodes** | `BarcodeEndpoints.cs` | `GET /barcodes/{barcode}` | `PRODUCTS_READ` (cached 60s) |
| **Customers** | `CustomerEndpoints.cs` | `GET/POST /customers`, `GET/PUT/DELETE /customers/{id}` | `CUSTOMERS_READ` / `CUSTOMERS_WRITE` |
| **Suppliers** | `SupplierEndpoints.cs` | `GET/POST /suppliers`, `GET/PUT/DELETE /suppliers/{id}` | `SUPPLIERS_READ` / `SUPPLIERS_WRITE` |
| **Import** | `ImportEndpoints.cs` | `POST /imports/masterdata/preview`, `/commit`; `POST /imports/opening-balances/preview`, `/commit`; `POST /imports/payments/preview`, `/commit` | `IMPORT_MASTER_DATA` / `IMPORT_OPENING_BALANCES` / `IMPORT_PAYMENTS` |
| **Warehouses** | `WarehouseEndpoints.cs` | `GET/POST /warehouses`, `GET/PUT/DELETE /warehouses/{id}` | `WAREHOUSES_READ` / `WAREHOUSES_WRITE` |
| **Stock Movements** | `StockMovementEndpoints.cs` | `POST /stock-movements/post` | `STOCK_POST` |
| **Stock** | `StockEndpoints.cs` | `GET /stock/balances`, `GET /stock/ledger` | `STOCK_READ` |
| **Purchases** | `PurchaseEndpoints.cs` | `GET/POST /purchases`, `GET/PUT/DELETE /purchases/{id}`, `POST /purchases/{id}/post` | `PURCHASES_READ` / `PURCHASES_WRITE` / `PURCHASES_POST` |
| **Production** | `ProductionEndpoints.cs` | `GET/POST /production`, `GET/PUT/DELETE /production/{id}`, `POST /production/{id}/post` | `PRODUCTION_READ` / `PRODUCTION_WRITE` / `PRODUCTION_POST` |
| **Sales** | `SalesEndpoints.cs` | `GET/POST /sales`, `GET/PUT/DELETE /sales/{id}`, `POST /sales/{id}/post` | `SALES_READ` / `SALES_WRITE` / `SALES_POST` |
| **Accounting** | `AccountingEndpoints.cs` | `GET /accounting/balances/customers`, `/suppliers`, `/{partyType}/{partyId}`, `GET /accounting/ledger/{partyType}/{partyId}` | `ACCOUNTING_READ` |
| **Payments** | `PaymentEndpoints.cs` | `GET/POST /payments`, `GET /payments/{id}` | `PAYMENTS_READ` / `PAYMENTS_WRITE` |
| **Dashboard** | `DashboardEndpoints.cs` | `GET /dashboard/summary`, `/sales`, `/top-products`, `/low-stock`, `/cashier-performance` | `DASHBOARD_READ` |
| **Printing Policy** | `PrintingPolicyEndpoints.cs` | `GET/POST /print-profiles`, `GET/PUT/DELETE /print-profiles/{id}`, `GET/POST /print-profiles/{id}/rules`, `GET/PUT/DELETE /print-profiles/{id}/rules/{ruleId}`, `GET /print-policy/{screenCode}` | `MANAGE_PRINTING_POLICY` |

**EndpointMapper.cs**: `MapApiEndpoints()` extension maps all groups under `/api/v1` with `.WithTags()`.

---

## 9. Program.cs (Pipeline)

**File**: `src/ElshazlyStore.Api/Program.cs`

Pipeline order:

```
1. Serilog Bootstrap Logger
2. WebApplication.CreateBuilder()
3. builder.Host.UseSerilog() — from appsettings
4. builder.Services.AddInfrastructure(config) — EF, services, DI
5. AddMemoryCache()
6. AddResponseCompression (Gzip, EnableForHttps)
7. Kestrel: MaxRequestBodySize from config
8. AddHttpContextAccessor()
9. AddScoped<ICurrentUserService, CurrentUserService>()
10. AddAuthentication(JwtBearer) — validate issuer/audience/lifetime/signing key
11. AddAuthorization()
12. AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
13. AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>()
14. AddEndpointsApiExplorer() + AddSwaggerGen()
15. Build()
16. Migrate DB (MigrateAsync)
17. Seed (AdminSeeder.SeedAsync)
18. app.UseResponseCompression()
19. app.UseMiddleware<CorrelationIdMiddleware>()
20. app.UseMiddleware<RequestTimingMiddleware>()
21. app.UseMiddleware<GlobalExceptionMiddleware>()
22. app.UseAuthentication()
23. app.UseAuthorization()
24. Swagger UI (Development only)
25. app.MapApiEndpoints()
26. app.Run()
```

---

## 10. Authorization

3 files in `src/ElshazlyStore.Api/Authorization/`:

| File | Class | Role |
|------|-------|------|
| `PermissionRequirement.cs` | `PermissionRequirement : IAuthorizationRequirement` | Holds `string PermissionCode` |
| `PermissionPolicyProvider.cs` | `PermissionPolicyProvider : IAuthorizationPolicyProvider` | Dynamically creates policies from `"Permission:CODE"` format. Endpoint declarations use `.RequireAuthorization("Permission:USERS_READ")` |
| `PermissionAuthorizationHandler.cs` | `PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>` | Checks JWT `"permission"` claims against the required code |

### 33 Permission Codes (from `Permissions.cs`)

Organized in phases:

| Phase | Codes |
|-------|-------|
| 1 — Identity | `USERS_READ`, `USERS_WRITE`, `ROLES_READ`, `ROLES_WRITE`, `AUDIT_READ` |
| 2 — Master Data | `PRODUCTS_READ`, `PRODUCTS_WRITE`, `CUSTOMERS_READ`, `CUSTOMERS_WRITE`, `SUPPLIERS_READ`, `SUPPLIERS_WRITE`, `IMPORT_MASTER_DATA` |
| 3 — Inventory | `STOCK_READ`, `STOCK_POST`, `WAREHOUSES_READ`, `WAREHOUSES_WRITE` |
| 4 — Procurement | `PURCHASES_READ`, `PURCHASES_WRITE`, `PURCHASES_POST` |
| 5 — Production | `PRODUCTION_READ`, `PRODUCTION_WRITE`, `PRODUCTION_POST` |
| 6 — Sales | `SALES_READ`, `SALES_WRITE`, `SALES_POST` |
| 7 — Accounting | `ACCOUNTING_READ`, `PAYMENTS_READ`, `PAYMENTS_WRITE`, `IMPORT_OPENING_BALANCES`, `IMPORT_PAYMENTS` |
| 8 — Dashboard | `DASHBOARD_READ` |
| 9 — Printing | `MANAGE_PRINTING_POLICY` |

---

## 11. Middleware

3 middlewares in `src/ElshazlyStore.Api/Middleware/`:

| Middleware | File | Behavior |
|------------|------|----------|
| **CorrelationIdMiddleware** | `CorrelationIdMiddleware.cs` | Reads/generates `X-Correlation-Id` header. Attaches to response. Pushes to Serilog `LogContext`. |
| **RequestTimingMiddleware** | `RequestTimingMiddleware.cs` | Measures elapsed time. Logs `Warning` if exceeds configurable `SlowRequestThresholdMs` (default 500ms). |
| **GlobalExceptionMiddleware** | `GlobalExceptionMiddleware.cs` | Catches all unhandled exceptions. Returns RFC 7807 `ProblemDetails` with stable error codes. Masks exception details in non-Development. Maps specific exception types to HTTP status codes. |

---

## 12. Seeding

**File**: `src/ElshazlyStore.Infrastructure/Seeding/AdminSeeder.cs`

`SeedAsync(IServiceProvider)`:

1. Seeds **all 33 permissions** from `Permissions.All` (upsert by Code)
2. Creates **Admin role** with ALL permissions (if not exists)
3. Creates **admin user** (password from `ADMIN_DEFAULT_PASSWORD` config, default `"Admin@123!"`)
4. Assigns Admin role to admin user
5. Creates **default warehouse** (Code: `MAIN`, Name: `Main Warehouse`, IsDefault: true)

---

## 13. DependencyInjection.cs

**File**: `src/ElshazlyStore.Infrastructure/DependencyInjection.cs`

`AddInfrastructure(IServiceCollection, IConfiguration)`:

| Registration | Lifetime | Notes |
|-------------|----------|-------|
| `AppDbContext` | Scoped | Npgsql, retry on failure (3), configurable command timeout |
| `AuditInterceptor` | Scoped | Added to DbContext via DI |
| `JwtSettings` | (Options bind) | From `Jwt` config section |
| `IPasswordHasher` → `AppPasswordHasher` | Singleton | |
| `ITokenService` → `TokenService` | Singleton | |
| `ImportService` | Scoped | |
| `StockService` | Scoped | |
| `AccountingService` | Scoped | |
| `PurchaseService` | Scoped | |
| `ProductionService` | Scoped | |
| `SalesService` | Scoped | |
| `DashboardService` | Scoped | |
| `PrintingPolicyService` | Scoped | |

---

## 14. Migrations

12 migrations in `src/ElshazlyStore.Infrastructure/Persistence/Migrations/`:

| Migration | Phase | Description |
|-----------|-------|-------------|
| `0001_initial` | 0 | SystemInfo table |
| `0002_identity_audit` | 1 | Users, Roles, Permissions, UserRoles, RolePermissions, RefreshTokens, AuditLogs |
| `0003_masterdata_barcodes_import` | 2 | Products, ProductVariants, BarcodeReservations, Customers, Suppliers, ImportJobs |
| `0004_inventory_core` | 3 | Warehouses, StockMovements, StockMovementLines, StockBalances |
| `0005_procurement` | 4 | PurchaseReceipts, PurchaseReceiptLines, SupplierPayables |
| `0006_production` | 5 | ProductionBatches, ProductionBatchLines |
| `0007_pos_sales` | 6 | SalesInvoices, SalesInvoiceLines, CustomerReceivables |
| `0008_accounting_payments` | 7 | LedgerEntries, Payments |
| `0009_dashboard_low_stock_threshold` | 8 | `LowStockThreshold` column on ProductVariants, dashboard composite index |
| `0010_printing_policy` | 9 | PrintProfiles, PrintRules |
| `0011_hardening_indexes` | 10 | Additional B-tree indexes for performance |
| `0012_perf_indexes_trgm` | 11 | `pg_trgm` extension, GIN trigram indexes on all searchable columns, PostgreSQL sequences for doc numbers, unique filtered Reference index |

---

## 15. Tests

**Framework**: xUnit 2.5.3 + `WebApplicationFactory` + SQLite in-memory  
**Shared collection**: `"Integration"` — all test classes share one `TestWebApplicationFactory` instance (prevents Serilog conflicts)

### Test Infrastructure

| File | Lines | Purpose |
|------|-------|---------|
| `TestWebApplicationFactory.cs` | 82 | Replaces Npgsql with SQLite in-memory. Shared connection kept open for factory lifetime. Sets JWT/admin config. Re-adds AuditInterceptor. |
| `IntegrationTestCollection.cs` | 10 | xUnit `ICollectionFixture<TestWebApplicationFactory>` definition |

### Integration Tests (Api/)

| File | Tests | What's Tested |
|------|-------|---------------|
| `HealthEndpointTests.cs` | 3 | Health returns OK, Correlation-Id header present, forwarded |
| `AuthEndpointTests.cs` | 11 | Login (valid/invalid/empty), `/me` (with/without token), refresh (valid/invalid), token rotation, DB never stores raw tokens, audit never contains password hash |
| `PermissionEnforcementTests.cs` | 5 | 401 without token (Theory), admin can list users/roles/permissions, create user |
| `BarcodeTests.cs` | 6 | Create variant with barcode, duplicate barcode 409, delete retires barcode, lookup existing/nonexistent/retired |
| `ImportTests.cs` | 6 | Preview detects errors (products CSV, duplicate customer code, duplicate barcode), commit succeeds, permission enforcement (preview + commit) |
| `StockMovementTests.cs` | 13 | Post (purchase/sale/transfer/adjustment), negative stock rejected, empty lines 400, invalid variant 404, ledger history, permission enforcement, transfer validations (single-positive, different-variants, valid two-warehouse) |
| `PurchaseReceiptTests.cs` | 12 | Create, post (creates movement + updates balance), idempotent post, concurrent double-post (only one movement), multi-line, get/list/update/delete (draft only), posted fails delete, invalid supplier 404, auth required |
| `ProductionBatchTests.cs` | 12 | Create, post (consumes + produces), insufficient raw material rejected, idempotent, concurrent double-post, multiple I/O, get/list/update/delete (draft only), posted fails delete, auth required |
| `SalesInvoiceTests.cs` | 15 | Walk-in and customer invoices, get/list/update/delete, post (reduces stock, insufficient rejected, idempotent, concurrent), immutable invoice number, with-customer creates receivable, multi-line, barcode pricing defaults, auth required |
| `AccountingPaymentTests.cs` | 15 | Post sales creates ledger entry, post purchase creates ledger entry, payment reduces outstanding, e-wallet requires/accepts wallet name, all payment methods, overpay disallowed, import opening balances/payments (permission + validation + commit), get ledger, get customer/supplier balances |
| `DashboardTests.cs` | 12 | Sales metrics, date range filtering, top products (by qty/revenue), low stock alerts (above/below threshold, variant threshold), cashier performance, summary sections, permission enforcement, default date range |
| `PrintingPolicyTests.cs` | 20 | Profile CRUD (permission, create/duplicate/empty/get/update/delete/cascade/search), Rule CRUD (create/duplicate/invalid-profile/update/delete), policy lookup (default/specific/no-match), set default unsets others |

### Domain Tests (Domain/)

| File | Tests | What's Tested |
|------|-------|---------------|
| `ErrorCodesTests.cs` | 2 | All error code constants are non-empty, all are UPPER_SNAKE_CASE |

### Infrastructure Tests (Infrastructure/)

| File | Tests | What's Tested |
|------|-------|---------------|
| `PasswordHasherTests.cs` | 5 | Hash returns non-empty, same password produces different hashes (salting), verify correct/wrong/empty password |

### Test Totals

- **15 test files**, ~3,814 lines total
- **137 test methods** (136 `[Fact]` + 1 `[Theory]`)
- **Coverage**: All major flows covered — auth, CRUD, posting, concurrency, permissions, imports, dashboard, printing policy

---

## 16. Docs

4 markdown files + 6 template files in `docs/`:

| File | Lines | Content |
|------|-------|---------|
| `requirements.md` | ~200 | Core architecture principles (server-only truth, strict invariants, audit, permissions), tech stack table, future UI/AI/outbox plans, phase roadmap (0-7), detailed Phase 2-3 requirements |
| `api.md` | ~842 | Complete API reference — every endpoint, request/response examples, all 33 permission codes, full error code table (40+ codes with HTTP status/description) |
| `db.md` | ~200 | Database schema reference — all tables with columns, all indexes (B-tree + GIN trigram), sequences, migration list, audit details, data type conventions |
| `operations.md` | ~311 | Deployment (Kestrel/Docker), config reference, production checklist, DB backup/restore (Linux + Windows), monitoring, security (auth flow, permission model), troubleshooting, performance tuning |

### Template Files (docs/templates/)

| File | Format | Content |
|------|--------|---------|
| `products_variants_sample.csv/.xlsx` | CSV+XLSX | 8 sample rows: ProductName, SKU, Barcode, Color, Size, RetailPrice, WholesalePrice, Description, Category |
| `customers_sample.csv/.xlsx` | CSV+XLSX | 5 sample rows: Name, Code, Phone, Phone2, Notes |
| `suppliers_sample.csv/.xlsx` | CSV+XLSX | 4 sample rows: Name, Code, Phone, Phone2, Notes |

---

## 17. Configuration

### appsettings.json (Production defaults)

```json
{
  "ConnectionStrings:DefaultConnection": "Host=localhost;Port=5432;Database=elshazlystore;Username=postgres;Password=postgres",
  "Jwt:Secret": "CHANGE-THIS-...",
  "Jwt:AccessTokenExpirationMinutes": 15,
  "Jwt:RefreshTokenExpirationDays": 7,
  "Serilog:MinimumLevel:Default": "Information",
  "RequestLimits:MaxRequestBodyMB": 10,
  "Performance:CommandTimeoutSeconds": 30,
  "Performance:SlowRequestThresholdMs": 500
}
```

### appsettings.Development.json

```json
{
  "ConnectionStrings:DefaultConnection": "..._dev",
  "Jwt:AccessTokenExpirationMinutes": 60,
  "Jwt:RefreshTokenExpirationDays": 30,
  "ADMIN_DEFAULT_PASSWORD": "Admin@123!",
  "Performance:CommandTimeoutSeconds": 60,
  "Performance:SlowRequestThresholdMs": 1000,
  "Serilog:MinimumLevel:Default": "Debug"
}
```

### Directory.Build.props

```xml
net8.0, Nullable=enable, ImplicitUsings=enable, TreatWarningsAsErrors=true
```

---

## 18. Performance / Load Testing

**Harness**: k6 (JavaScript) in `perf/load-test.js`

**Scenarios tested**: Health, product list, product search, stock balances, barcode lookup, customers list, dashboard summary.

**Thresholds**: p95 < 500ms, error rate < 5%.

**Configuration**: `BASE_URL`, `USERNAME`, `PASSWORD` via env vars.

**Setup**: Authenticates once, shares JWT across all VUs.

---

## 19. Docker

**File**: `docker-compose.yml`

```yaml
services:
  postgres:
    image: postgres:16-alpine
    ports: 5432:5432
    env: POSTGRES_USER=postgres, POSTGRES_PASSWORD=postgres, POSTGRES_DB=elshazlystore_dev
    volumes: pgdata (persistent)
```

No API container defined — intended for local dev (API runs directly via `dotnet run`).

---

## Appendix: Key Architectural Patterns

| Pattern | Implementation |
|---------|---------------|
| **TOCTOU-safe posting** | `ExecuteUpdateAsync(...WHERE Status = Draft)` returns affected rows — if 0, another request won the race |
| **Idempotent posts** | If entity already has StockMovementId, return 200 with existing ID |
| **Serializable isolation** | Stock movements use `IsolationLevel.Serializable` to prevent phantom reads |
| **Negative stock prevention** | Checked within the serializable transaction before committing |
| **Append-only ledger** | StockMovementLines are immutable after posting |
| **Refresh token rotation** | Old token revoked + `ReplacedByTokenHash` chain |
| **Audit interceptor** | `SaveChangesInterceptor` captures all INSERT/UPDATE/DELETE (except AuditLog/RefreshToken), redacts sensitive fields |
| **Provider-aware search** | `SearchExtensions` builds `ILIKE` (PostgreSQL) or `LIKE + LOWER` (SQLite) expression trees |
| **Batch import** | 500 entities per `SaveChanges` flush to manage change-tracker overhead |
| **GIN trigram indexes** | `pg_trgm` extension for fast `LIKE '%pattern%'` on all searchable text columns |
| **PostgreSQL sequences** | Gap-free document numbering (`INV-`, `PR-`, `PB-` prefixes) with SQLite fallback (COUNT-based) |
| **RFC 7807 errors** | All errors return `ProblemDetails` with stable, machine-readable `errorCode` |
| **Barcode lifecycle** | Reserved → Assigned → Retired (terminal, never reusable) |
