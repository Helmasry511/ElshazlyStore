# ElshazlyStore — API Reference

> Auto-generated endpoint reference. All responses follow RFC 7807 ProblemDetails on error.
> Every response includes `X-Correlation-Id` header.

---

## Base URL

```
/api/v1
```

---

## Authentication

All protected endpoints require a `Bearer` token in the `Authorization` header.

```
Authorization: Bearer <access_token>
```

Access tokens are short-lived JWTs (default: 15 min). Use the refresh endpoint to obtain new ones.

---

## Endpoints

### Health

| Method | Path           | Auth | Description            |
|--------|----------------|------|------------------------|
| GET    | `/api/v1/health` | No | Returns system health   |

**GET /api/v1/health** — Response `200 OK`:
```json
{
  "status": "Healthy",
  "timestamp": "2025-01-01T00:00:00Z"
}
```

---

### Auth

| Method | Path           | Auth | Description                        |
|--------|----------------|------|------------------------------------|
| POST   | `/auth/login`  | No   | Login with username + password     |
| POST   | `/auth/refresh`| No   | Refresh access token               |
| POST   | `/auth/logout` | Yes  | Revoke a refresh token             |
| GET    | `/auth/me`     | Yes  | Get current user info              |

**POST /auth/login**

Request:
```json
{ "username": "admin", "password": "Admin@123!" }
```

Response `200 OK`:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiI...",
  "refreshToken": "base64-opaque-token",
  "expiresAtUtc": "2025-01-01T00:15:00Z"
}
```

Error `400` — empty username/password.
Error `401` — invalid credentials or inactive account.

---

**POST /auth/refresh**

Request:
```json
{ "refreshToken": "base64-opaque-token" }
```

Response `200 OK`: Same shape as login response (new access + refresh tokens).
The old refresh token is revoked and replaced.

Error `401` — invalid, expired, or already-revoked refresh token.

---

**POST /auth/logout** *(requires Bearer token)*

Request:
```json
{ "refreshToken": "base64-opaque-token" }
```

Response `200 OK`:
```json
{ "message": "Logged out." }
```

---

**GET /auth/me** *(requires Bearer token)*

Response `200 OK`:
```json
{
  "id": "guid",
  "username": "admin",
  "isActive": true,
  "roles": ["Admin"]
}
```

---

### Users *(requires `USERS_READ` or `USERS_WRITE` permission)*

| Method | Path           | Permission   | Description          |
|--------|----------------|--------------|----------------------|
| GET    | `/users`       | USERS_READ   | List all users       |
| GET    | `/users/{id}`  | USERS_READ   | Get user by ID       |
| POST   | `/users`       | USERS_WRITE  | Create a new user    |
| PUT    | `/users/{id}`  | USERS_WRITE  | Update a user        |
| DELETE | `/users/{id}`  | USERS_WRITE  | Deactivate a user    |

**POST /users**

Request:
```json
{
  "username": "newuser",
  "password": "Secure@123",
  "roleIds": ["guid-of-role"]
}
```

Response `201 Created`:
```json
{
  "id": "new-user-guid",
  "username": "newuser",
  "isActive": true,
  "createdAtUtc": "2025-01-01T00:00:00Z"
}
```

**PUT /users/{id}**

Request:
```json
{
  "username": "updatedname",
  "isActive": true,
  "roleIds": ["guid-of-role"]
}
```

**DELETE /users/{id}** — Soft-deletes (sets `IsActive = false`). Response `204 No Content`.

---

### Roles *(requires `ROLES_READ` or `ROLES_WRITE` permission)*

| Method | Path                          | Permission  | Description                    |
|--------|-------------------------------|-------------|--------------------------------|
| GET    | `/roles`                      | ROLES_READ  | List all roles                 |
| GET    | `/roles/{id}`                 | ROLES_READ  | Get role by ID                 |
| POST   | `/roles`                      | ROLES_WRITE | Create a new role              |
| PUT    | `/roles/{id}`                 | ROLES_WRITE | Update role name/description   |
| DELETE | `/roles/{id}`                 | ROLES_WRITE | Delete a role                  |
| GET    | `/roles/{id}/permissions`     | ROLES_READ  | Get permissions for a role     |
| PUT    | `/roles/{id}/permissions`     | ROLES_WRITE | Set permissions for a role     |
| GET    | `/roles/permissions/all`      | ROLES_READ  | List all available permissions |

**POST /roles**

Request:
```json
{ "name": "Cashier", "description": "POS access only" }
```

**PUT /roles/{id}/permissions**

Request:
```json
{ "permissionIds": ["guid1", "guid2"] }
```

**GET /roles/permissions/all** — Response `200 OK`:
```json
[
  { "id": "guid", "code": "USERS_READ", "description": "Read user data" },
  { "id": "guid", "code": "USERS_WRITE", "description": "Create/update/delete users" }
]
```

---

## Permission Codes

| Code                | Description                              |
|---------------------|------------------------------------------|
| `USERS_READ`        | Read user data                           |
| `USERS_WRITE`       | Create, update, deactivate users         |
| `ROLES_READ`        | Read roles and permissions               |
| `ROLES_WRITE`       | Create, update, delete roles             |
| `AUDIT_READ`        | Read audit logs                          |
| `PRODUCTS_READ`     | View products and variants               |
| `PRODUCTS_WRITE`    | Create, update, delete products/variants |
| `CUSTOMERS_READ`    | View customers                           |
| `CUSTOMERS_WRITE`   | Create, update, delete customers         |
| `SUPPLIERS_READ`    | View suppliers                           |
| `SUPPLIERS_WRITE`   | Create, update, delete suppliers         |
| `IMPORT_MASTER_DATA`| Import master data from Excel/CSV        |
| `STOCK_READ`        | View stock balances and ledger            |
| `STOCK_POST`        | Post stock movements                     |
| `WAREHOUSES_READ`   | View warehouses                          |
| `WAREHOUSES_WRITE`  | Create, update, delete warehouses        |

---

## Products *(requires `PRODUCTS_READ` or `PRODUCTS_WRITE` permission)*

| Method | Path              | Permission      | Description              |
|--------|-------------------|-----------------|--------------------------|
| GET    | `/products`       | PRODUCTS_READ   | List products (paged)    |
| GET    | `/products/{id}`  | PRODUCTS_READ   | Get product with variants|
| POST   | `/products`       | PRODUCTS_WRITE  | Create a product         |
| PUT    | `/products/{id}`  | PRODUCTS_WRITE  | Update a product         |
| DELETE | `/products/{id}`  | PRODUCTS_WRITE  | Delete product + retire barcodes |

**GET /products** — Query parameters:

| Param    | Type   | Default | Description              |
|----------|--------|---------|--------------------------|
| `q`      | string |         | Search name/category/desc|
| `page`   | int    | 1       | Page number              |
| `pageSize`| int   | 25      | Page size (max 100)      |
| `sort`   | string | name    | name, name_desc, category, created, created_desc |

Response `200 OK`:
```json
{
  "items": [
    { "id": "guid", "name": "T-Shirt", "description": "...", "category": "Clothing", "isActive": true, "createdAtUtc": "...", "variantCount": 3 }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 25
}
```

**POST /products**
```json
{ "name": "T-Shirt Basic", "description": "Cotton t-shirt", "category": "Clothing" }
```

---

## Variants *(requires `PRODUCTS_READ` or `PRODUCTS_WRITE` permission)*

| Method | Path                      | Permission      | Description               |
|--------|---------------------------|-----------------|---------------------------|
| GET    | `/variants`               | PRODUCTS_READ   | List variants (paged, searchable by SKU/barcode/name) |
| GET    | `/variants/by-sku/{sku}`  | PRODUCTS_READ   | Lookup variant by SKU (exact match) |
| GET    | `/variants/{id}`          | PRODUCTS_READ   | Get variant by ID         |
| POST   | `/variants`               | PRODUCTS_WRITE  | Create variant + assign barcode |
| PUT    | `/variants/{id}`          | PRODUCTS_WRITE  | Update variant            |
| DELETE | `/variants/{id}`          | PRODUCTS_WRITE  | Delete variant (retires barcode) |

**POST /variants**

> **SKU** and **Barcode** are optional. If omitted (null or empty), the server auto-generates them:
> - **SKU**: 10-digit numeric, counter-based (e.g., `"0000000001"`)
> - **Barcode**: 13-digit numeric, random EAN-like (e.g., `"4829103756284"`)
>
> If provided, the values are preserved as-is.

Request (all fields supplied):
```json
{
  "productId": "guid",
  "sku": "TSH-BLK-M",
  "barcode": "8901234567890",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
```

Request (server generates SKU + Barcode):
```json
{
  "productId": "guid",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
```

Response `201 Created`:
```json
{
  "id": "guid",
  "productId": "guid",
  "productName": "T-Shirt Basic",
  "sku": "0000000001",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00,
  "isActive": true,
  "barcode": "4829103756284"
}
```

Error `409` — SKU or Barcode already exists (when explicitly provided).

**GET /variants/by-sku/{sku}**

Lookup a variant by its exact SKU. Works for both server-generated and manually-assigned SKUs.

Response `200 OK`:
```json
{
  "id": "guid",
  "productId": "guid",
  "productName": "T-Shirt Basic",
  "sku": "CUSTOM-SKU-001",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00,
  "isActive": true,
  "barcode": "4829103756284"
}
```

Error `404` — variant with that SKU not found.

**GET /variants?q={searchTerm}**

The list endpoint searches across: SKU, product name, color, size, and barcode.
Both generated and manual identifiers are searchable.

---

## Barcodes *(requires `PRODUCTS_READ` permission)*

| Method | Path                  | Permission    | Description                  |
|--------|-----------------------|---------------|------------------------------|
| GET    | `/barcodes/{barcode}` | PRODUCTS_READ | Lookup product+variant by barcode (works for both generated and manual barcodes) |

Response `200 OK`:
```json
{
  "barcode": "8901234567890",
  "status": "Assigned",
  "variantId": "guid",
  "sku": "TSH-BLK-M",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00,
  "isActive": true,
  "productId": "guid",
  "productName": "T-Shirt Basic",
  "productCategory": "Clothing"
}
```

Error `404` — barcode not found. Error `410` — barcode retired.

---

## Customers *(requires `CUSTOMERS_READ` or `CUSTOMERS_WRITE` permission)*

| Method | Path               | Permission       | Description            |
|--------|--------------------|------------------|------------------------|
| GET    | `/customers`       | CUSTOMERS_READ   | List customers (paged) |
| GET    | `/customers/{id}`  | CUSTOMERS_READ   | Get customer by ID     |
| POST   | `/customers`       | CUSTOMERS_WRITE  | Create a customer      |
| PUT    | `/customers/{id}`  | CUSTOMERS_WRITE  | Update a customer      |
| DELETE | `/customers/{id}`  | CUSTOMERS_WRITE  | Deactivate a customer  |

**GET /customers** — `q` searches across code, name, phone. Same paging as products.

**POST /customers**
```json
{ "name": "Ahmed Mohamed", "code": "CUST-000001", "phone": "01012345678", "phone2": "01112345678", "notes": "VIP" }
```
If `code` is omitted, server generates `CUST-NNNNNN`.

---

## Suppliers *(requires `SUPPLIERS_READ` or `SUPPLIERS_WRITE` permission)*

| Method | Path               | Permission       | Description            |
|--------|--------------------|------------------|------------------------|
| GET    | `/suppliers`       | SUPPLIERS_READ   | List suppliers (paged) |
| GET    | `/suppliers/{id}`  | SUPPLIERS_READ   | Get supplier by ID     |
| POST   | `/suppliers`       | SUPPLIERS_WRITE  | Create a supplier      |
| PUT    | `/suppliers/{id}`  | SUPPLIERS_WRITE  | Update a supplier      |
| DELETE | `/suppliers/{id}`  | SUPPLIERS_WRITE  | Deactivate a supplier  |

Same structure as customers. Code auto-generated as `SUP-NNNNNN` if omitted.

---

## Import *(requires `IMPORT_MASTER_DATA` permission)*

| Method | Path                            | Permission         | Description           |
|--------|---------------------------------|--------------------|-----------------------|
| POST   | `/imports/masterdata/preview`   | IMPORT_MASTER_DATA | Preview/validate file |
| POST   | `/imports/masterdata/commit`    | IMPORT_MASTER_DATA | Commit import job     |

**POST /imports/masterdata/preview** — `multipart/form-data` with file + `?type=Products|Customers|Suppliers`

Response `200 OK`:
```json
{
  "jobId": "guid",
  "totalRows": 10,
  "validRows": 8,
  "rowErrors": [
    [],
    [{ "column": "Barcode", "message": "Duplicate barcode 'XYZ'." }]
  ]
}
```

**POST /imports/masterdata/commit**
```json
{ "jobId": "guid" }
```
Response `200 OK`:
```json
{ "success": true, "importedCount": 10 }
```

---

## Warehouses *(requires `WAREHOUSES_READ` or `WAREHOUSES_WRITE` permission)*

| Method | Path                | Permission       | Description            |
|--------|---------------------|------------------|------------------------|
| GET    | `/warehouses`       | WAREHOUSES_READ  | List warehouses (paged)|
| GET    | `/warehouses/{id}`  | WAREHOUSES_READ  | Get warehouse by ID    |
| POST   | `/warehouses`       | WAREHOUSES_WRITE | Create a warehouse     |
| PUT    | `/warehouses/{id}`  | WAREHOUSES_WRITE | Update a warehouse     |
| DELETE | `/warehouses/{id}`  | WAREHOUSES_WRITE | Deactivate a warehouse |

**POST /warehouses**
```json
{ "code": "WH-02", "name": "Secondary Warehouse", "address": "123 Street", "isDefault": false }
```

A default warehouse (`MAIN`) is seeded automatically on first run.

---

## Stock Movements *(requires `STOCK_POST` permission)*

| Method | Path                       | Permission | Description         |
|--------|----------------------------|------------|---------------------|
| POST   | `/stock-movements/post`    | STOCK_POST | Post a movement     |

**Single endpoint for all stock mutations.** No direct balance manipulation.

**POST /stock-movements/post**
```json
{
  "type": 1,
  "reference": "PO-001",
  "notes": "Purchase from supplier X",
  "lines": [
    {
      "variantId": "guid",
      "warehouseId": "guid",
      "quantityDelta": 100,
      "unitCost": 10.50,
      "reason": null
    }
  ]
}
```

Movement type values:

| Value | Type               | QuantityDelta rule     |
|-------|--------------------|------------------------|
| 0     | OpeningBalance     | Must be positive       |
| 1     | PurchaseReceipt    | Must be positive       |
| 2     | SaleIssue          | Must be negative       |
| 3     | Transfer           | Mixed (neg + pos pair) |
| 4     | Adjustment         | Any sign               |
| 5     | ProductionConsume  | Must be negative       |
| 6     | ProductionProduce  | Must be positive       |

For **Transfer**, include two lines: one negative (source warehouse) and one positive (destination warehouse).

Response `200 OK`:
```json
{ "movementId": "guid" }
```

Errors: `422` if negative stock would result. `404` if variant or warehouse not found. `400` if lines empty.

---

## Stock Balances & Ledger *(requires `STOCK_READ` permission)*

| Method | Path              | Permission | Description                      |
|--------|-------------------|------------|----------------------------------|
| GET    | `/stock/balances` | STOCK_READ | Current stock levels (paged)     |
| GET    | `/stock/ledger`   | STOCK_READ | Movement history/ledger (paged)  |

**GET /stock/balances** — Query parameters:

| Param        | Type   | Default | Description                          |
|--------------|--------|---------|--------------------------------------|
| `warehouseId`| guid   |         | Filter by warehouse                  |
| `q`          | string |         | Search SKU/product name/barcode/color|
| `page`       | int    | 1       | Page number                          |
| `pageSize`   | int    | 25      | Page size (max 100)                  |
| `sort`       | string | product | sku, sku_desc, quantity, quantity_desc, product, updated, updated_desc |

Response `200 OK`:
```json
{
  "items": [
    {
      "variantId": "guid", "sku": "TSH-BLK-M", "color": "Black", "size": "M",
      "productName": "T-Shirt", "barcode": "8901234567890",
      "warehouseId": "guid", "warehouseCode": "MAIN", "warehouseName": "Main Warehouse",
      "quantity": 125, "lastUpdatedUtc": "..."
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 25
}
```

**GET /stock/ledger** — Query parameters:

| Param        | Type     | Default | Description              |
|--------------|----------|---------|--------------------------|
| `variantId`  | guid     |         | Filter by variant        |
| `warehouseId`| guid     |         | Filter by warehouse      |
| `from`       | datetime |         | Start date (UTC)         |
| `to`         | datetime |         | End date (UTC)           |
| `page`       | int      | 1       | Page number              |
| `pageSize`   | int      | 25      | Page size (max 100)      |

Response `200 OK`:
```json
{
  "items": [
    {
      "movementId": "guid", "type": "PurchaseReceipt", "reference": "PO-001",
      "postedAtUtc": "...", "postedByUsername": "admin",
      "variantId": "guid", "sku": "TSH-BLK-M",
      "warehouseId": "guid", "warehouseCode": "MAIN",
      "quantityDelta": 100, "unitCost": 10.50, "reason": null
    }
  ],
  "totalCount": 10,
  "page": 1,
  "pageSize": 25
}
```

---

### Purchases *(Phase 4 — Procurement)*

| Method | Path                       | Permission      | Description                     |
|--------|----------------------------|------------------|---------------------------------|
| GET    | `/purchases`               | PURCHASES_READ   | List purchase receipts (paged)  |
| GET    | `/purchases/{id}`          | PURCHASES_READ   | Get receipt with lines          |
| POST   | `/purchases`               | PURCHASES_WRITE  | Create draft purchase receipt   |
| PUT    | `/purchases/{id}`          | PURCHASES_WRITE  | Update draft receipt            |
| DELETE | `/purchases/{id}`          | PURCHASES_WRITE  | Delete draft receipt            |
| POST   | `/purchases/{id}/post`     | PURCHASES_POST   | Post receipt → stock movement   |

**POST /purchases** — Request:
```json
{
  "supplierId": "guid",
  "warehouseId": "guid",
  "documentNumber": "PO-001",
  "notes": "Optional notes",
  "lines": [
    { "variantId": "guid", "quantity": 50, "unitCost": 10.00 }
  ]
}
```

**POST /purchases/{id}/post** — Response `200 OK`:
```json
{ "stockMovementId": "guid" }
```

**Posting semantics:**
- **Idempotent:** If the receipt is already posted, returns `200` with the existing `stockMovementId`.
- **Concurrency-safe:** An atomic `UPDATE … WHERE Status = Draft` gate prevents double-posting under concurrent requests. Only one caller creates a stock movement.
- **Conflict:** If another request is mid-posting, returns `409` with error code `POST_CONCURRENCY_CONFLICT`.

---

### Production *(Phase 5)*

| Method | Path                       | Permission        | Description                     |
|--------|----------------------------|--------------------|---------------------------------|
| GET    | `/production`              | PRODUCTION_READ    | List production batches (paged) |
| GET    | `/production/{id}`         | PRODUCTION_READ    | Get batch with lines            |
| POST   | `/production`              | PRODUCTION_WRITE   | Create draft production batch   |
| PUT    | `/production/{id}`         | PRODUCTION_WRITE   | Update draft batch              |
| DELETE | `/production/{id}`         | PRODUCTION_WRITE   | Delete draft batch              |
| POST   | `/production/{id}/post`    | PRODUCTION_POST    | Post batch → stock movement     |

**POST /production** — Request:
```json
{
  "warehouseId": "guid",
  "batchNumber": "BATCH-001",
  "notes": "Optional",
  "inputs": [
    { "variantId": "guid", "quantity": 100 }
  ],
  "outputs": [
    { "variantId": "guid", "quantity": 50, "unitCost": 25.00 }
  ]
}
```

**POST /production/{id}/post** — Response `200 OK`:
```json
{ "consumeMovementId": "guid", "produceMovementId": "guid" }
```

**Posting semantics (same as Purchases/Sales):**
- **Idempotent:** Already-posted batch returns `200` with existing movement IDs.
- **Concurrency-safe:** Atomic `UPDATE … WHERE Status = Draft` prevents double-posting.
- **Conflict:** Mid-posting returns `409` / `POST_CONCURRENCY_CONFLICT`.

---

### Sales *(Phase 6 — POS)*

| Method | Path                   | Permission   | Description                    |
|--------|------------------------|--------------|--------------------------------|
| GET    | `/sales`               | SALES_READ   | List sales invoices (paged)    |
| GET    | `/sales/{id}`          | SALES_READ   | Get invoice with lines         |
| POST   | `/sales`               | SALES_WRITE  | Create draft sales invoice     |
| PUT    | `/sales/{id}`          | SALES_WRITE  | Update draft invoice           |
| DELETE | `/sales/{id}`          | SALES_WRITE  | Delete draft invoice           |
| POST   | `/sales/{id}/post`     | SALES_POST   | Post invoice → stock movement  |

**POST /sales** — Request:
```json
{
  "warehouseId": "guid",
  "customerId": "guid (optional)",
  "invoiceNumber": "INV-001 (optional, auto-generated)",
  "notes": "Optional",
  "lines": [
    { "variantId": "guid", "quantity": 2, "unitPrice": 100.00, "discountAmount": 5.00 }
  ]
}
```

**POST /sales/{id}/post** — Response `200 OK`:
```json
{ "stockMovementId": "guid" }
```

**Posting semantics (same as Purchases/Production):**
- **Idempotent:** Already-posted invoice returns `200` with existing `stockMovementId`.
- **Concurrency-safe:** Atomic `UPDATE … WHERE Status = Draft` prevents double-posting. Only one stock movement is ever created per entity.
- **Conflict:** Mid-posting returns `409` / `POST_CONCURRENCY_CONFLICT`.

---

### Accounting *(Phase 7 — AR/AP)*

| Method | Path                                     | Permission      | Description                    |
|--------|------------------------------------------|-----------------|--------------------------------|
| GET    | `/accounting/balances/customers`         | ACCOUNTING_READ | Paged customer AR balances     |
| GET    | `/accounting/balances/suppliers`         | ACCOUNTING_READ | Paged supplier AP balances     |
| GET    | `/accounting/balances/{partyType}/{id}`  | ACCOUNTING_READ | Single party balance           |
| GET    | `/accounting/ledger/{partyType}/{id}`    | ACCOUNTING_READ | Ledger entries for a party     |

`partyType` is `Customer` or `Supplier`.

### Payments *(Phase 7)*

| Method | Path              | Permission     | Description           |
|--------|-------------------|----------------|-----------------------|
| GET    | `/payments`       | PAYMENTS_READ  | List payments (paged) |
| GET    | `/payments/{id}`  | PAYMENTS_READ  | Get payment detail    |
| POST   | `/payments`       | PAYMENTS_WRITE | Create a payment      |

**POST /payments** — Request:
```json
{
  "partyType": "Customer",
  "partyId": "guid",
  "amount": 500.00,
  "method": "Cash",
  "walletName": null,
  "reference": "REC-001",
  "paymentDateUtc": "2026-03-01T00:00:00Z"
}
```
`method` options: `Cash`, `Card`, `BankTransfer`, `EWallet`, `Cheque`.

### Import — Opening Balances & Payments *(Phase 7)*

| Method | Path                               | Permission              | Description                    |
|--------|-------------------------------------|-------------------------|--------------------------------|
| POST   | `/imports/opening-balances/preview` | IMPORT_OPENING_BALANCES | Preview opening balance CSV    |
| POST   | `/imports/opening-balances/commit`  | IMPORT_OPENING_BALANCES | Commit opening balances        |
| POST   | `/imports/payments/preview`         | IMPORT_PAYMENTS         | Preview payments CSV           |
| POST   | `/imports/payments/commit`          | IMPORT_PAYMENTS         | Commit payments                |

---

### Dashboard *(Phase 8 — KPIs)*

| Method | Path                           | Permission      | Description                   |
|--------|--------------------------------|-----------------|-------------------------------|
| GET    | `/dashboard/summary`           | DASHBOARD_READ  | Full KPI summary              |
| GET    | `/dashboard/sales`             | DASHBOARD_READ  | Sales metrics only            |
| GET    | `/dashboard/top-products`      | DASHBOARD_READ  | Top products by qty/revenue   |
| GET    | `/dashboard/low-stock`         | DASHBOARD_READ  | Low stock alerts              |
| GET    | `/dashboard/cashier-performance`| DASHBOARD_READ | Cashier KPIs                  |

**GET /dashboard/summary** — Query parameters:

| Param              | Type     | Default          | Description                    |
|--------------------|----------|------------------|--------------------------------|
| `from`             | datetime | Start of month   | Start date (UTC)               |
| `to`               | datetime | Now              | End date (UTC)                 |
| `topN`             | int      | 10               | Top N products                 |
| `lowStockThreshold`| decimal  | 5                | Default low stock threshold    |

Response `200 OK`:
```json
{
  "sales": { "totalSales": 15000, "invoiceCount": 42, "averageTicket": 357.14 },
  "topProducts": [ { "productName": "...", "sku": "...", "totalQuantity": 100, "totalRevenue": 5000 } ],
  "lowStockAlerts": [ { "sku": "...", "productName": "...", "warehouseName": "MAIN", "currentStock": 3, "threshold": 5 } ],
  "cashierPerformance": [ { "cashierUsername": "admin", "invoiceCount": 42, "totalSales": 15000, "averageTicket": 357.14 } ]
}
```

---

### Printing Policy *(Phase 9)*

#### Print Profiles

| Method | Path                                        | Permission             | Description                |
|--------|---------------------------------------------|------------------------|----------------------------|
| GET    | `/print-profiles`                           | MANAGE_PRINTING_POLICY | List profiles (paged)      |
| GET    | `/print-profiles/{id}`                      | MANAGE_PRINTING_POLICY | Get profile with rules     |
| POST   | `/print-profiles`                           | MANAGE_PRINTING_POLICY | Create profile             |
| PUT    | `/print-profiles/{id}`                      | MANAGE_PRINTING_POLICY | Update profile             |
| DELETE | `/print-profiles/{id}`                      | MANAGE_PRINTING_POLICY | Delete profile + rules     |

**POST /print-profiles** — Request:
```json
{ "name": "Default POS", "isDefault": true }
```

#### Print Rules (nested under profile)

| Method | Path                                                 | Permission             | Description          |
|--------|------------------------------------------------------|------------------------|----------------------|
| GET    | `/print-profiles/{id}/rules`                         | MANAGE_PRINTING_POLICY | List rules           |
| GET    | `/print-profiles/{id}/rules/{ruleId}`                | MANAGE_PRINTING_POLICY | Get rule             |
| POST   | `/print-profiles/{id}/rules`                         | MANAGE_PRINTING_POLICY | Create rule          |
| PUT    | `/print-profiles/{id}/rules/{ruleId}`                | MANAGE_PRINTING_POLICY | Update rule          |
| DELETE | `/print-profiles/{id}/rules/{ruleId}`                | MANAGE_PRINTING_POLICY | Delete rule          |

**POST /print-profiles/{id}/rules** — Request:
```json
{ "screenCode": "POS_RECEIPT", "configJson": "{\"paperWidth\":80,\"showLogo\":true}", "enabled": true }
```

#### Policy Lookup

| Method | Path                         | Permission             | Description                           |
|--------|------------------------------|------------------------|---------------------------------------|
| GET    | `/print-policy/{screenCode}` | MANAGE_PRINTING_POLICY | Get active rule for screen (default profile or `?profileId=`) |

---

### Reason Codes *(Phase RET 0)*

Admin-managed catalog of reasons used for sales returns, purchase returns, and pre-sale dispositions.
Reasons are **never hard-deleted** — they can only be disabled to preserve historical references.

#### Endpoints

| Method | Path                          | Permission           | Description                      |
|--------|-------------------------------|----------------------|----------------------------------|
| GET    | `/reasons`                    | VIEW_REASON_CODES    | List reasons (paged, filterable) |
| GET    | `/reasons/{id}`               | VIEW_REASON_CODES    | Get reason by ID                 |
| POST   | `/reasons`                    | MANAGE_REASON_CODES  | Create reason (admin)            |
| PUT    | `/reasons/{id}`               | MANAGE_REASON_CODES  | Update reason (admin)            |
| POST   | `/reasons/{id}/disable`       | MANAGE_REASON_CODES  | Soft-disable reason (admin)      |

**GET /reasons** — Query parameters:
- `category` — Filter by category: `General`, `SalesReturn`, `PurchaseReturn`, `Disposition`
- `isActive` — Filter by active/inactive status (`true`/`false`)
- `q` — Free-text search on Code and NameAr
- `page`, `pageSize`, `includeTotal` — Pagination

**POST /reasons** — Request:
```json
{
  "code": "DAMAGED",
  "nameAr": "تالف",
  "description": "Item is physically damaged",
  "category": "Disposition",
  "requiresManagerApproval": false
}
```

**PUT /reasons/{id}** — Request (all fields optional):
```json
{
  "nameAr": "تالف - محدث",
  "description": "Updated description",
  "category": "Disposition",
  "requiresManagerApproval": true
}
```

**POST /reasons/{id}/disable** — No body required. Sets `isActive = false`.

#### Reason Categories

| Value           | Usage                                              |
|-----------------|----------------------------------------------------|
| General         | Applies to any context                             |
| SalesReturn     | Customer-initiated sales return                    |
| PurchaseReturn  | Supplier-directed purchase return                  |
| Disposition     | Pre-sale: damage, theft, expiry, manufacturing defect |

#### Disposition Types (Enum)

Used when recording stock dispositions alongside a reason code:

| Value           | Arabic                       | Description                        |
|-----------------|------------------------------|------------------------------------|
| Scrap           | هالك/تالف نهائي              | Irreparable, to be destroyed       |
| Rework          | قابل لإعادة التشغيل          | Can be reworked/repaired           |
| ReturnToVendor  | مرتجع للمورد                 | Return to supplier/vendor          |
| ReturnToStock   | يرجع مخزن صالح للبيع         | Return to sellable stock           |
| Quarantine      | حجز/عزل للفحص                | Quarantine for inspection          |
| WriteOff        | تسوية/خصم كمي (سرقة/فقد)     | Write-off for theft/loss           |

#### Seeded Default Reasons

| Code                   | NameAr                        | Category        |
|------------------------|-------------------------------|-----------------|
| DAMAGED                | تالف                          | Disposition     |
| THEFT                  | سرقة                          | Disposition     |
| EXPIRED                | منتهي الصلاحية                | Disposition     |
| NOT_MATCHING_SPEC      | غير مطابق للمواصفات           | Disposition     |
| MANUFACTURING_DEFECT   | عيب تصنيع                     | Disposition     |
| CUSTOMER_CHANGED_MIND  | العميل غيّر رأيه              | SalesReturn     |
| WRONG_ITEM             | صنف خاطئ                      | SalesReturn     |
| DEFECTIVE_PRODUCT      | منتج معيب                     | SalesReturn     |
| SUPPLIER_QUALITY       | جودة غير مقبولة من المورد     | PurchaseReturn  |
| WRONG_DELIVERY         | توريد خاطئ                    | PurchaseReturn  |
| OVER_DELIVERY          | زيادة في التوريد              | PurchaseReturn  |

#### Usage Policy
- Reason codes are **referenced by Id** in all movements, returns, and ledger entries.
- Once a reason has been used in any transaction, it **cannot be deleted** — only disabled.
- Disabled reasons remain visible in historical records but are excluded from new-transaction dropdowns.
- Reasons with `requiresManagerApproval = true` require elevated approval workflow.

---

### Sales Returns *(Phase RET 1)*

Customer sales returns with strict inventory + accounting impact.

#### Endpoints

| Method | Path                                 | Permission           | Description                        |
|--------|--------------------------------------|----------------------|------------------------------------|
| GET    | `/sales-returns`                     | VIEW_SALES_RETURNS   | List returns (paged)               |
| GET    | `/sales-returns/{id}`                | VIEW_SALES_RETURNS   | Get return with lines              |
| POST   | `/sales-returns`                     | SALES_RETURN_CREATE  | Create draft return                |
| PUT    | `/sales-returns/{id}`                | SALES_RETURN_CREATE  | Update draft return                |
| DELETE | `/sales-returns/{id}`                | SALES_RETURN_CREATE  | Delete draft return                |
| POST   | `/sales-returns/{id}/post`           | SALES_RETURN_POST    | Post return to inventory           |
| POST   | `/sales-returns/{id}/void`           | SALES_RETURN_VOID    | Void **draft** return (cancel)     |

#### Status Flow

```
Draft → Posted   (permanent, no undo in RET 1)
Draft → Voided   (soft-cancel, preserves record)
```

- **Draft**: Editable. Lines can be added/removed/updated. Can be posted, voided, or deleted.
- **Posted**: Immutable and permanent. Stock movement created, ledger entry written. Cannot be voided or reversed.
- **Voided**: Terminal state. Soft-cancellation of a draft return. No inventory/accounting impact.

> **RET 1 Void Policy (A1)**: Void is only allowed on Draft returns. Attempting to void a Posted return returns `409 SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST`. Inventory/ledger reversal deferred to RET 2.

#### Allowed Dispositions (RET 1)

RET 1 only allows dispositions with fully defined inventory effects:

| Value | Name           | Inventory Effect                              | RET 1 Status |
|-------|----------------|-----------------------------------------------|--------------|
| 3     | ReturnToStock  | Stock added to sellable warehouse (+qty)      | **Allowed**  |
| 4     | Quarantine     | Stock added to warehouse for inspection (+qty)| **Allowed**  |
| 0     | Scrap          | —                                             | Blocked      |
| 1     | Rework         | —                                             | Blocked      |
| 2     | ReturnToVendor | —                                             | Blocked      |
| 5     | WriteOff       | —                                             | Blocked      |

> Scrap, WriteOff, Rework, and ReturnToVendor return `400 SALES_RETURN_DISPOSITION_NOT_ALLOWED`. Use **RET 3 Dispositions** for these flows.

#### Create Draft Request

```json
POST /api/v1/sales-returns
{
  "warehouseId": "uuid",
  "customerId": "uuid | null",
  "originalSalesInvoiceId": "uuid | null",
  "returnDateUtc": "2026-03-01T12:00:00Z",
  "notes": "string | null",
  "lines": [
    {
      "variantId": "uuid",
      "quantity": 2,
      "unitPrice": 50.00,
      "reasonCodeId": "uuid",
      "dispositionType": 3,
      "notes": "string | null"
    }
  ]
}
```

#### Posting Effects (Atomic)

1. **Inventory**: Creates a `SaleReturnReceipt` stock movement with positive delta for all lines (only ReturnToStock/Quarantine allowed).

2. **Accounting (Credit Note)**: If customer exists, creates a `CreditNote` ledger entry (negative amount = reduces outstanding receivable). The credit note automatically reduces the customer's outstanding balance via `ComputeOutstandingAsync`, which sums all ledger entries (Invoice + Payment + OpeningBalance + CreditNote). No separate "apply credit" step is needed — the financial loop is closed by the ledger model.

3. **Concurrency**: Uses atomic `UPDATE … WHERE Status = Draft` gate. Double-post returns idempotent 200 OK with existing movement ID.

#### Credit Note Financial Model

- Credit notes are recorded as `LedgerEntryType.CreditNote` with negative `Amount`.
- Outstanding balance = SUM(all ledger entries for party). CreditNote entries reduce this sum.
- Overpayment validation in `CreatePaymentAsync` uses the credit-note-adjusted outstanding.
- Example: Invoice +500, CreditNote −150 → Outstanding = 350. Max payment = 350.
- Walk-in returns (no customer) do not create credit notes.

#### Return Qty Validation (when `originalSalesInvoiceId` provided)

- Per variant: `requested return qty ≤ (sold qty − already returned qty)`.
- Validated at both **creation** and **posting** time.
- Already-returned qty counts only from other **posted** returns linked to the same invoice.

#### Audit

- Every post is captured by the audit interceptor with correlationId, user, and before/after state.

---

### Purchase Returns *(Phase RET 2)*

Supplier purchase returns with strict inventory + AP impact.

#### Endpoints

| Method | Path                                    | Permission             | Description                        |
|--------|-----------------------------------------|------------------------|------------------------------------|
| GET    | `/purchase-returns`                     | VIEW_PURCHASE_RETURNS  | List returns (paged)               |
| GET    | `/purchase-returns/{id}`                | VIEW_PURCHASE_RETURNS  | Get return with lines              |
| POST   | `/purchase-returns`                     | PURCHASE_RETURN_CREATE | Create draft return                |
| PUT    | `/purchase-returns/{id}`                | PURCHASE_RETURN_CREATE | Update draft return                |
| DELETE | `/purchase-returns/{id}`                | PURCHASE_RETURN_CREATE | Delete draft return                |
| POST   | `/purchase-returns/{id}/post`           | PURCHASE_RETURN_POST   | Post return to inventory           |
| POST   | `/purchase-returns/{id}/void`           | PURCHASE_RETURN_VOID   | Void **draft** return (cancel)     |

#### Status Flow

```
Draft → Posted   (permanent, no undo in RET 2)
Draft → Voided   (soft-cancel, preserves record)
```

- **Draft**: Editable. Lines can be added/removed/updated. Can be posted, voided, or deleted.
- **Posted**: Immutable and permanent. Stock movement created, debit note written. Cannot be voided or reversed.
- **Voided**: Terminal state. Soft-cancellation of a draft return. No inventory/accounting impact.

> **RET 2 Void Policy**: Void is only allowed on Draft returns. Attempting to void a Posted return returns `409 PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST`.

#### Default Disposition

All purchase return lines use `ReturnToVendor` disposition. Stock is issued out of the specified warehouse (negative delta).

#### Create Draft Request

```json
POST /api/v1/purchase-returns
{
  "warehouseId": "uuid",
  "supplierId": "uuid",
  "originalPurchaseReceiptId": "uuid | null",
  "returnDateUtc": "2026-03-01T12:00:00Z",
  "notes": "string | null",
  "lines": [
    {
      "variantId": "uuid",
      "quantity": 2,
      "unitCost": 50.00,
      "reasonCodeId": "uuid",
      "notes": "string | null"
    }
  ]
}
```

#### Posting Effects (Atomic)

1. **Inventory**: Creates a `PurchaseReturnIssue` stock movement with **negative** delta for all lines (stock removed from warehouse).

2. **Accounting (Debit Note)**: Creates a `DebitNote` ledger entry (negative amount = reduces supplier payable / outstanding balance). The debit note automatically reduces the supplier's outstanding balance via `ComputeOutstandingAsync`.

3. **Concurrency**: Uses atomic `UPDATE … WHERE Status = Draft` gate. Double-post returns idempotent 200 OK with existing movement ID.

#### Return Qty Validation (when `originalPurchaseReceiptId` provided)

- Per variant: `requested return qty ≤ (received qty − already returned qty)`.
- Validated at both **creation** and **posting** time.
- Already-returned qty counts only from other **posted** returns linked to the same receipt.

#### Audit

- Every post is captured by the audit interceptor with correlationId, user, and before/after state.

---

### Inventory Dispositions *(Phase RET 3)*

Pre-sale disposition workflow for handling inventory issues discovered **before** sale: damage, theft, expiry, spec mismatch, manufacturing defects.

#### Endpoints

| Method | Path                                   | Permission          | Description                       |
|--------|----------------------------------------|---------------------|-----------------------------------|
| GET    | `/dispositions`                        | VIEW_DISPOSITIONS   | List dispositions (paged)         |
| GET    | `/dispositions/{id}`                   | VIEW_DISPOSITIONS   | Get disposition with lines        |
| POST   | `/dispositions`                        | DISPOSITION_CREATE  | Create draft disposition          |
| PUT    | `/dispositions/{id}`                   | DISPOSITION_CREATE  | Update draft disposition          |
| DELETE | `/dispositions/{id}`                   | DISPOSITION_CREATE  | Delete draft disposition          |
| POST   | `/dispositions/{id}/approve`           | DISPOSITION_APPROVE | Manager-approve disposition       |
| POST   | `/dispositions/{id}/post`              | DISPOSITION_POST    | Post disposition to inventory     |
| POST   | `/dispositions/{id}/void`              | DISPOSITION_VOID    | Void **draft** disposition        |

#### Status Flow

```
Draft → Posted   (permanent, no undo)
Draft → Voided   (soft-cancel, preserves record)
```

- **Draft**: Editable. Lines can be added/removed/updated. Can be approved, posted, voided, or deleted.
- **Posted**: Immutable and permanent. Stock movement created. Cannot be voided or reversed.
- **Voided**: Terminal state. Soft-cancellation of a draft disposition. No inventory impact.

> **Void Policy**: Void is only allowed on Draft dispositions. Attempting to void a Posted disposition returns `409 DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST`.

#### Disposition Types

| Type         | Inventory Effect                                                 |
|--------------|------------------------------------------------------------------|
| `Scrap`      | Remove from source warehouse → add to **SCRAP** warehouse       |
| `Quarantine` | Remove from source warehouse → add to **QUARANTINE** warehouse  |
| `Rework`     | Remove from source warehouse → add to **REWORK** warehouse      |
| `WriteOff`   | Remove from source warehouse only (total loss)                   |

> `ReturnToVendor` and `ReturnToStock` are **not allowed** for pre-sale dispositions (use RET 2 / RET 1 instead). Attempting to use them returns `400 DISPOSITION_INVALID_TYPE`.

#### Manager Approval

Dispositions whose reason codes have `requiresManagerApproval = true` **must** be approved before posting:

1. Create disposition with lines referencing reason codes.
2. If **any** line's reason code requires manager approval, call `POST /dispositions/{id}/approve`.
3. Approval records `approvedByUserId` and `approvedAtUtc`.
4. Updating lines on a previously-approved disposition **clears** the approval, requiring re-approval.
5. Posting without required approval returns `409 DISPOSITION_REQUIRES_APPROVAL`.

#### Create Draft Request

```json
POST /api/v1/dispositions
{
  "warehouseId": "uuid",
  "dispositionDateUtc": "2026-03-01T12:00:00Z",
  "notes": "string | null",
  "lines": [
    {
      "variantId": "uuid",
      "quantity": 5,
      "reasonCodeId": "uuid",
      "dispositionType": "Scrap | Quarantine | WriteOff | Rework",
      "notes": "string | null"
    }
  ]
}
```

#### Posting Effects (Atomic)

1. **Inventory**: Creates a `Disposition` stock movement. Each line produces:
   - **Negative** delta on the source warehouse (stock removed).
   - **Positive** delta on the destination special warehouse (Scrap → SCRAP, Quarantine → QUARANTINE, Rework → REWORK).
   - `WriteOff` has no positive delta — stock is written off entirely.

2. **Concurrency**: Uses atomic `UPDATE … WHERE Status = Draft` gate. Double-post returns idempotent 200 OK with existing movement ID.

3. **Negative Stock**: Posting is rejected with `422 STOCK_NEGATIVE_NOT_ALLOWED` if the source warehouse has insufficient stock.

#### Special Warehouses

Three special warehouses are auto-seeded by the system:

| Name         | Purpose                                    |
|--------------|--------------------------------------------|
| `QUARANTINE` | Holds items pending inspection / decision  |
| `SCRAP`      | Final destination for scrapped items       |
| `REWORK`     | Items sent for repair / rework             |

These warehouses cannot be used as a source warehouse for dispositions.

#### Audit

- Every post and approval is captured by the audit interceptor with correlationId, user, and before/after state.

---

## Error Responses

All errors follow RFC 7807 ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Username is required.",
  "instance": "/api/v1/auth/login",
  "errorCode": "VALIDATION_FAILED",
  "traceId": "correlation-id"
}
```

| HTTP Status | Error Code                      | When                                       |
|-------------|---------------------------------|--------------------------------------------|
| 400         | VALIDATION_FAILED               | Missing/invalid request fields             |
| 401         | INVALID_CREDENTIALS             | Wrong username/password                    |
| 401         | TOKEN_EXPIRED                   | Access/refresh token expired               |
| 401         | TOKEN_INVALID                   | Malformed or revoked token                 |
| 401         | UNAUTHORIZED                    | Missing authentication                     |
| 403         | FORBIDDEN                       | Insufficient permissions                   |
| 403         | ACCOUNT_INACTIVE                | Account deactivated                        |
| 404         | NOT_FOUND                       | Entity not found                           |
| 409         | CONFLICT                        | Duplicate username/role/SKU/code           |
| 409         | BARCODE_ALREADY_EXISTS          | Barcode already taken                      |
| 409         | BARCODE_RETIRED                 | Barcode retired, cannot reuse              |
| 400         | IMPORT_COMMIT_FAILED            | Import commit validation failure           |
| 404         | IMPORT_JOB_NOT_FOUND            | Import job ID not found                    |
| 409         | IMPORT_JOB_ALREADY_COMMITTED    | Import job already committed               |
| 400         | TRANSFER_UNBALANCED             | Transfer lines don't net to zero, per-variant imbalance, or single warehouse |
| 400         | MOVEMENT_EMPTY                  | Stock movement has no lines                |
| 404         | VARIANT_NOT_FOUND               | Variant ID does not exist                  |
| 404         | WAREHOUSE_NOT_FOUND             | Warehouse ID not found or inactive         |
| 422         | STOCK_NEGATIVE_NOT_ALLOWED      | Movement would cause negative balance      |
| 404         | PURCHASE_RECEIPT_NOT_FOUND      | Purchase receipt not found                 |
| 409         | PURCHASE_RECEIPT_ALREADY_POSTED | Cannot modify posted receipt               |
| 400         | PURCHASE_RECEIPT_EMPTY          | Receipt has no lines                       |
| 404         | SUPPLIER_NOT_FOUND              | Supplier not found                         |
| 409         | DOCUMENT_NUMBER_EXISTS          | Document/invoice number duplicate          |
| 404         | PRODUCTION_BATCH_NOT_FOUND      | Production batch not found                 |
| 409         | PRODUCTION_BATCH_ALREADY_POSTED | Cannot modify posted batch                 |
| 400         | PRODUCTION_BATCH_NO_INPUTS      | Batch has no input lines                   |
| 400         | PRODUCTION_BATCH_NO_OUTPUTS     | Batch has no output lines                  |
| 404         | SALES_INVOICE_NOT_FOUND         | Sales invoice not found                    |
| 409         | SALES_INVOICE_ALREADY_POSTED    | Cannot modify posted invoice               |
| 409         | POST_ALREADY_POSTED             | Entity already posted (idempotent 200)     |
| 409         | POST_CONCURRENCY_CONFLICT       | Another post in progress, retry shortly    |
| 400         | SALES_INVOICE_EMPTY             | Invoice has no lines                       |
| 404         | CUSTOMER_NOT_FOUND              | Customer not found                         |
| 404         | PAYMENT_NOT_FOUND               | Payment not found                          |
| 422         | OVERPAYMENT_NOT_ALLOWED         | Payment exceeds outstanding balance        |
| 400         | WALLET_NAME_REQUIRED            | E-wallet method requires wallet name       |
| 400         | INVALID_PAYMENT_METHOD          | Unknown payment method                     |
| 400         | INVALID_PARTY_TYPE              | Party type must be Customer or Supplier    |
| 404         | PARTY_NOT_FOUND                 | Customer/supplier not found                |
| 404         | PRINT_PROFILE_NOT_FOUND         | Print profile not found                    |
| 404         | PRINT_RULE_NOT_FOUND            | Print rule not found                       |
| 409         | PRINT_PROFILE_NAME_EXISTS       | Profile name already taken                 |
| 409         | PRINT_RULE_SCREEN_EXISTS        | Rule for screen already exists in profile  |
| 404         | REASON_CODE_NOT_FOUND           | Reason code not found                      |
| 409         | REASON_CODE_ALREADY_EXISTS      | Reason code already exists                 |
| 409         | REASON_CODE_IN_USE              | Cannot delete reason code in use           |
| 404         | SALES_RETURN_NOT_FOUND          | Sales return not found                     |
| 409         | SALES_RETURN_ALREADY_POSTED     | Cannot modify posted sales return          |
| 400         | SALES_RETURN_EMPTY              | Sales return has no lines                  |
| 409         | RETURN_NUMBER_EXISTS            | Return number already taken                |
| 422         | RETURN_QTY_EXCEEDS_SOLD         | Return qty exceeds sold qty for variant    |
| 400         | REASON_CODE_INACTIVE            | Reason code is inactive                    |
| 409         | SALES_RETURN_ALREADY_VOIDED     | Sales return already voided                |
| 400         | SALES_RETURN_NOT_POSTED         | Can only void a posted sales return        |
| 400         | SALES_RETURN_DISPOSITION_NOT_ALLOWED | Disposition not allowed in RET 1 (use RET 3) |
| 409         | SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST | Cannot void a posted return (RET 1 policy A1) |
| 404         | PURCHASE_RETURN_NOT_FOUND       | Purchase return not found                  |
| 409         | PURCHASE_RETURN_ALREADY_POSTED  | Cannot modify posted purchase return       |
| 400         | PURCHASE_RETURN_EMPTY           | Purchase return has no lines               |
| 409         | PURCHASE_RETURN_NUMBER_EXISTS   | Purchase return number already taken       |
| 422         | RETURN_QTY_EXCEEDS_RECEIVED     | Return qty exceeds received qty for variant|
| 409         | PURCHASE_RETURN_ALREADY_VOIDED  | Purchase return already voided             |
| 409         | PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST | Cannot void a posted purchase return |
| 404         | DISPOSITION_NOT_FOUND           | Disposition not found                      |
| 409         | DISPOSITION_ALREADY_POSTED      | Cannot modify posted disposition           |
| 400         | DISPOSITION_EMPTY               | Disposition has no lines                   |
| 409         | DISPOSITION_ALREADY_VOIDED      | Disposition already voided                 |
| 409         | DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST | Cannot void a posted disposition  |
| 409         | DISPOSITION_NUMBER_EXISTS       | Disposition number already taken           |
| 409         | DISPOSITION_REQUIRES_APPROVAL   | Manager approval required before posting   |
| 400         | DISPOSITION_INVALID_TYPE        | Disposition type not allowed for pre-sale  |
| 404         | DESTINATION_WAREHOUSE_NOT_FOUND | Special destination warehouse not found    |
| 500         | INTERNAL_ERROR                  | Unhandled server error                     |

---

## Permissions

| Code                     | Description                                      |
|--------------------------|--------------------------------------------------|
| USERS_READ               | View users                                       |
| USERS_WRITE              | Create, update, deactivate users                 |
| ROLES_READ               | View roles and permissions                       |
| ROLES_WRITE              | Create, update, delete roles                     |
| AUDIT_READ               | View audit logs                                  |
| PRODUCTS_READ            | View products and variants                       |
| PRODUCTS_WRITE           | Create, update, delete products and variants     |
| CUSTOMERS_READ           | View customers                                   |
| CUSTOMERS_WRITE          | Create, update, delete customers                 |
| SUPPLIERS_READ           | View suppliers                                   |
| SUPPLIERS_WRITE          | Create, update, delete suppliers                 |
| IMPORT_MASTER_DATA       | Import master data from CSV/XLSX                 |
| STOCK_READ               | View stock balances and ledger                   |
| STOCK_POST               | Post stock movements                             |
| WAREHOUSES_READ          | View warehouses                                  |
| WAREHOUSES_WRITE         | Create, update, delete warehouses                |
| PURCHASES_READ           | View purchase receipts                           |
| PURCHASES_WRITE          | Create, update, delete purchase receipts         |
| PURCHASES_POST           | Post purchase receipts to inventory              |
| PRODUCTION_READ          | View production batches                          |
| PRODUCTION_WRITE         | Create, update, delete production batches        |
| PRODUCTION_POST          | Post production batches to inventory             |
| SALES_READ               | View sales invoices                              |
| SALES_WRITE              | Create, update, delete sales invoices            |
| SALES_POST               | Post sales invoices to inventory                 |
| ACCOUNTING_READ          | View AR/AP ledger and balances                   |
| PAYMENTS_READ            | View payments                                    |
| PAYMENTS_WRITE           | Create payments                                  |
| IMPORT_OPENING_BALANCES  | Import opening balances from CSV/XLSX            |
| IMPORT_PAYMENTS          | Import payments from CSV/XLSX                    |
| DASHBOARD_READ           | View dashboard KPIs and analytics                |
| MANAGE_PRINTING_POLICY   | Manage printing profiles and rules               |
| MANAGE_REASON_CODES      | Create, update, disable reason codes             |
| VIEW_REASON_CODES        | View reason codes catalog                        |
| SALES_RETURN_CREATE      | Create and edit draft sales returns              |
| SALES_RETURN_POST        | Post sales returns to inventory                  |
| SALES_RETURN_VOID        | Void posted sales returns                        |
| VIEW_SALES_RETURNS       | View sales returns                               |
| PURCHASE_RETURN_CREATE   | Create and edit draft purchase returns            |
| PURCHASE_RETURN_POST     | Post purchase returns to inventory               |
| PURCHASE_RETURN_VOID     | Void draft purchase returns                      |
| VIEW_PURCHASE_RETURNS    | View purchase returns                            |
| DISPOSITION_CREATE       | Create and edit draft dispositions               |
| DISPOSITION_POST         | Post dispositions to inventory                   |
| DISPOSITION_APPROVE      | Approve dispositions (manager approval)          |
| DISPOSITION_VOID         | Void draft dispositions                          |
| VIEW_DISPOSITIONS        | View dispositions                                |
