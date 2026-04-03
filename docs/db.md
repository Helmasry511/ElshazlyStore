# ElshazlyStore — Database Schema Reference

> PostgreSQL 15+ · EF Core 8 · All tables use snake_case naming.

---

## Connection

```
Host=localhost;Port=5432;Database=elshazly_store;Username=xxx;Password=xxx
```

Configured via `ConnectionStrings:DefaultConnection` in `appsettings.json`.

---

## Tables

### Identity & Auth

| Table              | Description                           | Key Columns                                           |
|--------------------|---------------------------------------|-------------------------------------------------------|
| `users`            | Application users                     | `Id`, `Username` (unique), `PasswordHash`, `IsActive` |
| `roles`            | Authorization roles                   | `Id`, `Name` (unique)                                 |
| `permissions`      | Permission codes                      | `Id`, `Code` (unique)                                 |
| `user_roles`       | User ↔ Role join table                | `UserId`, `RoleId` (composite PK)                     |
| `role_permissions`  | Role ↔ Permission join table         | `RoleId`, `PermissionId` (composite PK)               |
| `refresh_tokens`   | Hashed refresh tokens for JWT rotation| `Id`, `UserId`, `TokenHash` (unique), `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByTokenHash` |
| `audit_logs`       | Immutable change audit trail          | `Id` (bigint), `Action`, `EntityName`, `PrimaryKey`, `OldValues` (JSONB), `NewValues` (JSONB) |
| `system_info`      | Key-value system metadata             | `Id`, `Key` (unique), `Value`                         |

### Master Data (Phase 2)

| Table                | Description                         | Key Columns                                           |
|----------------------|-------------------------------------|-------------------------------------------------------|
| `products`           | Product catalog                     | `Id`, `Name`, `Category`, `IsActive`                  |
| `product_variants`   | SKU-level variants (color/size)     | `Id`, `ProductId` (FK), `Sku` (unique), `RetailPrice`, `WholesalePrice`, `LowStockThreshold` |
| `barcode_reservations`| Pre-allocated barcodes             | `Id`, `Barcode` (unique), `VariantId` (FK, nullable), `Status`                    |
| `customers`          | Customer records                    | `Id`, `Code` (unique), `Name`, `Phone`                |
| `suppliers`          | Supplier records                    | `Id`, `Code` (unique), `Name`, `Phone`                |
| `import_jobs`        | CSV/XLSX import tracking            | `Id`, `Type`, `FileName`, `Status`, `PreviewJson`     |

### Inventory (Phase 3)

| Table                | Description                         | Key Columns                                           |
|----------------------|-------------------------------------|-------------------------------------------------------|
| `warehouses`         | Physical/logical storage locations  | `Id`, `Code` (unique), `Name`, `IsDefault`, `IsActive`|
| `stock_movements`    | Movement headers                    | `Id`, `Type`, `Reference`, `Notes`, `PostedAtUtc`, `CreatedByUserId` (FK)         |
| `stock_movement_lines`| Movement detail lines              | `Id`, `StockMovementId` (FK), `VariantId` (FK), `WarehouseId` (FK), `QuantityDelta`, `UnitCost` |
| `stock_balances`     | Current stock per variant+warehouse | `Id`, `VariantId` + `WarehouseId` (unique composite), `Quantity` numeric(18,4) |

### Procurement (Phase 4)

| Table                  | Description                       | Key Columns                                           |
|------------------------|-----------------------------------|-------------------------------------------------------|
| `purchase_receipts`    | Purchase receipt headers          | `Id`, `DocumentNumber` (unique), `SupplierId` (FK), `WarehouseId` (FK), `Status`, `StockMovementId` |
| `purchase_receipt_lines`| Receipt line items               | `Id`, `PurchaseReceiptId` (FK), `VariantId` (FK), `Quantity`, `UnitCost` |
| `supplier_payables`    | AP payable records                | `Id`, `SupplierId` (FK), `PurchaseReceiptId` (FK), `Amount` |

### Production (Phase 5)

| Table                    | Description                     | Key Columns                                           |
|--------------------------|---------------------------------|-------------------------------------------------------|
| `production_batches`     | Production batch headers        | `Id`, `BatchNumber` (unique), `WarehouseId` (FK), `Status`, `StockMovementId` |
| `production_batch_lines` | Input/output lines              | `Id`, `ProductionBatchId` (FK), `VariantId` (FK), `LineType` (Input/Output), `Quantity`, `UnitCost` |

### Sales / POS (Phase 6)

| Table                  | Description                       | Key Columns                                           |
|------------------------|-----------------------------------|-------------------------------------------------------|
| `sales_invoices`       | Sales invoice headers             | `Id`, `InvoiceNumber` (unique), `CustomerId` (FK), `WarehouseId` (FK), `CashierUserId`, `Status`, `TotalAmount`, `StockMovementId` |
| `sales_invoice_lines`  | Invoice line items                | `Id`, `SalesInvoiceId` (FK), `VariantId` (FK), `Quantity`, `UnitPrice`, `DiscountAmount`, `LineTotal` |
| `customer_receivables`  | AR receivable records            | `Id`, `CustomerId` (FK), `SalesInvoiceId` (FK), `Amount` |

### Accounting & Payments (Phase 7)

| Table            | Description                         | Key Columns                                           |
|------------------|-------------------------------------|-------------------------------------------------------|
| `ledger_entries` | Double-entry style AR/AP ledger     | `Id`, `PartyType` (Customer/Supplier), `PartyId`, `EntryType`, `Amount` numeric(18,4), `RelatedInvoiceId`, `RelatedPaymentId` |
| `payments`       | Payment records                     | `Id`, `PartyType`, `PartyId`, `Amount` numeric(18,4), `Method`, `WalletName`, `PaymentDateUtc` |

### Printing Policy (Phase 9)

| Table            | Description                         | Key Columns                                           |
|------------------|-------------------------------------|-------------------------------------------------------|
| `print_profiles` | Named printing profile groups       | `Id`, `Name` (unique), `IsDefault`, `IsActive`        |
| `print_rules`    | Per-screen printing rules           | `Id`, `PrintProfileId` (FK, cascade), `ScreenCode`, `ConfigJson` (text), `Enabled` |

### Returns & Dispositions (Phase RET)

| Table                         | Description                          | Key Columns                                           |
|-------------------------------|--------------------------------------|-------------------------------------------------------|
| `reason_codes`                | Admin-managed reason catalog         | `Id`, `Code` (unique), `NameAr`, `Category` (General/SalesReturn/PurchaseReturn/Disposition), `IsActive`, `RequiresManagerApproval` |
| `sales_returns`               | Customer return headers              | `Id`, `ReturnNumber` (unique), `CustomerId` (FK, nullable), `OriginalSalesInvoiceId` (FK, nullable), `WarehouseId` (FK), `Status` (Draft/Posted/Voided), `TotalAmount`, `StockMovementId` (FK, nullable), audit fields |
| `sales_return_lines`          | Customer return line items           | `Id`, `SalesReturnId` (FK), `VariantId` (FK), `Quantity`, `UnitPrice`, `LineTotal`, `ReasonCodeId` (FK), `DispositionType`, `Notes` |
| `purchase_returns`            | Supplier return headers              | `Id`, `ReturnNumber` (unique), `SupplierId` (FK), `OriginalPurchaseReceiptId` (FK, nullable), `WarehouseId` (FK), `Status` (Draft/Posted/Voided), `TotalAmount`, `StockMovementId` (FK, nullable), audit fields |
| `purchase_return_lines`       | Supplier return line items           | `Id`, `PurchaseReturnId` (FK), `VariantId` (FK), `Quantity`, `UnitCost`, `LineTotal`, `ReasonCodeId` (FK), `DispositionType`, `Notes` |
| `inventory_dispositions`      | Pre-sale disposition headers         | `Id`, `DispositionNumber` (unique), `WarehouseId` (FK), `Status` (Draft/Posted/Voided), `StockMovementId` (FK, nullable), `RowVersion` (concurrency), `ApprovedByUserId`, `ApprovedAtUtc`, audit fields |
| `inventory_disposition_lines` | Pre-sale disposition line items      | `Id`, `InventoryDispositionId` (FK), `VariantId` (FK), `Quantity`, `ReasonCodeId` (FK), `DispositionType` (Scrap/Quarantine/Rework/WriteOff), `Notes` |

---

## Indexes

### Identity
- `users`: `Username` (unique)
- `refresh_tokens`: `TokenHash` (unique), `UserId`
- `audit_logs`: `EntityName`, `TimestampUtc`, `UserId`

### Master Data
- `products`: `Name`, **GIN trgm** on `LOWER(Name)`
- `product_variants`: `Sku` (unique), `ProductId`, **GIN trgm** on `LOWER(Sku)`
- `barcode_reservations`: `Barcode` (unique), `VariantId` (filtered non-null)
- `customers`: `Code` (unique), `Name`, `Phone`, **GIN trgm** on `LOWER(Name)`, `LOWER(Code)`, `LOWER(Phone)`
- `suppliers`: `Code` (unique), `Name`, `Phone`, **GIN trgm** on `LOWER(Name)`, `LOWER(Code)`, `LOWER(Phone)`

### Inventory
- `stock_balances`: `(VariantId, WarehouseId)` (unique)
- `stock_movements`: `Type`, `PostedAtUtc`, `Reference`, **(Type, PostedAtUtc)** composite, **Reference** (unique filtered WHERE NOT NULL — idempotency)
- `stock_movement_lines`: `StockMovementId`, `VariantId`
- `warehouses`: **GIN trgm** on `LOWER(Name)`, `LOWER(Code)`

### Procurement
- `purchase_receipts`: `DocumentNumber` (unique), `SupplierId`, `Status`, `CreatedAtUtc`, **(Status, CreatedAtUtc)** composite, **GIN trgm** on `LOWER(DocumentNumber)`
- `purchase_receipt_lines`: `PurchaseReceiptId`, `VariantId`

### Production
- `production_batches`: `BatchNumber` (unique), `Status`, `CreatedAtUtc`, **(Status, CreatedAtUtc)** composite, **GIN trgm** on `LOWER(BatchNumber)`
- `production_batch_lines`: `ProductionBatchId`, `VariantId`

### Sales
- `sales_invoices`: `InvoiceNumber` (unique), `CustomerId`, `CashierUserId`, `Status`, `(Status, PostedAtUtc)` composite for dashboard, **GIN trgm** on `LOWER(InvoiceNumber)`
- `sales_invoice_lines`: `SalesInvoiceId`, `VariantId`

### Accounting
- `ledger_entries`: `(PartyType, PartyId)`, `RelatedInvoiceId`, `RelatedPaymentId`, `CreatedAtUtc`, `EntryType`
- `payments`: `(PartyType, PartyId)`, `PaymentDateUtc`, `Method`, **GIN trgm** on `LOWER(Reference)`

### Printing Policy
- `print_profiles`: `Name` (unique), `IsDefault`, **GIN trgm** on `LOWER(Name)`
- `print_rules`: `(PrintProfileId, ScreenCode)` (unique), `ScreenCode`

### Returns & Dispositions
- `reason_codes`: `Code` (unique), `Category`, `IsActive`
- `sales_returns`: `ReturnNumber` (unique), `Status`, `(Status, PostedAtUtc)` composite, **GIN trgm** on `LOWER(ReturnNumber)`, `CustomerId`, `OriginalSalesInvoiceId`
- `sales_return_lines`: `SalesReturnId`, `VariantId`, `ReasonCodeId`
- `purchase_returns`: `ReturnNumber` (unique), `Status`, `(Status, PostedAtUtc)` composite, **GIN trgm** on `LOWER(ReturnNumber)`, `SupplierId`, `OriginalPurchaseReceiptId`
- `purchase_return_lines`: `PurchaseReturnId`, `VariantId`, `ReasonCodeId`
- `inventory_dispositions`: `DispositionNumber` (unique), `Status`, `(Status, PostedAtUtc)` composite, **GIN trgm** on `LOWER(DispositionNumber)`, `WarehouseId`
- `inventory_disposition_lines`: `InventoryDispositionId`, `VariantId`, `ReasonCodeId`

### Extensions

- **pg_trgm** — installed by migration `0012_perf_indexes_trgm`. Enables GIN trigram indexes for fast `LIKE '%pattern%'` / `ILIKE` text search on all user-searchable columns.

---

## Sequences

PostgreSQL sequences for atomic, gap-free document numbering. Created by migrations `0012_perf_indexes_trgm` and `0016_ret4_perf_indexes`.

| Sequence                         | Format         | Used By                  |
|----------------------------------|----------------|--------------------------|
| `sales_invoice_number_seq`       | `INV-{:D6}`    | `SalesService`           |
| `purchase_receipt_number_seq`    | `PR-{:D6}`     | `PurchaseService`        |
| `production_batch_number_seq`    | `PB-{:D6}`     | `ProductionService`      |
| `sales_return_number_seq`        | `SRET-{:D6}`   | `SalesReturnService`     |
| `purchase_return_number_seq`     | `PRET-{:D6}`   | `PurchaseReturnService`  |
| `disposition_number_seq`         | `DISP-{:D6}`   | `DispositionService`     |
Sequences are seeded on migration to advance past existing auto-generated numbers. Services call `nextval()` on PostgreSQL; tests on SQLite use a COUNT-based fallback.

---

## Migrations

| Migration                                        | Phase | Description                                    |
|--------------------------------------------------|-------|------------------------------------------------|
| `0001_initial`                                    | 0     | System info, health                            |
| `0002_identity_audit`                             | 1     | Users, roles, permissions, audit logs          |
| `0003_masterdata_barcodes_import`                 | 2     | Products, variants, barcodes, customers, suppliers, imports |
| `0004_inventory_core`                             | 3     | Warehouses, stock movements, stock balances    |
| `0005_procurement`                                | 4     | Purchase receipts, supplier payables           |
| `0006_production`                                 | 5     | Production batches, batch lines                |
| `0007_pos_sales`                                  | 6     | Sales invoices, customer receivables           |
| `0008_accounting_payments`                        | 7     | Ledger entries, payments                       |
| `0009_dashboard_low_stock_threshold`              | 8     | LowStockThreshold column, dashboard index      |
| `0010_printing_policy`                            | 9     | Print profiles, print rules                    |
| `0011_hardening_indexes`                          | 10    | Additional performance indexes                 |
| `0012_perf_indexes_trgm`                          | 11    | pg_trgm extension, GIN trigram indexes (incl. phone), B-tree composites, unique filtered Reference, PostgreSQL sequences |
| `0013_reason_codes`                               | RET 0 | Reason codes catalog (code unique, category, requiresManagerApproval) |
| `0014_sales_returns`                              | RET 1 | Sales returns + lines with FKs to customers, invoices, stock movements, reason codes |
| `0015_purchase_returns`                           | RET 2 | Purchase returns + lines with FKs to suppliers, receipts, stock movements, reason codes |
| `0016_ret4_perf_indexes`                          | RET 4 | GIN trgm indexes on return/disposition numbers, PostgreSQL sequences for numbering |

---

## Audit

All entity changes (INSERT, UPDATE, DELETE) are automatically captured by `AuditInterceptor`:
- **Excluded entities**: `AuditLog`, `RefreshToken`
- **Redacted fields**: `PasswordHash`, `Token`, `TokenHash`, `ReplacedByToken`, `ReplacedByTokenHash`, `RefreshToken`, `Secret` — never serialised into OldValues/NewValues
- **Captured fields**: `UserId`, `Username`, `IpAddress`, `UserAgent`, `CorrelationId`, `Action`, `EntityName`, `PrimaryKey`, `OldValues` (JSON, max 4 KB), `NewValues` (JSON, max 4 KB)
- Audit logs are immutable — no UPDATE or DELETE on `audit_logs`

---

## Data Types

- All monetary amounts: `numeric(18,4)` (PostgreSQL) / `decimal` (C#)
- All timestamps: `timestamp with time zone` (UTC)
- All IDs: `uuid` (Guid)
- JSON columns: `text` (ConfigJson) or `jsonb` (AuditLog values)
- Enum-like columns stored as `varchar` with string conversion (e.g., `Status`, `PartyType`, `Method`, `EntryType`)
