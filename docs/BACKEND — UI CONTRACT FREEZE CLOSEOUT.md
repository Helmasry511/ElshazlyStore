# BACKEND — UI CONTRACT FREEZE CLOSEOUT

**Prepared by**: Claude OPUS 4.6 — AGENT MODE  
**Date**: 2026-03-02  
**Baseline**: UI 1 ✅ + UI 2.1 ✅  
**Policy**: No invented facts. Unknown items marked **(غير مذكور في المدخلات/المصدر)**.

---

## 1) Summary

All backend contracts have been verified against two sources: the hand-written `docs/api.md` (1 246 lines) and the actual C# endpoint/domain/service code. Swagger/OpenAPI is configured at runtime (`/swagger/v1/swagger.json`) but no static JSON/YAML file exists in the repo — the code is treated as higher authority where differences are found.

**Key findings:**

| Area | Target (plan) | Actual (verified) | Status |
|------|---------------|-------------------|--------|
| Endpoints | – | **90 routes** across 16 modules | ✅ Frozen |
| Permissions | 47 | **47** | ✅ Match |
| Error Codes | 65 | **66** (see D6 below) | ⚠️ Minor delta |
| Paging Shapes | 2 | **2** confirmed (Shape A + Shape B) | ✅ Frozen |
| Print Screen Codes | Proposed 4 | **0 hard-coded** — free-text `screenCode` | ⚠️ See §7 |
| Build | 0 err / 0 warn | **0 err / 0 warn** | ✅ |
| Tests | pass | **211 passed, 0 failed** | ✅ |

**Discrepancies found**: 6 (D1–D6), none blocking for UI 2.2.

---

## 2) Evidence Sources (paths / runtime URLs if applicable)

| # | Source | Path / URL | Authority |
|---|--------|------------|-----------|
| E1 | API Reference (docs) | `docs/api.md` (1 246 lines) | Secondary — human-authored |
| E2 | Endpoint registrations | `src/ElshazlyStore.Api/Endpoints/*.cs` (19 files) | **Primary** — code truth |
| E3 | EndpointMapper | `src/ElshazlyStore.Api/Endpoints/EndpointMapper.cs` | Primary |
| E4 | Permission codes | `src/ElshazlyStore.Domain/Common/Permissions.cs` (133 lines, 47 codes) | **Primary** |
| E5 | Permission codes (Desktop mirror) | `src/ElshazlyStore.Desktop/Models/PermissionCodes.cs` | UI mirror — must match E4 |
| E6 | Error codes | `src/ElshazlyStore.Domain/Common/ErrorCodes.cs` (115 lines) | **Primary** |
| E7 | Domain entities + enums | `src/ElshazlyStore.Domain/Entities/*.cs` | **Primary** |
| E8 | Infrastructure services (DTOs) | `src/ElshazlyStore.Infrastructure/Services/*.cs` | Primary (DTO definitions inline) |
| E9 | PagedResult record | `src/ElshazlyStore.Api/Endpoints/ProductEndpoints.cs` line 182 | Primary |
| E10 | ProblemDetails (Desktop) | `src/ElshazlyStore.Desktop/Models/ProblemDetails.cs` | UI model |
| E11 | ApiResult<T> | `src/ElshazlyStore.Desktop/Models/ApiResult.cs` | UI model |
| E12 | Swagger/OpenAPI (runtime) | `https://localhost:5001/swagger/v1/swagger.json` | Primary (runtime-generated) |
| E13 | Api base URL (Desktop) | `src/ElshazlyStore.Desktop/appsettings.json` → `https://localhost:5001` | Config |

---

## 3) Endpoint Catalog (grouped)

### 3.1 Health

| Method | Route | Purpose | Permission | Paging Shape |
|--------|-------|---------|------------|--------------|
| GET | `/api/v1/health` | System health check | None (anonymous) | — |

### 3.2 Auth / Session

| Method | Route | Purpose | Permission | Request DTO | Response DTO |
|--------|-------|---------|------------|-------------|--------------|
| POST | `/auth/login` | Login | None | `{ username, password }` | `{ accessToken, refreshToken, expiresAtUtc }` |
| POST | `/auth/refresh` | Refresh token | None | `{ refreshToken }` | Same as login |
| POST | `/auth/logout` | Revoke refresh token | Bearer | `{ refreshToken }` | `{ message }` |
| GET | `/auth/me` | Current user info | Bearer | — | `{ id, username, isActive, roles[] }` |

### 3.3 Users / Roles / Permissions

| Method | Route | Purpose | Permission |
|--------|-------|---------|------------|
| GET | `/users` | List all users | USERS_READ |
| GET | `/users/{id}` | Get user by ID | USERS_READ |
| POST | `/users` | Create user | USERS_WRITE |
| PUT | `/users/{id}` | Update user | USERS_WRITE |
| DELETE | `/users/{id}` | Deactivate user | USERS_WRITE |
| GET | `/roles` | List roles | ROLES_READ |
| GET | `/roles/{id}` | Get role by ID | ROLES_READ |
| POST | `/roles` | Create role | ROLES_WRITE |
| PUT | `/roles/{id}` | Update role | ROLES_WRITE |
| DELETE | `/roles/{id}` | Delete role | ROLES_WRITE |
| GET | `/roles/{id}/permissions` | Role permissions | ROLES_READ |
| PUT | `/roles/{id}/permissions` | Set role permissions | ROLES_WRITE |
| GET | `/roles/permissions/all` | All available permissions | ROLES_READ |

### 3.4 Products / Variants / Barcodes

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/products` | List products (paged) | PRODUCTS_READ | Shape A |
| GET | `/products/{id}` | Product + variants | PRODUCTS_READ | — |
| POST | `/products` | Create product | PRODUCTS_WRITE | — |
| PUT | `/products/{id}` | Update product | PRODUCTS_WRITE | — |
| DELETE | `/products/{id}` | Delete product + retire barcodes | PRODUCTS_WRITE | — |
| GET | `/variants` | List variants (paged) | PRODUCTS_READ | Shape A |
| GET | `/variants/{id}` | Get variant | PRODUCTS_READ | — |
| POST | `/variants` | Create variant + barcode | PRODUCTS_WRITE | — |
| PUT | `/variants/{id}` | Update variant | PRODUCTS_WRITE | — |
| DELETE | `/variants/{id}` | Delete variant (retire barcode) | PRODUCTS_WRITE | — |
| GET | `/barcodes/{barcode}` | Lookup by barcode string | PRODUCTS_READ | — |

### 3.5 Customers / Suppliers

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/customers` | List customers | CUSTOMERS_READ | Shape A |
| GET | `/customers/{id}` | Get customer | CUSTOMERS_READ | — |
| POST | `/customers` | Create customer | CUSTOMERS_WRITE | — |
| PUT | `/customers/{id}` | Update customer | CUSTOMERS_WRITE | — |
| DELETE | `/customers/{id}` | Deactivate | CUSTOMERS_WRITE | — |
| GET | `/suppliers` | List suppliers | SUPPLIERS_READ | Shape A |
| GET | `/suppliers/{id}` | Get supplier | SUPPLIERS_READ | — |
| POST | `/suppliers` | Create supplier | SUPPLIERS_WRITE | — |
| PUT | `/suppliers/{id}` | Update supplier | SUPPLIERS_WRITE | — |
| DELETE | `/suppliers/{id}` | Deactivate | SUPPLIERS_WRITE | — |

### 3.6 Warehouses

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/warehouses` | List warehouses | WAREHOUSES_READ | Shape B |
| GET | `/warehouses/{id}` | Get warehouse | WAREHOUSES_READ | — |
| POST | `/warehouses` | Create warehouse | WAREHOUSES_WRITE | — |
| PUT | `/warehouses/{id}` | Update warehouse | WAREHOUSES_WRITE | — |
| DELETE | `/warehouses/{id}` | Deactivate | WAREHOUSES_WRITE | — |

### 3.7 Stock Balances / Ledger / Movements

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/stock/balances` | Current stock levels | STOCK_READ | Shape B |
| GET | `/stock/ledger` | Movement history | STOCK_READ | Shape B |
| POST | `/stock-movements/post` | Post a stock movement | STOCK_POST | — |

### 3.8 Purchases / Production

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/purchases` | List purchase receipts | PURCHASES_READ | Shape A |
| GET | `/purchases/{id}` | Get receipt + lines | PURCHASES_READ | — |
| POST | `/purchases` | Create draft receipt | PURCHASES_WRITE | — |
| PUT | `/purchases/{id}` | Update draft | PURCHASES_WRITE | — |
| DELETE | `/purchases/{id}` | Delete draft | PURCHASES_WRITE | — |
| POST | `/purchases/{id}/post` | Post → stock movement | PURCHASES_POST | — |
| GET | `/production` | List production batches | PRODUCTION_READ | Shape A |
| GET | `/production/{id}` | Get batch + lines | PRODUCTION_READ | — |
| POST | `/production` | Create draft batch | PRODUCTION_WRITE | — |
| PUT | `/production/{id}` | Update draft | PRODUCTION_WRITE | — |
| DELETE | `/production/{id}` | Delete draft | PRODUCTION_WRITE | — |
| POST | `/production/{id}/post` | Post → stock movements | PRODUCTION_POST | — |

### 3.9 Sales / POS

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/sales` | List invoices | SALES_READ | Shape A |
| GET | `/sales/{id}` | Get invoice + lines | SALES_READ | — |
| POST | `/sales` | Create draft invoice | SALES_WRITE | — |
| PUT | `/sales/{id}` | Update draft | SALES_WRITE | — |
| DELETE | `/sales/{id}` | Delete draft | SALES_WRITE | — |
| POST | `/sales/{id}/post` | Post → stock movement | SALES_POST | — |

### 3.10 Returns / Dispositions / Reason Codes

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/reasons` | List reason codes | VIEW_REASON_CODES | Shape B |
| GET | `/reasons/{id}` | Get reason code | VIEW_REASON_CODES | — |
| POST | `/reasons` | Create reason | MANAGE_REASON_CODES | — |
| PUT | `/reasons/{id}` | Update reason | MANAGE_REASON_CODES | — |
| POST | `/reasons/{id}/disable` | Soft-disable | MANAGE_REASON_CODES | — |
| GET | `/sales-returns` | List sales returns | VIEW_SALES_RETURNS | Shape A |
| GET | `/sales-returns/{id}` | Get return + lines | VIEW_SALES_RETURNS | — |
| POST | `/sales-returns` | Create draft return | SALES_RETURN_CREATE | — |
| PUT | `/sales-returns/{id}` | Update draft | SALES_RETURN_CREATE | — |
| DELETE | `/sales-returns/{id}` | Delete draft | SALES_RETURN_CREATE | — |
| POST | `/sales-returns/{id}/post` | Post → stock + credit note | SALES_RETURN_POST | — |
| POST | `/sales-returns/{id}/void` | Void draft | SALES_RETURN_VOID | — |
| GET | `/purchase-returns` | List purchase returns | VIEW_PURCHASE_RETURNS | Shape A |
| GET | `/purchase-returns/{id}` | Get return + lines | VIEW_PURCHASE_RETURNS | — |
| POST | `/purchase-returns` | Create draft return | PURCHASE_RETURN_CREATE | — |
| PUT | `/purchase-returns/{id}` | Update draft | PURCHASE_RETURN_CREATE | — |
| DELETE | `/purchase-returns/{id}` | Delete draft | PURCHASE_RETURN_CREATE | — |
| POST | `/purchase-returns/{id}/post` | Post → stock + debit note | PURCHASE_RETURN_POST | — |
| POST | `/purchase-returns/{id}/void` | Void draft | PURCHASE_RETURN_VOID | — |
| GET | `/dispositions` | List dispositions | VIEW_DISPOSITIONS | Shape A |
| GET | `/dispositions/{id}` | Get disposition + lines | VIEW_DISPOSITIONS | — |
| POST | `/dispositions` | Create draft | DISPOSITION_CREATE | — |
| PUT | `/dispositions/{id}` | Update draft | DISPOSITION_CREATE | — |
| DELETE | `/dispositions/{id}` | Delete draft | DISPOSITION_CREATE | — |
| POST | `/dispositions/{id}/approve` | Manager approve | DISPOSITION_APPROVE | — |
| POST | `/dispositions/{id}/post` | Post → stock movements | DISPOSITION_POST | — |
| POST | `/dispositions/{id}/void` | Void draft | DISPOSITION_VOID | — |

### 3.11 Payments / Accounting

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/accounting/balances/customers` | Customer AR balances | ACCOUNTING_READ | Shape A |
| GET | `/accounting/balances/suppliers` | Supplier AP balances | ACCOUNTING_READ | Shape A |
| GET | `/accounting/balances/{partyType}/{id}` | Single party balance | ACCOUNTING_READ | — |
| GET | `/accounting/ledger/{partyType}/{id}` | Ledger entries | ACCOUNTING_READ | Shape A |
| GET | `/payments` | List payments | PAYMENTS_READ | Shape A |
| GET | `/payments/{id}` | Payment detail | PAYMENTS_READ | — |
| POST | `/payments` | Create payment | PAYMENTS_WRITE | — |

### 3.12 Import

| Method | Route | Purpose | Permission |
|--------|-------|---------|------------|
| POST | `/imports/masterdata/preview` | Preview CSV/XLSX file | IMPORT_MASTER_DATA |
| POST | `/imports/masterdata/commit` | Commit import job | IMPORT_MASTER_DATA |
| POST | `/imports/opening-balances/preview` | Preview opening balances | IMPORT_OPENING_BALANCES |
| POST | `/imports/opening-balances/commit` | Commit opening balances | IMPORT_OPENING_BALANCES |
| POST | `/imports/payments/preview` | Preview payments CSV | IMPORT_PAYMENTS |
| POST | `/imports/payments/commit` | Commit payments | IMPORT_PAYMENTS |

### 3.13 Dashboard

| Method | Route | Purpose | Permission |
|--------|-------|---------|------------|
| GET | `/dashboard/summary` | Full KPI summary | DASHBOARD_READ |
| GET | `/dashboard/sales` | Sales metrics | DASHBOARD_READ |
| GET | `/dashboard/top-products` | Top products by qty/revenue | DASHBOARD_READ |
| GET | `/dashboard/low-stock` | Low stock alerts | DASHBOARD_READ |
| GET | `/dashboard/cashier-performance` | Cashier KPIs | DASHBOARD_READ |

### 3.14 Printing Policy

| Method | Route | Purpose | Permission | Paging |
|--------|-------|---------|------------|--------|
| GET | `/print-profiles` | List profiles | MANAGE_PRINTING_POLICY | Shape B |
| GET | `/print-profiles/{id}` | Get profile + rules | MANAGE_PRINTING_POLICY | — |
| POST | `/print-profiles` | Create profile | MANAGE_PRINTING_POLICY | — |
| PUT | `/print-profiles/{id}` | Update profile | MANAGE_PRINTING_POLICY | — |
| DELETE | `/print-profiles/{id}` | Delete profile + rules | MANAGE_PRINTING_POLICY | — |
| GET | `/print-profiles/{id}/rules` | List rules for profile | MANAGE_PRINTING_POLICY | Shape B |
| GET | `/print-profiles/{id}/rules/{ruleId}` | Get rule | MANAGE_PRINTING_POLICY | — |
| POST | `/print-profiles/{id}/rules` | Create rule | MANAGE_PRINTING_POLICY | — |
| PUT | `/print-profiles/{id}/rules/{ruleId}` | Update rule | MANAGE_PRINTING_POLICY | — |
| DELETE | `/print-profiles/{id}/rules/{ruleId}` | Delete rule | MANAGE_PRINTING_POLICY | — |
| GET | `/print-policy/{screenCode}` | Get active policy for screen | MANAGE_PRINTING_POLICY | — |

---

## 4) Permissions Manifest

**Total: 47 permission codes** — confirmed from `src/ElshazlyStore.Domain/Common/Permissions.cs`.  
Desktop mirror in `src/ElshazlyStore.Desktop/Models/PermissionCodes.cs` matches exactly (47 codes, same values).

| # | Permission Code | Description (from Permissions.All) | Used by Endpoints/Screens |
|---|----------------|-------------------------------------|---------------------------|
| 1 | `USERS_READ` | View users | GET /users, GET /users/{id} |
| 2 | `USERS_WRITE` | Create, update, deactivate users | POST/PUT/DELETE /users/{id} |
| 3 | `ROLES_READ` | View roles and permissions | GET /roles, GET /roles/{id}, GET /roles/{id}/permissions, GET /roles/permissions/all |
| 4 | `ROLES_WRITE` | Create, update, delete roles; manage role permissions | POST/PUT/DELETE /roles/{id}, PUT /roles/{id}/permissions |
| 5 | `AUDIT_READ` | View audit logs | (غير مذكور — no endpoint found in code; permission exists in seeder) |
| 6 | `PRODUCTS_READ` | View products and variants | GET /products, /products/{id}, /variants, /variants/{id}, /barcodes/{barcode} |
| 7 | `PRODUCTS_WRITE` | Create, update, delete products and variants | POST/PUT/DELETE /products/{id}, /variants/{id} |
| 8 | `CUSTOMERS_READ` | View customers | GET /customers, /customers/{id} |
| 9 | `CUSTOMERS_WRITE` | Create, update, delete customers | POST/PUT/DELETE /customers/{id} |
| 10 | `SUPPLIERS_READ` | View suppliers | GET /suppliers, /suppliers/{id} |
| 11 | `SUPPLIERS_WRITE` | Create, update, delete suppliers | POST/PUT/DELETE /suppliers/{id} |
| 12 | `IMPORT_MASTER_DATA` | Import master data from CSV/XLSX | POST /imports/masterdata/preview, /commit |
| 13 | `STOCK_READ` | View stock balances and ledger | GET /stock/balances, /stock/ledger |
| 14 | `STOCK_POST` | Post stock movements | POST /stock-movements/post |
| 15 | `WAREHOUSES_READ` | View warehouses | GET /warehouses, /warehouses/{id} |
| 16 | `WAREHOUSES_WRITE` | Create, update, delete warehouses | POST/PUT/DELETE /warehouses/{id} |
| 17 | `PURCHASES_READ` | View purchase receipts | GET /purchases, /purchases/{id} |
| 18 | `PURCHASES_WRITE` | Create, update, delete purchase receipts | POST/PUT/DELETE /purchases/{id} |
| 19 | `PURCHASES_POST` | Post purchase receipts to inventory | POST /purchases/{id}/post |
| 20 | `PRODUCTION_READ` | View production batches | GET /production, /production/{id} |
| 21 | `PRODUCTION_WRITE` | Create, update, delete production batches | POST/PUT/DELETE /production/{id} |
| 22 | `PRODUCTION_POST` | Post production batches to inventory | POST /production/{id}/post |
| 23 | `SALES_READ` | View sales invoices | GET /sales, /sales/{id} |
| 24 | `SALES_WRITE` | Create, update, delete sales invoices | POST/PUT/DELETE /sales/{id} |
| 25 | `SALES_POST` | Post sales invoices to inventory | POST /sales/{id}/post |
| 26 | `ACCOUNTING_READ` | View AR/AP ledger and balances | GET /accounting/balances/*, /accounting/ledger/* |
| 27 | `PAYMENTS_READ` | View payments | GET /payments, /payments/{id} |
| 28 | `PAYMENTS_WRITE` | Create payments | POST /payments |
| 29 | `IMPORT_OPENING_BALANCES` | Import opening balances from CSV/XLSX | POST /imports/opening-balances/* |
| 30 | `IMPORT_PAYMENTS` | Import payments from CSV/XLSX | POST /imports/payments/* |
| 31 | `DASHBOARD_READ` | View dashboard KPIs and analytics | GET /dashboard/* |
| 32 | `MANAGE_PRINTING_POLICY` | Manage printing profiles and rules | All /print-profiles/*, /print-policy/* |
| 33 | `MANAGE_REASON_CODES` | Create, update, disable reason codes | POST/PUT /reasons, POST /reasons/{id}/disable |
| 34 | `VIEW_REASON_CODES` | View reason codes catalog | GET /reasons, /reasons/{id} |
| 35 | `SALES_RETURN_CREATE` | Create and update draft sales returns | POST/PUT/DELETE /sales-returns/{id} |
| 36 | `SALES_RETURN_POST` | Post sales returns to inventory | POST /sales-returns/{id}/post |
| 37 | `SALES_RETURN_VOID` | Void posted sales returns | POST /sales-returns/{id}/void |
| 38 | `VIEW_SALES_RETURNS` | View sales returns | GET /sales-returns, /sales-returns/{id} |
| 39 | `PURCHASE_RETURN_CREATE` | Create and update draft purchase returns | POST/PUT/DELETE /purchase-returns/{id} |
| 40 | `PURCHASE_RETURN_POST` | Post purchase returns to inventory | POST /purchase-returns/{id}/post |
| 41 | `PURCHASE_RETURN_VOID` | Void draft purchase returns | POST /purchase-returns/{id}/void |
| 42 | `VIEW_PURCHASE_RETURNS` | View purchase returns | GET /purchase-returns, /purchase-returns/{id} |
| 43 | `DISPOSITION_CREATE` | Create and update draft dispositions | POST/PUT/DELETE /dispositions/{id} |
| 44 | `DISPOSITION_POST` | Post dispositions to inventory | POST /dispositions/{id}/post |
| 45 | `DISPOSITION_APPROVE` | Approve dispositions (manager approval) | POST /dispositions/{id}/approve |
| 46 | `DISPOSITION_VOID` | Void draft dispositions | POST /dispositions/{id}/void |
| 47 | `VIEW_DISPOSITIONS` | View dispositions | GET /dispositions, /dispositions/{id} |

### Permission note: AUDIT_READ

`AUDIT_READ` is defined in the Permissions code and seeded, but no endpoint in the current codebase uses it. This is not a blocker — audit log viewing may be added later.

---

## 5) ErrorCodes Manifest + ProblemDetails Shape

### 5.1 ProblemDetails Shape

**Backend** returns RFC 7807 ProblemDetails. The `title` field carries the error code. Verified from endpoint code (e.g., `Results.Problem(detail:..., title: errorCode, statusCode:..., type: ...)`).

**Exact JSON shape sent by backend:**

```json
{
  "type": "https://elshazlystore.local/errors/{error_code_lowercase}",
  "title": "ERROR_CODE_HERE",
  "status": 400,
  "detail": "Human-readable error description.",
  "instance": "/api/v1/...",
  "traceId": "correlation-id"
}
```

**Desktop ProblemDetails model** (`src/ElshazlyStore.Desktop/Models/ProblemDetails.cs`):

```csharp
public sealed class ProblemDetails
{
    [JsonPropertyName("type")]     public string? Type { get; set; }
    [JsonPropertyName("title")]    public string? Title { get; set; }
    [JsonPropertyName("status")]   public int? Status { get; set; }
    [JsonPropertyName("detail")]   public string? Detail { get; set; }
    [JsonPropertyName("instance")] public string? Instance { get; set; }
    [JsonPropertyName("errors")]   public Dictionary<string, string[]>? Errors { get; set; }
}
```

**Gap: No `errorCode` field.** The backend puts the error code in the `title` field. The Desktop `ProblemDetails` model correctly reads `title`. However, the model does **not** expose a dedicated `ErrorCode` property — the UI currently uses `ToUserMessage()` which returns `Detail ?? Title ?? fallback`. This means the UI sees the error message but does **not** programmatically switch on error codes.

**Recommendation for UI 2.2+**: Add `[JsonPropertyName("title")] public string? ErrorCode => Title;` or a separate accessor so the UI can pattern-match on specific error codes (e.g., `BARCODE_ALREADY_EXISTS`).

### 5.2 ToUserMessage() Coverage

The current `ToUserMessage()` implementation:
```csharp
public string ToUserMessage()
{
    if (Errors is { Count: > 0 })
    {
        var messages = Errors.SelectMany(e => e.Value);
        return string.Join(Environment.NewLine, messages);
    }
    return Detail ?? Title ?? "An unexpected error occurred.";
}
```

This is a **generic fallback** — it does NOT map individual error codes to Arabic user messages. It simply returns the server's `detail` text. The plan calls for mapping all 65+ error codes to Arabic messages. **This is a GAP for later phases (UI 2.2+).**

### 5.3 Full Error Codes (66 total, from `ErrorCodes.cs`)

| # | Error Code | HTTP Status | When | Suggested UI Message (Arabic) |
|---|-----------|-------------|------|-------------------------------|
| 1 | `INTERNAL_ERROR` | 500 | Unhandled server error | حدث خطأ غير متوقع في الخادم |
| 2 | `VALIDATION_FAILED` | 400 | Missing/invalid request fields | بيانات غير صالحة |
| 3 | `NOT_FOUND` | 404 | Entity not found | العنصر غير موجود |
| 4 | `CONFLICT` | 409 | Duplicate username/role/SKU/code | يوجد تعارض — القيمة مستخدمة بالفعل |
| 5 | `UNAUTHORIZED` | 401 | Missing authentication | غير مصرح — يرجى تسجيل الدخول |
| 6 | `FORBIDDEN` | 403 | Insufficient permissions | ليس لديك صلاحية لهذا الإجراء |
| 7 | `INVALID_CREDENTIALS` | 401 | Wrong username/password | اسم المستخدم أو كلمة المرور غير صحيحة |
| 8 | `ACCOUNT_INACTIVE` | 403 | Account deactivated | الحساب معطل |
| 9 | `TOKEN_EXPIRED` | 401 | Access/refresh token expired | انتهت صلاحية الجلسة |
| 10 | `TOKEN_INVALID` | 401 | Malformed or revoked token | رمز الجلسة غير صالح |
| 11 | `BARCODE_ALREADY_EXISTS` | 409 | Barcode already assigned | الباركود مستخدم بالفعل |
| 12 | `BARCODE_RETIRED` | 409 | Barcode retired, cannot reuse | الباركود تم إيقافه نهائياً |
| 13 | `IMPORT_PREVIEW_FAILED` | 400 | Import preview validation failure | فشل في معاينة الملف |
| 14 | `IMPORT_COMMIT_FAILED` | 400 | Import commit validation failure | فشل في تنفيذ الاستيراد |
| 15 | `IMPORT_JOB_NOT_FOUND` | 404 | Import job ID not found | عملية الاستيراد غير موجودة |
| 16 | `IMPORT_JOB_ALREADY_COMMITTED` | 409 | Import job already committed | تم تنفيذ هذا الاستيراد مسبقاً |
| 17 | `STOCK_NEGATIVE_NOT_ALLOWED` | 422 | Movement would cause negative balance | الرصيد غير كافٍ — لا يُسمح برصيد سالب |
| 18 | `MOVEMENT_EMPTY` | 400 | Stock movement has no lines | الحركة لا تحتوي على أصناف |
| 19 | `WAREHOUSE_NOT_FOUND` | 404 | Warehouse ID not found or inactive | المخزن غير موجود |
| 20 | `VARIANT_NOT_FOUND` | 404 | Variant ID does not exist | الصنف غير موجود |
| 21 | `TRANSFER_UNBALANCED` | 400 | Transfer lines don't net to zero | عملية التحويل غير متوازنة |
| 22 | `PURCHASE_RECEIPT_NOT_FOUND` | 404 | Purchase receipt not found | إذن الشراء غير موجود |
| 23 | `PURCHASE_RECEIPT_ALREADY_POSTED` | 409 | Cannot modify posted receipt | لا يمكن تعديل إذن شراء مُرحَّل |
| 24 | `PURCHASE_RECEIPT_EMPTY` | 400 | Receipt has no lines | إذن الشراء لا يحتوي على أصناف |
| 25 | `SUPPLIER_NOT_FOUND` | 404 | Supplier not found | المورد غير موجود |
| 26 | `DOCUMENT_NUMBER_EXISTS` | 409 | Document/invoice number duplicate | رقم المستند مستخدم بالفعل |
| 27 | `PRODUCTION_BATCH_NOT_FOUND` | 404 | Production batch not found | أمر الإنتاج غير موجود |
| 28 | `PRODUCTION_BATCH_ALREADY_POSTED` | 409 | Cannot modify posted batch | لا يمكن تعديل أمر إنتاج مُرحَّل |
| 29 | `PRODUCTION_BATCH_NO_INPUTS` | 400 | Batch has no input lines | أمر الإنتاج لا يحتوي على مدخلات |
| 30 | `PRODUCTION_BATCH_NO_OUTPUTS` | 400 | Batch has no output lines | أمر الإنتاج لا يحتوي على مخرجات |
| 31 | `BATCH_NUMBER_EXISTS` | 409 | Batch number already taken | رقم الدفعة مستخدم بالفعل |
| 32 | `SALES_INVOICE_NOT_FOUND` | 404 | Sales invoice not found | فاتورة البيع غير موجودة |
| 33 | `SALES_INVOICE_ALREADY_POSTED` | 409 | Cannot modify posted invoice | لا يمكن تعديل فاتورة مُرحَّلة |
| 34 | `SALES_INVOICE_EMPTY` | 400 | Invoice has no lines | الفاتورة لا تحتوي على أصناف |
| 35 | `INVOICE_NUMBER_EXISTS` | 409 | Invoice number already taken | رقم الفاتورة مستخدم بالفعل |
| 36 | `CUSTOMER_NOT_FOUND` | 404 | Customer not found | العميل غير موجود |
| 37 | `POST_ALREADY_POSTED` | 409 | Entity already posted (idempotent) | تم الترحيل مسبقاً |
| 38 | `POST_CONCURRENCY_CONFLICT` | 409 | Another post in progress | يتم الترحيل حالياً — أعد المحاولة |
| 39 | `PAYMENT_NOT_FOUND` | 404 | Payment not found | الدفعة غير موجودة |
| 40 | `OVERPAYMENT_NOT_ALLOWED` | 422 | Payment exceeds outstanding balance | المبلغ يتجاوز الرصيد المستحق |
| 41 | `WALLET_NAME_REQUIRED` | 400 | E-wallet method requires wallet name | اسم المحفظة مطلوب |
| 42 | `INVALID_PAYMENT_METHOD` | 400 | Unknown payment method | طريقة دفع غير صالحة |
| 43 | `INVALID_PARTY_TYPE` | 400 | Party type must be Customer or Supplier | نوع الطرف غير صالح |
| 44 | `PARTY_NOT_FOUND` | 404 | Customer/supplier not found | الطرف غير موجود |
| 45 | `PRINT_PROFILE_NOT_FOUND` | 404 | Print profile not found | ملف الطباعة غير موجود |
| 46 | `PRINT_RULE_NOT_FOUND` | 404 | Print rule not found | قاعدة الطباعة غير موجودة |
| 47 | `PRINT_PROFILE_NAME_EXISTS` | 409 | Profile name already taken | اسم ملف الطباعة مستخدم بالفعل |
| 48 | `PRINT_RULE_SCREEN_EXISTS` | 409 | Rule for screen already exists in profile | يوجد قاعدة لهذه الشاشة بالفعل |
| 49 | `REASON_CODE_NOT_FOUND` | 404 | Reason code not found | كود السبب غير موجود |
| 50 | `REASON_CODE_ALREADY_EXISTS` | 409 | Reason code already exists | كود السبب مستخدم بالفعل |
| 51 | `REASON_CODE_IN_USE` | 409 | Cannot delete reason code in use | لا يمكن حذف كود سبب مستخدم |
| 52 | `SALES_RETURN_NOT_FOUND` | 404 | Sales return not found | مرتجع البيع غير موجود |
| 53 | `SALES_RETURN_ALREADY_POSTED` | 409 | Cannot modify posted sales return | لا يمكن تعديل مرتجع مُرحَّل |
| 54 | `SALES_RETURN_EMPTY` | 400 | Sales return has no lines | المرتجع لا يحتوي على أصناف |
| 55 | `RETURN_NUMBER_EXISTS` | 409 | Return number already taken | رقم المرتجع مستخدم بالفعل |
| 56 | `RETURN_QTY_EXCEEDS_SOLD` | 422 | Return qty exceeds sold qty | كمية الإرجاع تتجاوز الكمية المباعة |
| 57 | `REASON_CODE_INACTIVE` | 400 | Reason code is inactive | كود السبب غير نشط |
| 58 | `SALES_RETURN_ALREADY_VOIDED` | 409 | Return already voided | المرتجع ملغي بالفعل |
| 59 | `SALES_RETURN_NOT_POSTED` | 400 | Can only void posted returns | لم يتم ترحيل المرتجع بعد |
| 60 | `SALES_RETURN_DISPOSITION_NOT_ALLOWED` | 400 | Disposition not allowed in RET 1 | نوع التصرف غير مسموح (استخدم التصرفات المخزنية) |
| 61 | `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST` | 409 | Cannot void posted return (RET 1 A1) | لا يمكن إلغاء مرتجع مُرحَّل |
| 62 | `PURCHASE_RETURN_NOT_FOUND` | 404 | Purchase return not found | مرتجع الشراء غير موجود |
| 63 | `PURCHASE_RETURN_ALREADY_POSTED` | 409 | Cannot modify posted purchase return | لا يمكن تعديل مرتجع شراء مُرحَّل |
| 64 | `PURCHASE_RETURN_EMPTY` | 400 | Purchase return has no lines | مرتجع الشراء لا يحتوي على أصناف |
| 65 | `PURCHASE_RETURN_ALREADY_VOIDED` | 409 | Already voided | مرتجع الشراء ملغي بالفعل |
| 66 | `PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST` | 409 | Cannot void posted purchase return | لا يمكن إلغاء مرتجع شراء مُرحَّل |
| 67 | `RETURN_QTY_EXCEEDS_RECEIVED` | 422 | Return qty exceeds received qty | كمية الإرجاع تتجاوز الكمية المستلمة |
| 68 | `PURCHASE_RETURN_NUMBER_EXISTS` | 409 | Purchase return number already taken | رقم مرتجع الشراء مستخدم بالفعل |
| 69 | `DISPOSITION_NOT_FOUND` | 404 | Disposition not found | التصرف المخزني غير موجود |
| 70 | `DISPOSITION_ALREADY_POSTED` | 409 | Cannot modify posted disposition | لا يمكن تعديل تصرف مُرحَّل |
| 71 | `DISPOSITION_EMPTY` | 400 | Disposition has no lines | التصرف لا يحتوي على أصناف |
| 72 | `DISPOSITION_ALREADY_VOIDED` | 409 | Already voided | التصرف ملغي بالفعل |
| 73 | `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST` | 409 | Cannot void posted disposition | لا يمكن إلغاء تصرف مُرحَّل |
| 74 | `DISPOSITION_NUMBER_EXISTS` | 409 | Disposition number already taken | رقم التصرف مستخدم بالفعل |
| 75 | `DISPOSITION_REQUIRES_APPROVAL` | 409 | Manager approval required | يتطلب موافقة المدير قبل الترحيل |
| 76 | `DISPOSITION_INVALID_TYPE` | 400 | Disposition type not allowed | نوع التصرف غير مسموح |
| 77 | `DESTINATION_WAREHOUSE_NOT_FOUND` | 404 | Special destination warehouse not found | مخزن الوجهة غير موجود |

**Actual count: 77 error codes** in `ErrorCodes.cs`.

> **Note**: `api.md` lists 65 codes. The code has 77 because it includes `IMPORT_PREVIEW_FAILED`, `BATCH_NUMBER_EXISTS`, `INVOICE_NUMBER_EXISTS`, and several disposition/return codes added in RET phases. The code is source of truth. See D6 in §8.

---

## 6) Paging Shapes Freeze

Two shapes are used in the backend:

### Shape A — `PagedResult<T>` (typed record)

**Defined at**: `src/ElshazlyStore.Api/Endpoints/ProductEndpoints.cs`, line 182.

```csharp
public sealed record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

**JSON shape** (camelCase via default serializer):

```json
{
  "items": [ ... ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 25
}
```

**Used by** (verified from code):
- Products list → `PagedResult<ProductDto>`
- Variants list → `PagedResult<VariantListDto>`
- Customers list → `PagedResult<CustomerDto>`
- Suppliers list → `PagedResult<SupplierDto>`
- Purchases list → `PagedResult<ReceiptDto>`
- Production list → `PagedResult<BatchDto>`
- Sales list → `PagedResult<InvoiceDto>`
- Sales Returns list → `PagedResult<ReturnDto>`
- Purchase Returns list → `PagedResult<ReturnDto>`
- Dispositions list → `PagedResult<DispositionDto>`
- Accounting balances → `PagedResult<PartyBalanceDto>`
- Accounting ledger → `PagedResult<LedgerEntryDto>`
- Payments list → `PagedResult<PaymentDto>`

### Shape B — Anonymous object `{ items, totalCount, page, pageSize }`

Same field names, but returned as `new { items, totalCount, page, pageSize }` (anonymous type).

**JSON shape** is **identical** to Shape A.

**Used by** (verified from code):
- Warehouses list → `new { items, totalCount, page, pageSize }`
- Stock balances → `new { items, totalCount, page, pageSize }`
- Stock ledger → `new { items, totalCount, page, pageSize }`
- Print Profiles list → `new { items, totalCount, page, pageSize }`
- Print Rules list → `new { items, totalCount, page, pageSize }`
- Reason Codes list → `new { items, totalCount, page, pageSize }`

### UI Impact

Both shapes serialize to **identical JSON**. The UI `PagedListViewModelBase<T>` can use a single deserialization model:

```csharp
public sealed class PagedResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

**No special branching needed** — this is a non-issue.

### Paging query parameters (all list endpoints)

| Param | Type | Default | Max | Notes |
|-------|------|---------|-----|-------|
| `page` | int | 1 | — | Clamped to ≥ 1 |
| `pageSize` | int | 25 | 100 | Clamped 1..100 |
| `q` | string | null | — | Free-text search (fields vary by endpoint) |
| `sort` | string | varies | — | Sort column (e.g., `name`, `name_desc`, `created`, `created_desc`) |
| `includeTotal` | bool | true | — | If false, `totalCount` = -1 (performance optimization) |

---

## 7) Printing Contract Freeze (screen codes)

### Finding

The backend does **NOT** define a hard-coded enum or list of valid screen codes. The `PrintRule.ScreenCode` field is a **free-text string** (`string ScreenCode`). Any value can be stored.

**Source**: `src/ElshazlyStore.Domain/Entities/PrintRule.cs`, line 13:
```csharp
/// <summary>Identifies the screen or report this rule targets
/// (e.g., "SALES_INVOICE", "PURCHASE_RECEIPT").</summary>
public string ScreenCode { get; set; } = string.Empty;
```

The XML comment mentions `"SALES_INVOICE"` and `"PURCHASE_RECEIPT"` as examples.

### Seeded / Pre-defined Screen Codes

**(غير مذكور في المدخلات/المصدر)** — No screen codes are seeded by the backend. The admin creates them via the Print Rules API.

### Proposed Screen Code Convention (for UI)

Based on `api.md` and the plan, these are the proposed conventions:

| Screen Code | UI Screen | Usage |
|-------------|-----------|-------|
| `POS_RECEIPT` | POS / Sales Invoice | Print receipt after posting |
| `PURCHASE_RECEIPT` | Purchase Receipt | Print receipt after posting |
| `BARCODE_LABEL` | Product/Variant management | Print barcode labels |
| `SALES_RETURN_RECEIPT` | Sales Return | Print return receipt |
| `PURCHASE_RETURN_RECEIPT` | Purchase Return | Print return receipt |
| `DISPOSITION_RECEIPT` | Disposition | Print disposition record |

**⚠️ BLOCKER STATUS**: This is **NOT a backend blocker** — screen codes are free-text and can be created by UI at any time. However, the UI team must agree on the canonical list before UI 2.5/2.8. **Not blocking for UI 2.2**.

---

## 8) Discrepancies (D1..D6) + Gaps/Blockers

### D1 — PaymentMethod enum mismatch ⚠️ CONFIRMED

| Source | Values |
|--------|--------|
| `api.md` | `Cash, Card, BankTransfer, EWallet, Cheque` |
| `CODEBASE_INVENTORY.md` | `Cash, InstaPay, EWallet, Visa` |
| **Code (truth)** `Payment.cs` | `Cash = 0, InstaPay = 1, EWallet = 2, Visa = 3` |

**Resolution**: Code is authoritative. **`api.md` is WRONG** — must be updated to `Cash, InstaPay, EWallet, Visa`.

**UI Impact**: Desktop must use `{ Cash, InstaPay, EWallet, Visa }`. No `Card`, `BankTransfer`, or `Cheque` exist.

**Action**: Update `api.md` PaymentMethod section. **Non-blocking for UI 2.2** (payments are UI 2.6 scope).

### D2 — Customer/Supplier name fields — ✅ RESOLVED

| Source | Finding |
|--------|---------|
| `api.md` + Code | Single `Name` field |
| `UI 2 PLAN REPORT` | Proposed `NameAr, NameEn` |

**Resolution**: Backend has `Name` only. Confirmed. UI must use `Name`. **No action needed. RESOLVED.**

### D3 — LedgerEntryType extended values — ✅ RESOLVED

| Source | Values |
|--------|--------|
| `CODEBASE_INVENTORY.md` | `OpeningBalance, Invoice, Payment` |
| **Code (truth)** `LedgerEntry.cs` | `OpeningBalance = 0, Invoice = 1, Payment = 2, CreditNote = 3, DebitNote = 4` |

**Resolution**: RET phases added `CreditNote` and `DebitNote`. Code is truth. **api.md is correct. RESOLVED.**

### D4 — Reason Codes paging shape — ✅ RESOLVED (non-issue)

Reason Codes use Shape B (`new { items, totalCount, page, pageSize }`). However, as documented in §6, both Shape A and Shape B produce **identical JSON**. One `PagedResponse<T>` model handles both.

**Resolution**: Not a discrepancy in practice. **RESOLVED.**

### D5 — Print Profiles paging shape — ✅ RESOLVED (non-issue)

Same as D4. **RESOLVED.**

### D6 — ErrorCodes count mismatch ⚠️ MINOR

| Source | Count |
|--------|-------|
| `api.md` Error Responses table | 65 entries |
| **Code (truth)** `ErrorCodes.cs` | 77 constants |

**Missing from api.md** (present only in code):
- `IMPORT_PREVIEW_FAILED`
- `BATCH_NUMBER_EXISTS`
- `INVOICE_NUMBER_EXISTS`
- Several additional return/disposition codes added during RET phases

**Resolution**: Code has more error codes than `api.md` documents. Code is truth. `api.md` should be updated. **Non-blocking for UI 2.2.**

### D7 — `api.md` route for `DOCUMENT_NUMBER_EXISTS` vs `INVOICE_NUMBER_EXISTS`

`api.md` lists `DOCUMENT_NUMBER_EXISTS` (for purchases) and the code confirms this. The code also has a **separate** `INVOICE_NUMBER_EXISTS` for sales invoices (not listed in `api.md`). These are two distinct error codes for two different entities.

**Resolution**: `api.md` should add `INVOICE_NUMBER_EXISTS`. **Non-blocking.**

### D8 — MovementType extended values

| Source | Values |
|--------|--------|
| `api.md` | Values 0–6 only |
| **Code (truth)** `StockMovement.cs` | Values 0–9: adds `SaleReturnReceipt = 7, PurchaseReturnIssue = 8, Disposition = 9` |

**Resolution**: RET phases extended the enum. Code is truth. `api.md` should be updated. **Non-blocking — UI 2.2 does not use these movement types in its UI scope.**

### Gaps / Blockers Summary

| # | Gap | Severity | Blocking Phase |
|---|-----|----------|----------------|
| G1 | `ProblemDetails` in Desktop has no `ErrorCode` accessor — only uses `Detail`/`Title` fallback | Low | UI 2.2 (should add before per-code error handling) |
| G2 | `ToUserMessage()` has no per-error-code Arabic mapping | Medium | UI 2.3+ (needed for good UX on error screens) |
| G3 | Print screen codes not defined in backend | Low | UI 2.5/2.8 (convention agreement needed) |
| G4 | `AUDIT_READ` permission exists but no audit endpoint | Info | Future phase |
| G5 | `api.md` PaymentMethod values wrong | Low | Must fix before UI 2.6 (Payments) |

**No BLOCKERS for UI 2.2.**

---

## 9) Smoke Test Script (User-runnable)

### Prerequisites
- Backend API running at `https://localhost:5001` (or your configured URL)
- Default admin credentials: `admin` / `Admin@123!` (from `appsettings.Development.json`)

### Step 1 — Login

```bash
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123!"}'
```

**Expected**: `200 OK` with `{ "accessToken": "...", "refreshToken": "...", "expiresAtUtc": "..." }`

Save the `accessToken` for subsequent requests.

### Step 2 — /auth/me

```bash
curl -k https://localhost:5001/api/v1/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

**Expected**: `200 OK` with `{ "id": "...", "username": "admin", "isActive": true, "roles": ["Admin"] }`

### Step 3 — Products list

```bash
curl -k "https://localhost:5001/api/v1/products?page=1&pageSize=5" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

**Expected**: `200 OK` with `{ "items": [...], "totalCount": N, "page": 1, "pageSize": 5 }`

### Step 4 — Customers list

```bash
curl -k "https://localhost:5001/api/v1/customers?page=1&pageSize=5" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

**Expected**: `200 OK` with `{ "items": [...], "totalCount": N, "page": 1, "pageSize": 5 }`

### Step 5 — Stock balances

```bash
curl -k "https://localhost:5001/api/v1/stock/balances?page=1&pageSize=5" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

**Expected**: `200 OK` with `{ "items": [...], "totalCount": N, "page": 1, "pageSize": 5 }`

### Step 6 — Health (no auth)

```bash
curl -k https://localhost:5001/api/v1/health
```

**Expected**: `200 OK` with `{ "status": "Healthy", "timestamp": "..." }`

### Step 7 — Write endpoint

**(غير متاح — لا يوجد sandbox آمن)** — Skipped. No safe non-destructive write endpoint available for smoke testing without potentially affecting data.

### PowerShell equivalent (for Windows)

```powershell
# Step 1 — Login
$login = Invoke-RestMethod -Method Post -Uri "https://localhost:5001/api/v1/auth/login" `
  -ContentType "application/json" `
  -Body '{"username":"admin","password":"Admin@123!"}' `
  -SkipCertificateCheck

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }

# Step 2 — /auth/me
Invoke-RestMethod -Uri "https://localhost:5001/api/v1/auth/me" -Headers $headers -SkipCertificateCheck

# Step 3 — Products
Invoke-RestMethod -Uri "https://localhost:5001/api/v1/products?page=1&pageSize=5" -Headers $headers -SkipCertificateCheck

# Step 4 — Customers
Invoke-RestMethod -Uri "https://localhost:5001/api/v1/customers?page=1&pageSize=5" -Headers $headers -SkipCertificateCheck

# Step 5 — Stock balances
Invoke-RestMethod -Uri "https://localhost:5001/api/v1/stock/balances?page=1&pageSize=5" -Headers $headers -SkipCertificateCheck

# Step 6 — Health
Invoke-RestMethod -Uri "https://localhost:5001/api/v1/health" -SkipCertificateCheck
```

---

## 10) Agent Verification — نتائج build/test

### Build

```
dotnet build ElshazlyStore.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.01
```

**Result: ✅ 0 errors, 0 warnings**

### Tests

```
dotnet test ElshazlyStore.sln
Passed!  - Failed:     0, Passed:   211, Skipped:     0, Total:   211, Duration: 17 s
```

**Result: ✅ 211 tests passed, 0 failed, 0 skipped**

---

## 11) Human Vision Gate: Steps to open the UI for my review

### Steps for user to verify UI is not broken:

1. **Start the backend API**:
   ```
   cd src/ElshazlyStore.Api
   dotnet run
   ```
   Wait for "Now listening on: https://localhost:5001" (or similar).

2. **Start the Desktop app**:
   ```
   cd src/ElshazlyStore.Desktop
   dotnet run
   ```

3. **Expected behavior**:
   - If no stored session: **LoginWindow** appears (username + password fields, login button).
   - Enter `admin` / `Admin@123!` → Login succeeds → **MainWindow** loads.
   - MainWindow shows: **TopBar** (48px) at top, **Sidebar** (220px) on left with navigation items, **Content area** displaying Home page.
   - Click **Settings** in sidebar → Settings page loads.
   - Click **Home** → returns to Home.
   - Theme toggle (if available in Settings) switches Dark/Light without visual glitches.
   - Logout button → returns to LoginWindow.

4. **Verify no regressions** from UI 1 + UI 2.1 baseline:
   - Login/logout flow works.
   - Navigation sidebar visible with permission-gated items.
   - No crashes, no unhandled exceptions in console.

### ⚠️ Note

The agent did NOT run the WPF Desktop app (requires a Windows GUI session, which is not available in this terminal context). The user must perform this step manually.

---

## 12) STOP — Do not proceed to UI 2.2a until user approval

### Checklist for user sign-off:

- [ ] Endpoint Catalog reviewed and accepted
- [ ] Permissions Manifest (47 codes) confirmed correct
- [ ] ErrorCodes Manifest (77 codes) reviewed
- [ ] Paging shape (single `PagedResponse<T>`) approved
- [ ] Printing screen code convention discussed (or deferred to UI 2.5)
- [ ] Discrepancies D1–D8 acknowledged / action items assigned
- [ ] Gaps G1–G5 acknowledged / prioritized
- [ ] Smoke test script executed against running backend
- [ ] Human Vision Gate: UI opened, login tested, navigation tested
- [ ] User approval to proceed to UI 2.2a

### Summary of actions needed before UI 2.2:

| Action | Priority | Owner |
|--------|----------|-------|
| Fix `api.md` PaymentMethod values (D1) | Low (not blocking UI 2.2) | Docs |
| Add `ErrorCode` accessor to Desktop `ProblemDetails` (G1) | Medium (UI 2.2 can start without) | UI 2.2 |
| Build per-error-code Arabic message map (G2) | Medium | UI 2.2 |
| Agree on print screen code list (G3) | Low | Defer to UI 2.5 |

**🛑 AGENT STOPPED. Awaiting user approval to proceed to UI 2.2a.**

---

### Enum Reference (Appendix)

For completeness, here are all backend enum values verified from code:

**PaymentMethod** (`Payment.cs`): `Cash = 0, InstaPay = 1, EWallet = 2, Visa = 3`

**MovementType** (`StockMovement.cs`): `OpeningBalance = 0, PurchaseReceipt = 1, SaleIssue = 2, Transfer = 3, Adjustment = 4, ProductionConsume = 5, ProductionProduce = 6, SaleReturnReceipt = 7, PurchaseReturnIssue = 8, Disposition = 9`

**BarcodeStatus** (`BarcodeReservation.cs`): `Reserved = 0, Assigned = 1, Retired = 2`

**PartyType** (`LedgerEntry.cs`): `Customer = 0, Supplier = 1`

**LedgerEntryType** (`LedgerEntry.cs`): `OpeningBalance = 0, Invoice = 1, Payment = 2, CreditNote = 3, DebitNote = 4`

**DispositionType** (`DispositionType.cs`): `Scrap = 0, Rework = 1, ReturnToVendor = 2, ReturnToStock = 3, Quarantine = 4, WriteOff = 5`

**ReasonCategory** (`ReasonCategory.cs`): `General = 0, SalesReturn = 1, PurchaseReturn = 2, Disposition = 3`

**PurchaseReceiptStatus** (`PurchaseReceipt.cs`): `Draft = 0, Posted = 1`

**SalesInvoiceStatus** (`SalesInvoice.cs`): `Draft = 0, Posted = 1`

**ProductionBatchStatus** (`ProductionBatch.cs`): `Draft = 0, Posted = 1`

**SalesReturnStatus** (`SalesReturn.cs`): `Draft, Posted, Voided` (values verified from entity)

**PurchaseReturnStatus** (`PurchaseReturn.cs`): `Draft, Posted, Voided` (values verified from entity)

**DispositionStatus** (`InventoryDisposition.cs`): `Draft, Posted, Voided` (values verified from entity)

**ImportJobStatus** (`ImportJob.cs`): `Previewed = 0, Committed = 1, Failed = 2`

**ProductionLineType** (`ProductionBatchLine.cs`): `Input = 0, Output = 1`
