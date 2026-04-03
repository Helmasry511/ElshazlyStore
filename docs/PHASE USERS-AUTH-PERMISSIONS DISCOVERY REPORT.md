# PHASE USERS-AUTH-PERMISSIONS DISCOVERY REPORT

**Date:** 2026-04-02  
**Scope:** Users Management + Login/Auth Improvement + Fine-Grained Permissions + UI/API Access Enforcement  
**Project:** ElshazlyStore ERP/POS Desktop + API  
**Status:** Read-only discovery — no code changes were made

---

## 1. Current Reality in Codebase

### 1.1 What Currently Exists for Login/Auth

**Confirmed and implemented:**

- `POST /api/v1/auth/login` — accepts `{username, password}`, validates against `users` table, checks `IsActive`, builds JWT with embedded `permission` claims, issues a refresh token.
- `POST /api/v1/auth/refresh` — rotates refresh token (revokes old, issues new), re-builds JWT with new permission snapshot.
- `POST /api/v1/auth/logout` — revokes the provided refresh token (sets `RevokedAtUtc`).
- `GET /api/v1/auth/me` — returns `{id, username, isActive, roles[]}`. Does **not** return permissions list; permissions come from JWT claims only.
- JWT: HMAC-SHA256, 15-minute access token (configured in `JwtSettings`), 7-day refresh token.
- Refresh token stored hashed (HMAC-SHA256) in `refresh_tokens` table with rotation chain (`ReplacedByTokenHash`).
- Password hashing: ASP.NET Core Identity `PasswordHasher` (PBKDF2).
- Desktop `SessionService`: calls `/auth/login`, stores tokens in `SecureTokenStore` (DPAPI-encrypted per-Windows-user), parses permissions from JWT payload via `JwtClaimParser` (manual Base64 decode — no library dependency).
- Desktop `TokenRefreshHandler`: intercepts 401 responses, calls `AuthRefresh` HttpClient (no auth loop), fires `SessionExpired` event on failure.
- Desktop: silent session restore on startup via `TryRestoreSessionAsync` using stored DPAPI token.

**Not implemented (confirmed absent):**

- No last login timestamp recorded anywhere.
- No force-password-change flag or flow.
- No first-login detection.
- No login attempt audit entry in `audit_logs` (login action is not written; only EF entity changes are audited).
- No password history tracking.
- No dedicated reset-password endpoint.

---

### 1.2 What Currently Exists for Users

**Domain entity `User` (confirmed fields):**
```
Id              Guid (PK)
Username        string (max 100, unique index)
PasswordHash    string (max 512)
IsActive        bool (default true)
CreatedAtUtc    DateTime
```
Navigation: `UserRoles`, `RefreshTokens`

**API endpoints (`UserEndpoints.cs`):**
- `GET /api/v1/users` → requires `USERS_READ`
- `GET /api/v1/users/{id}` → requires `USERS_READ`
- `POST /api/v1/users` → requires `USERS_WRITE` — creates user, assigns roles
- `PUT /api/v1/users/{id}` → requires `USERS_WRITE` — updates username, password, isActive, roles
- `DELETE /api/v1/users/{id}` → requires `USERS_WRITE` — **sets `IsActive = false` (not hard delete, not soft delete with flag)**

**Admin seeder:** Seeds one `admin` user at startup if it does not exist. Password from `ADMIN_DEFAULT_PASSWORD` env var (default `Admin@123!`).

**Desktop UI:** No `UsersViewModel` or `UsersPage` exists. No navigation case for Users in `MainViewModel.NavigateTo`. `CanViewUsers` flag exists in `MainViewModel` and drives sidebar visibility, but there is no destination screen yet — navigating there falls through to `HomeViewModel` ("page not yet implemented" log message).

**Confirmed absent on User entity:**
- `DisplayNameArabic` / `FullNameArabic` — غير موجود حالياً
- `LastLoginAtUtc` — غير موجود حالياً
- `ForcePasswordChangeOnFirstLogin` — غير موجود حالياً
- `IsDeleted` (soft delete) / `DeletedAtUtc` — غير موجود حالياً
- `CreatedByUserId` on User itself — غير موجود حالياً
- `UpdatedAtUtc` / `UpdatedByUserId` — غير موجود حالياً
- Password history table — غير موجود حالياً

---

### 1.3 What Currently Exists for Roles/Permissions

**Confirmed entities:**
- `Role`: Id, Name (unique), Description, CreatedAtUtc
- `Permission`: Id, Code (unique string), Description
- `RolePermission`: composite key (RoleId, PermissionId) — many-to-many join
- `UserRole`: composite key (UserId, RoleId) — many-to-many join

**Confirmed permission codes (in `Permissions.cs` and `PermissionCodes.cs`):**

| Code | Description |
|---|---|
| USERS_READ | View users |
| USERS_WRITE | Create/update/deactivate users |
| ROLES_READ | View roles and permissions |
| ROLES_WRITE | Create/update/delete roles; manage permissions |
| AUDIT_READ | View audit logs |
| PRODUCTS_READ | View products and variants |
| PRODUCTS_WRITE | Create/update/delete products |
| CUSTOMERS_READ | View customers |
| CUSTOMERS_WRITE | Create/update/delete customers |
| SUPPLIERS_READ | View suppliers |
| SUPPLIERS_WRITE | Create/update/delete suppliers |
| IMPORT_MASTER_DATA | Import from Excel/CSV |
| STOCK_READ | View stock balances and ledger |
| STOCK_POST | Post stock movements |
| WAREHOUSES_READ | View warehouses |
| WAREHOUSES_WRITE | Create/update/delete warehouses |
| PURCHASES_READ | View purchase receipts |
| PURCHASES_WRITE | Create/update/delete purchase receipts |
| PURCHASES_POST | Post purchase receipts |
| PRODUCTION_READ | View production batches |
| PRODUCTION_WRITE | Create/update/delete production batches |
| PRODUCTION_POST | Post production batches |
| SALES_READ | View sales invoices |
| SALES_WRITE | Create/update/delete sales invoices |
| SALES_POST | Post sales invoices |
| ACCOUNTING_READ | View AR/AP ledger |
| PAYMENTS_READ | View payments |
| PAYMENTS_WRITE | Create payments |
| IMPORT_OPENING_BALANCES | Import opening balances |
| IMPORT_PAYMENTS | Import payments |
| DASHBOARD_READ | View dashboard KPIs |
| MANAGE_PRINTING_POLICY | Manage print profiles and rules |
| MANAGE_REASON_CODES | Create/update/disable reason codes |
| VIEW_REASON_CODES | View reason codes catalog |
| SALES_RETURN_CREATE | Create/update draft sales returns |
| SALES_RETURN_POST | Post sales returns |
| SALES_RETURN_VOID | Void posted sales returns |
| VIEW_SALES_RETURNS | View sales returns |
| PURCHASE_RETURN_CREATE | Create/update draft purchase returns |
| PURCHASE_RETURN_POST | Post purchase returns |
| PURCHASE_RETURN_VOID | Void posted purchase returns |
| VIEW_PURCHASE_RETURNS | View purchase returns |
| DISPOSITION_CREATE | Create dispositions |
| DISPOSITION_POST | Post dispositions |
| DISPOSITION_APPROVE | Approve dispositions |
| DISPOSITION_VOID | Void dispositions |
| VIEW_DISPOSITIONS | View dispositions |

**API for roles (`RoleEndpoints.cs`):**
- Full CRUD on roles
- `GET /roles/{id}/permissions`, `PUT /roles/{id}/permissions` (replace-all model)
- `GET /roles/permissions/all` — lists all defined permission codes
All gated by `ROLES_READ` / `ROLES_WRITE`.

**Desktop UI:** No `RolesViewModel` or `RolesPage` exists. `CanViewRoles` exists in `MainViewModel` but there is no destination screen — same fall-through to Home.

**Confirmed absent:**
- User-specific permission overrides — غير موجود حالياً (architecture is role → permission only; no per-user grant/deny)
- Permission hierarchy (section → screen → action structure) — غير موجود حالياً (flat string codes only)
- Any concept of "Manager" vs "Admin" at the data model level — غير موجود حالياً (roles are free-form names)

---

### 1.4 What Currently Exists for Menu/Screen Access Control

**Confirmed in `MainViewModel.RefreshUserState()`:**
- 20+ boolean observable properties (e.g., `CanViewDashboard`, `CanViewProducts`, `CanViewSales`, `CanViewUsers`, etc.) are set from `IPermissionService.HasPermission()` at login/startup.
- Sidebar sections and items in `MainWindow.xaml` bind their `Visibility` to these properties via `BoolToVisibilityConverter`.
- Entire sections (Commerce, Inventory, Sales, Accounting, Admin) collapse when all child items are hidden.
- `RefreshUserState()` is called once on startup in `MainViewModel` constructor. There is no mechanism to refresh permissions mid-session without a full logout/login.

**Gap:** Permissions are baked into sidebar at login time. If an admin changes another user's role, that user's running session does not reflect the change until next login.

---

### 1.5 What Currently Exists for Button-Level Access Control

**Partially implemented. Pattern confirmed in:**

| ViewModel | Permission Flags |
|---|---|
| `PurchasesViewModel` | `CanWrite` (PURCHASES_WRITE), `CanPost` (PURCHASES_POST) |
| `SalesViewModel` | `CanPost` (SALES_POST), `CanCreateCustomers` (CUSTOMERS_WRITE) |
| `POSViewModel` | `CanCreateCustomers` (CUSTOMERS_WRITE) |
| `SalesReturnsViewModel` | `CanCreate` (SALES_RETURN_CREATE), `CanPost` (SALES_RETURN_POST) |
| `PurchaseReturnsViewModel` | `CanCreate` (PURCHASE_RETURN_CREATE), `CanPost` (PURCHASE_RETURN_POST), `CanVoid` (PURCHASE_RETURN_VOID) |
| `CustomersViewModel` | `CanWrite` (CUSTOMERS_WRITE), `CanViewPayments` (PAYMENTS_READ) |

**Pattern:** Each ViewModel initializes its permission flags from `IPermissionService` in the constructor. XAML buttons/controls bind `Visibility` to these flags via `BoolToVisibilityConverter`.

**Screens with NO button-level permission flags (confirmed only section-level hiding):**
- `ProductsViewModel`, `VariantsViewModel` — no CanWrite flag, write actions not separately guarded
- `SuppliersViewModel`, `WarehousesViewModel` — غير محسوم (need to verify)
- `StockBalancesViewModel`, `StockLedgerViewModel`, `StockMovementsViewModel` — غير محسوم
- `ReasonCodesViewModel`, `SupplierPaymentsViewModel`, `CustomerPaymentsViewModel` — غير محسوم

The button-level pattern is **inconsistent** — applied to some modules but not all.

---

### 1.6 What Currently Exists for Audit / Last Login / Password Reset / First-Login Password Change

**Audit logging — partially implemented:**
- `AuditInterceptor` (EF Core `SaveChangesInterceptor`) captures INSERT/UPDATE/DELETE for all business entities automatically.
- Excluded from audit: `AuditLog` itself and `RefreshToken`.
- Sensitive fields excluded from serialization: `PasswordHash`, `TokenHash`, `ReplacedByTokenHash`, `FileContent`.
- `AuditLog` entity has: `TimestampUtc`, `UserId`, `Username`, `IpAddress`, `UserAgent`, `CorrelationId`, `Action`, `Module`, `EntityName`, `PrimaryKey`, `OldValues` (jsonb), `NewValues` (jsonb).
- Indexed on `TimestampUtc`, `UserId`, `EntityName`.

**Audit logging — confirmed absent:**
- Login events are NOT written to `audit_logs` (only DB entity changes are captured).
- Logout events are NOT written.
- Permission change events are NOT written as explicit audit entries (only the underlying `role_permissions` table changes are captured generically).
- `AUDIT_READ` permission code exists in `Permissions.cs` but there is **no `/api/v1/audit` endpoint** in the project. The permission code is defined but has no corresponding API surface.
- No desktop screen for viewing audit logs.

**Last login — غير موجود حالياً.** No `LastLoginAtUtc` field on `User`. Login endpoint does not update any timestamp.

**Password reset — غير موجود حالياً as a dedicated flow.** Admin can change a user's password via `PUT /api/v1/users/{id}` with `{password: "..."}`, but there is no dedicated reset endpoint, no notification, and no forced next-login password change.

**Force password change on first login — غير موجود حالياً.** No field, no endpoint behavior, no desktop flow.

---

## 2. Gap Analysis vs Required Phase

### 2.1 Fully Missing

| Requirement | Status |
|---|---|
| Arabic display name / title for user in system | غير موجود حالياً |
| Last login tracking (`LastLoginAtUtc`) | غير موجود حالياً |
| Force password change on first login | غير موجود حالياً |
| Dedicated reset-password endpoint (Admin only) | غير موجود حالياً |
| User-specific permission overrides (per-user grant/deny beyond role) | غير موجود حالياً |
| Permission hierarchy: Section → Screen → Action | غير موجود حالياً |
| Permission assignment UI (desktop screen) | غير موجود حالياً |
| Users management UI (desktop screen — UsersPage/ViewModel) | غير موجود حالياً |
| Roles management UI (desktop screen — RolesPage/ViewModel) | غير موجود حالياً |
| Login event audit entry | غير موجود حالياً |
| Audit log viewer endpoint + desktop screen | غير موجود حالياً |
| Soft delete / `IsDeleted` on users | غير موجود حالياً |
| `CreatedByUserId` / `UpdatedByUserId` / `UpdatedAtUtc` on User | غير موجود حالياً |
| Stale-permission refresh without logout | غير موجود حالياً |
| Manager vs Admin distinction at model level | غير موجود حالياً |
| First-login password change UI flow (desktop) | غير موجود حالياً |

---

### 2.2 Partially Implemented

| Requirement | What Exists | What's Missing |
|---|---|---|
| Button-level access control (UI hiding) | Implemented in Purchases, Sales, SalesReturns, PurchaseReturns, Customers | Not implemented in Products, Variants, Suppliers, Warehouses, Stock, Production modules |
| Audit trailing | DB changes audited via AuditInterceptor | Login/logout not audited; no API/UI to read audit |
| User activation/deactivation | `IsActive = false` on "delete" endpoint | No dedicated activate endpoint; no "delete" vs "deactivate" distinction |

---

### 2.3 Existing but Insufficient

| Item | Issue |
|---|---|
| Permission model | Flat string codes only; no hierarchy. The new phase needs Section → Screen → Action metadata for the permission assignment UI. |
| `USERS_WRITE` permission | Controls all user operations (create, update, deactivate). Cannot distinguish Manager (can create) from Admin (can activate/deactivate/delete/reset). |
| Role-only permissions | No per-user permission override. New phase requires user-specific overrides. |
| `/auth/me` response | Returns `roles[]` but no `permissions[]`. Desktop parses permissions from JWT. No server-provided permissions endpoint for client to re-check without re-parsing JWT. |
| Sidebar visibility refresh | One-time at session start. No mechanism to invalidate the client-side permission cache mid-session. |

---

### 2.4 Existing and Reusable

| Item | Assessment |
|---|---|
| JWT + Refresh token infrastructure | Fully reusable. Token contains `permission` claims already. |
| `PermissionAuthorizationHandler` + `PermissionPolicyProvider` | Fully reusable. Any new permission code is automatically supported. |
| `Permissions.cs` static constants + `PermissionCodes.cs` | Reusable; needs extension for new granular codes. |
| `IPermissionService` + `PermissionService` (desktop) | Reusable as-is for new permission check calls. |
| `MainViewModel` boolean flag + BoolToVisibility pattern | Reusable pattern for all new screen-level hiding. |
| VM constructor permission flag initialization pattern | Reusable pattern (`CanCreate`, `CanPost`, `CanVoid` etc.). |
| `AuditInterceptor` + `AuditLog` entity | Reusable for structural audit. Needs login audit layered on top separately. |
| `AdminSeeder` | Reusable; add Manager role seeding here. |
| `AppPasswordHasher` (PBKDF2) | Fully reusable for new password flows. |
| `SecureTokenStore` (DPAPI) | Reusable. |
| `PagedListViewModelBase` | Reusable for Users/Roles pages. |

---

## 3. Screen & Module Inventory

All major sections and screens that should participate in permissions, confirmed from `MainViewModel.NavigateTo`, `MainWindow.xaml` DataTemplates, `ViewModels/`, and API `EndpointMapper`.

### Section: Main
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| Home | `HomeViewModel` | View (always accessible) |
| Dashboard | غير موجود حالياً — no ViewModel; API endpoint exists | View |

### Section: Commerce
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| Products | `ProductsViewModel` | View, Create, Edit, Delete |
| Variants (SKUs) | `VariantsViewModel` | View, Create, Edit, Delete |
| Customers | `CustomersViewModel` | View, Create, Edit, Activate, Deactivate, UploadAttachment, DeleteAttachment, OpenFolder |
| Suppliers | `SuppliersViewModel` | View, Create, Edit, Delete |

### Section: Inventory
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| Warehouses | `WarehousesViewModel` | View, Create, Edit, Delete |
| Stock Balances | `StockBalancesViewModel` | View, Export (غير محسوم) |
| Stock Ledger | `StockLedgerViewModel` | View |
| Stock Movements | `StockMovementsViewModel` | View, Create (Post) |
| Purchases | `PurchasesViewModel` | View, Create, Edit, Delete, Post |
| Production | غير موجود حالياً — no ViewModel or Page in desktop; API exists | View, Create, Edit, Delete, Post |

### Section: Sales
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| Sales (Invoices) | `SalesViewModel` | View, Create, Edit, Delete, Post, Print |
| POS | `POSViewModel` | View, Create (Post is implicit), Print |
| Sales Returns | `SalesReturnsViewModel` | View, Create, Post, Void |
| Purchase Returns | `PurchaseReturnsViewModel` | View, Create, Post, Void |
| Dispositions | غير موجود حالياً — no ViewModel/Page in desktop; API exists | View, Create, Post, Approve, Void |

### Section: Accounting
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| AR/AP Balances | `StockLedgerViewModel` is re-used? غير محسوم | View |
| Supplier Payments | `SupplierPaymentsViewModel` | View, Create, Print |
| Customer Payments | `CustomerPaymentsViewModel` | View, Create, Print |

### Section: Admin
| Screen | Confirmed ViewModel | Likely Actions |
|---|---|---|
| Users | غير موجود حالياً — no ViewModel/Page | View, Create, Edit, Activate, Deactivate, Delete, ResetPassword |
| Roles | غير موجود حالياً — no ViewModel/Page | View, Create, Edit, Delete |
| Permission Assignment | غير موجود حالياً | View, Grant (all), Revoke (all), individual toggle |
| Import | API: ImportEndpoints | View, Import |
| Reason Codes | `ReasonCodesViewModel` | View, Create, Edit, Activate, Deactivate |
| Print Config | غير موجود حالياً as separate page; API exists | View, Create, Edit, Delete |
| Audit Log | غير موجود حالياً | View |

---

## 4. Proposed Permission Skeleton

**Legend:** `[confirmed]` = action confirmed from existing code or API. `[inferred]` = action inferred from screen logic or domain expectation.

### SECTION: USERS (إدارة المستخدمين)
```
USERS
├── Screen: Users List & Detail
│   ├── USERS_VIEW            [confirmed — USERS_READ]
│   ├── USERS_CREATE          [inferred — split from USERS_WRITE]
│   ├── USERS_EDIT            [inferred — split from USERS_WRITE]
│   ├── USERS_ACTIVATE        [inferred — admin only]
│   ├── USERS_DEACTIVATE      [inferred — admin only]
│   ├── USERS_DELETE          [inferred — admin only, soft delete]
│   └── USERS_RESET_PASSWORD  [inferred — admin only]
└── Screen: Roles & Permissions
    ├── ROLES_VIEW            [confirmed — ROLES_READ]
    ├── ROLES_CREATE          [inferred — split from ROLES_WRITE]
    ├── ROLES_EDIT            [inferred]
    ├── ROLES_DELETE          [inferred]
    └── ROLES_MANAGE_PERMISSIONS [inferred — split from ROLES_WRITE]
```

### SECTION: COMMERCE (البضاعة والعملاء والموردون)
```
COMMERCE
├── Screen: Products
│   ├── PRODUCTS_VIEW         [confirmed — PRODUCTS_READ]
│   ├── PRODUCTS_CREATE       [confirmed from API]
│   ├── PRODUCTS_EDIT         [confirmed from API]
│   └── PRODUCTS_DELETE       [confirmed from API]
├── Screen: Variants (SKUs)
│   ├── VARIANTS_VIEW         [inferred — same as products]
│   ├── VARIANTS_CREATE       [inferred]
│   ├── VARIANTS_EDIT         [inferred]
│   └── VARIANTS_DELETE       [inferred]
├── Screen: Customers
│   ├── CUSTOMERS_VIEW        [confirmed — CUSTOMERS_READ]
│   ├── CUSTOMERS_CREATE      [confirmed — CUSTOMERS_WRITE used]
│   ├── CUSTOMERS_EDIT        [confirmed — CUSTOMERS_WRITE used]
│   ├── CUSTOMERS_ACTIVATE    [confirmed — exists in UI]
│   ├── CUSTOMERS_DEACTIVATE  [confirmed — exists in UI]
│   ├── CUSTOMERS_VIEW_PAYMENTS [confirmed — PAYMENTS_READ checked in VM]
│   ├── CUSTOMERS_UPLOAD_ATTACHMENT [confirmed — CustomerAttachmentStorage implemented]
│   ├── CUSTOMERS_DELETE_ATTACHMENT [confirmed]
│   └── CUSTOMERS_OPEN_FOLDER [confirmed]
└── Screen: Suppliers
    ├── SUPPLIERS_VIEW        [confirmed — SUPPLIERS_READ]
    ├── SUPPLIERS_CREATE      [confirmed from API]
    ├── SUPPLIERS_EDIT        [confirmed from API]
    └── SUPPLIERS_DELETE      [confirmed from API]
```

### SECTION: INVENTORY (المخزون)
```
INVENTORY
├── Screen: Warehouses
│   ├── WAREHOUSES_VIEW       [confirmed — WAREHOUSES_READ]
│   ├── WAREHOUSES_CREATE     [confirmed from API]
│   ├── WAREHOUSES_EDIT       [confirmed from API]
│   └── WAREHOUSES_DELETE     [confirmed from API]
├── Screen: Stock Balances
│   └── STOCK_VIEW            [confirmed — STOCK_READ]
├── Screen: Stock Ledger
│   └── STOCK_VIEW            [shared — STOCK_READ]
├── Screen: Stock Movements
│   ├── STOCK_VIEW            [confirmed — STOCK_READ]
│   └── STOCK_POST            [confirmed — STOCK_POST]
└── Screen: Purchases
    ├── PURCHASES_VIEW        [confirmed — PURCHASES_READ]
    ├── PURCHASES_CREATE      [confirmed — PURCHASES_WRITE]
    ├── PURCHASES_EDIT        [confirmed — PURCHASES_WRITE]
    ├── PURCHASES_DELETE      [confirmed from API]
    └── PURCHASES_POST        [confirmed — PURCHASES_POST]
```

### SECTION: PRODUCTION (الإنتاج)
```
PRODUCTION
└── Screen: Production Batches
    ├── PRODUCTION_VIEW       [confirmed — PRODUCTION_READ]
    ├── PRODUCTION_CREATE     [confirmed — PRODUCTION_WRITE]
    ├── PRODUCTION_EDIT       [confirmed — PRODUCTION_WRITE]
    ├── PRODUCTION_DELETE     [confirmed from API]
    └── PRODUCTION_POST       [confirmed — PRODUCTION_POST]
```

### SECTION: SALES (المبيعات)
```
SALES
├── Screen: Sales Invoices
│   ├── SALES_VIEW            [confirmed — SALES_READ]
│   ├── SALES_CREATE          [confirmed — SALES_WRITE]
│   ├── SALES_EDIT            [confirmed — SALES_WRITE]
│   ├── SALES_DELETE          [confirmed from API]
│   ├── SALES_POST            [confirmed — SALES_POST, CanPost in SalesViewModel]
│   └── SALES_PRINT           [inferred — print pattern exists in project]
├── Screen: POS
│   ├── SALES_VIEW            [shared — SALES_READ]
│   ├── SALES_CREATE          [shared — SALES_WRITE + SALES_POST for POS]
│   └── SALES_PRINT           [inferred]
├── Screen: Sales Returns
│   ├── VIEW_SALES_RETURNS    [confirmed]
│   ├── SALES_RETURN_CREATE   [confirmed — CanCreate in VM]
│   ├── SALES_RETURN_POST     [confirmed — CanPost in VM]
│   └── SALES_RETURN_VOID     [confirmed — code exists; not yet in CanVoid flag in SalesReturnsVM]
└── Screen: Purchase Returns
    ├── VIEW_PURCHASE_RETURNS [confirmed]
    ├── PURCHASE_RETURN_CREATE [confirmed — CanCreate in VM]
    ├── PURCHASE_RETURN_POST  [confirmed — CanPost in VM]
    └── PURCHASE_RETURN_VOID  [confirmed — CanVoid in VM]
```

### SECTION: DISPOSITIONS (التصرف في المرتجعات)
```
DISPOSITIONS
└── Screen: Inventory Dispositions
    ├── VIEW_DISPOSITIONS     [confirmed — code exists]
    ├── DISPOSITION_CREATE    [confirmed — code exists]
    ├── DISPOSITION_POST      [confirmed — code exists]
    ├── DISPOSITION_APPROVE   [confirmed — code exists]
    └── DISPOSITION_VOID      [confirmed — code exists]
```

### SECTION: ACCOUNTING (الحسابات)
```
ACCOUNTING
├── Screen: AR/AP Balances
│   └── ACCOUNTING_VIEW       [confirmed — ACCOUNTING_READ]
├── Screen: Supplier Payments
│   ├── PAYMENTS_VIEW         [confirmed — PAYMENTS_READ]
│   ├── PAYMENTS_CREATE       [confirmed — PAYMENTS_WRITE]
│   └── PAYMENTS_PRINT        [inferred]
└── Screen: Customer Payments
    ├── PAYMENTS_VIEW         [shared]
    ├── PAYMENTS_CREATE       [shared]
    └── PAYMENTS_PRINT        [inferred]
```

### SECTION: ADMIN (الإدارة)
```
ADMIN
├── Screen: Import
│   ├── IMPORT_MASTER_DATA    [confirmed]
│   ├── IMPORT_OPENING_BALANCES [confirmed]
│   └── IMPORT_PAYMENTS       [confirmed]
├── Screen: Reason Codes
│   ├── VIEW_REASON_CODES     [confirmed]
│   └── MANAGE_REASON_CODES   [confirmed]
├── Screen: Print Config
│   └── MANAGE_PRINTING_POLICY [confirmed]
└── Screen: Audit Log
    └── AUDIT_VIEW            [confirmed code AUDIT_READ exists; endpoint غير موجود حالياً]
```

---

## 5. Recommended Access Model

### 5.1 User Title / Arabic Display Name

**Recommendation:** `DisplayNameArabic` (or `FullNameArabic`) should be a **plain label field** on the `User` entity — not a role template, not a permission template. It is purely UI decoration for display inside the system.

- It should appear in the top bar instead of (or alongside) the username.
- It does not affect permissions or role assignment.
- It is optional (nullable); system falls back to `Username` if not set.

**Why not a template:** Roles are already the permission-grouping mechanism. Coupling a human-readable Arabic name to a role template would create confusion and duplicate the role concept.

---

### 5.2 Primary Role / Profile

**Recommendation:** One user → one primary role (e.g., Admin, Manager, Cashier, Viewer) with optional user-specific overrides (grant or deny specific actions).

Reasoning from codebase: The current model supports `User → UserRole (many)`, meaning a user can be in multiple roles simultaneously. This **works** for an override pattern too (a user in both "Cashier" and an "Overrides" role). However, a simpler, more maintainable V1 model is:

- One primary role per user → determines the baseline permission set.
- A table `UserPermission(UserId, PermissionCode, GrantOrDeny: bool)` for per-user deviations.

Effective permission = `(role permissions) UNION (explicit user grants) MINUS (explicit user denies)`.

**This is the cleanest V1 model** given the direction to have user-specific overrides.

---

### 5.3 User-Specific Override Storage

Recommended structure (new table needed):
```sql
user_permissions (
  user_id           uuid  FK → users.id
  permission_code   text  (references permissions.code)
  is_granted        bool  (true = explicit grant; false = explicit deny)
  created_at_utc    timestamptz
  created_by_user_id uuid (who assigned it)
  PRIMARY KEY (user_id, permission_code)
)
```

The JWT generation must then compute: `effective_permissions = (role codes) UNION (granted overrides) MINUS (denied overrides)`.

---

### 5.4 UI Hiding ↔ API Enforcement Connection

The current architecture is already **dual-layer** on the sections that implement it correctly:

1. **API layer:** `RequireAuthorization("Permission:CODE")` on every endpoint — backend always enforces.
2. **UI layer:** `CanView*`, `CanPost`, `CanCreate`, `CanVoid` flags in ViewModels hide buttons/sections.

**Key rule for the new phase:** The UI hides (not just disables) unauthorized elements. The API independently enforces the same permission. The UI check is UX convenience; the API check is the security guarantee.

**Practical guideline:** For a new button to be permission-controlled:
- Add the boolean flag to the ViewModel, initialized from `IPermissionService.HasPermission("CODE")`.
- Bind XAML `Visibility` to that flag.
- Add `RequireAuthorization("Permission:CODE")` to the corresponding API endpoint.
Both layers must be done together.

---

## 6. Admin vs Manager Capabilities

### 6.1 Agreed Rules (from phase brief)

| Action | Admin / System Admin | Manager |
|---|---|---|
| Create user | ✓ | ✓ |
| Activate user | ✓ | ✗ |
| Deactivate user | ✓ | ✗ |
| Delete user (soft) | ✓ | ✗ |
| Reset password (force) | ✓ | ✗ |
| Edit own profile / change own password | ✓ | ✓ |
| Assign roles to users | غير محسوم | غير محسوم |
| View all users | ✓ | ✓ (or limited?) غير محسوم |

### 6.2 Recommended Split of Permission Codes

**Current single `USERS_WRITE` code is insufficient.** It must be split:

| New Code | Who Gets It | Description |
|---|---|---|
| `USERS_CREATE` | Admin + Manager | Create new users |
| `USERS_EDIT` | Admin + Manager | Edit user profile fields (name, Arabic title) |
| `USERS_ACTIVATE` | Admin only | Activate a deactivated user |
| `USERS_DEACTIVATE` | Admin only | Deactivate an active user |
| `USERS_DELETE` | Admin only | Soft-delete a user |
| `USERS_RESET_PASSWORD` | Admin only | Force reset another user's password |
| `USERS_ASSIGN_ROLES` | غير محسوم | Assign/change roles — decide if Manager can do this |

### 6.3 Codebase Obstacles

1. **Current `DeactivateUserAsync` is mapped to `DELETE /users/{id}`.** It does `IsActive = false`. For the new model, there must be separate `POST /users/{id}/deactivate` and `DELETE /users/{id}` (hard or soft delete) endpoints, each with its own permission code.
2. **`AdminSeeder` seeds one `Admin` role** with all permissions. The new `Manager` role must be seeded with the correct subset. The seeder must be extended.
3. **No "created by" tracking on users.** If a Manager creates a user, there is no current field to record who created them. This becomes important for an Admin to later activate/deactivate users created by a specific Manager.

---

## 7. Data Model Impact

Entities/tables/fields that are likely needed or need extension. **No implementation — report only.**

### 7.1 Extend `users` table (User entity)

| Field | Type | Purpose |
|---|---|---|
| `display_name_arabic` | `text` (nullable) | Arabic display name shown in UI header |
| `last_login_at_utc` | `timestamptz` (nullable) | Updated on each successful login |
| `force_password_change` | `bool` (default false) | Set true on admin-reset or first creation |
| `is_deleted` | `bool` (default false) | Soft-delete marker |
| `deleted_at_utc` | `timestamptz` (nullable) | When soft-deleted |
| `deleted_by_user_id` | `uuid` (nullable, FK → users) | Who deleted |
| `created_by_user_id` | `uuid` (nullable, FK → users) | Who created this user |
| `updated_at_utc` | `timestamptz` (nullable) | Last update time |
| `updated_by_user_id` | `uuid` (nullable, FK → users) | Who last updated |

### 7.2 New `user_permissions` table

```
user_permissions:
  user_id           uuid FK → users.id
  permission_code   text  (matches permissions.code)
  is_granted        bool
  created_at_utc    timestamptz
  created_by_user_id uuid FK → users.id (nullable)
  PRIMARY KEY (user_id, permission_code)
```

### 7.3 Extend `permissions` table (or add metadata table)

For the hierarchical permission assignment UI, each permission code needs:
```
  section_name     text   (e.g., "SALES")
  screen_name      text   (e.g., "SALES_INVOICES")
  action_name      text   (e.g., "POST")
  display_label    text   (e.g., "تسجيل الفاتورة")
  sort_order       int
```

This metadata can be embedded in `Permissions.cs` as a richer `All` collection, and synced to the `permissions` table via the seeder.

### 7.4 New `user_audit_log` table (or extend `audit_logs`)

For login/logout events:
```
  id                bigserial PK
  timestamp_utc     timestamptz
  user_id           uuid FK → users.id
  username          text
  event_type        text  (LOGIN_SUCCESS, LOGIN_FAILED, LOGOUT, PASSWORD_CHANGED, PASSWORD_RESET_BY_ADMIN)
  ip_address        text
  user_agent        text
  correlation_id    text
```

**Alternatively:** Re-use `audit_logs` with `EntityName = "User"`, `Action = "LOGIN"` etc. — but this mixes structural audit and activity audit. A dedicated table is cleaner.

### 7.5 `Role` entity — no structural change needed

The current `role → role_permission → permission` chain is sufficient once `user_permissions` overrides are added.

---

## 8. UI Impact

### 8.1 User Creation/Edit Screen (new `UsersPage` / `UsersViewModel`)

- New ViewModel: `UsersViewModel` (follows `PagedListViewModelBase<UserDto>` pattern).
- Fields: username, `display_name_arabic` (new), password (create only or admin-reset), role selector, `is_active` toggle (admin only).
- Admin-only controls: Activate/Deactivate buttons, Delete button, Reset Password button — hidden (Visibility Collapsed) for Manager.
- `CanCreate`, `CanActivate`, `CanDeactivate`, `CanDelete`, `CanResetPassword` flags in ViewModel.
- New `DataTemplate` in `MainWindow.xaml` for `UsersViewModel`.
- New case in `MainViewModel.NavigateTo("Users")`.

### 8.2 Permission Assignment UI

This is the most complex new screen. Requirements:
- Hierarchical tree: Section → Screen → Action.
- Grant all / Revoke all at section and screen level.
- Individual toggle per action.
- Displays combined effective permission (role baseline + user overrides), visually distinguishing source.
- Likely implemented as a nested `TreeView` or grouped `ItemsControl` in WPF, with checkboxes bound to a flat permission state collection.
- Complexity: **High** — requires loading the full permission metadata hierarchy, the user's role permissions, and the user's individual overrides, then merging them for display.

### 8.3 Login Screen Changes

- Display `display_name_arabic` in the top bar after login (minor change — `CurrentUsername` in `MainViewModel` should also surface this).
- Add "force password change" intercept: after `LoginAsync` succeeds, check if `force_password_change` is true → show a password change dialog before showing `MainWindow`.
- The `/auth/me` response or login response must return the `force_password_change` flag.

### 8.4 First-Login Password Change Flow

- After login, if `ForcePasswordChange = true`, prevent navigation to `MainWindow`.
- Show an intermediate dialog or a dedicated screen: "You must change your password before continuing."
- New password field + confirm field.
- On confirm: call new `POST /api/v1/auth/change-password` endpoint, backend clears `ForcePasswordChange`, returns new tokens.
- Desktop then proceeds to `MainWindow`.
- UX: Must not allow the user to close this dialog without changing the password.

### 8.5 Hidden Navigation / Screens / Buttons Behavior

**Current pattern is already correct for section-level:** Sidebar items are hidden (Visibility=Collapsed), not disabled. This must be maintained and extended.

**New screens must follow the same pattern:**
- Direct navigation to a hidden screen (e.g., `NavigateTo("Users")`) from the keyboard or any other path must be guarded. Currently, unauthorized navigation falls through to Home — this is acceptable behavior.
- New Users/Roles/Audit pages must still check permissions when the ViewModel loads, in case someone navigates directly.

**Dependencies:** Each new ViewModel must receive `IPermissionService` via DI (already the standard pattern).

---

## 9. API / Backend Impact

### 9.1 Login/Auth Endpoints — Changes Needed

| Change | New or Modified |
|---|---|
| `POST /auth/login` — update `last_login_at_utc` on User | Modify |
| `POST /auth/login` — write login event to `user_audit_log` (or `audit_logs`) | Modify |
| `POST /auth/login` response — include `force_password_change: bool` | Modify (LoginResponse DTO) |
| `POST /auth/change-password` — new endpoint | New |
| `GET /auth/me` response — include `permissions[]` and `force_password_change` | Modify (MeResponse DTO) |

### 9.2 User Management Endpoints — Changes Needed

| Change | New or Modified |
|---|---|
| `POST /users` — record `created_by_user_id` from `ICurrentUserService` | Modify |
| `POST /users` — set `force_password_change = true` by default on creation | Modify |
| `POST /users/{id}/activate` — Admin only (new code `USERS_ACTIVATE`) | New |
| `POST /users/{id}/deactivate` — Admin only (new code `USERS_DEACTIVATE`) | New |
| `DELETE /users/{id}` — soft delete, Admin only (new code `USERS_DELETE`) | Modify (current one deactivates; make it soft-delete) |
| `POST /users/{id}/reset-password` — Admin only (new code `USERS_RESET_PASSWORD`) | New |
| `GET /users` — exclude `is_deleted = true` from default list | Modify |
| Split `USERS_WRITE` into `USERS_CREATE`, `USERS_EDIT`, `USERS_ACTIVATE`, etc. | Modify permission codes |

### 9.3 Permission Retrieval

| Change | New or Modified |
|---|---|
| `GET /users/{id}/permissions` — return effective permission set (role + overrides) | New |
| `PUT /users/{id}/permissions` — set user-specific overrides | New |
| `GET /auth/me` — include `permissions[]` in response (remove client-side JWT parsing dependency) | Modify |

### 9.4 Audit Logging

| Change | New or Modified |
|---|---|
| `GET /api/v1/audit` — paged audit log endpoint (gated by `AUDIT_READ`) | New |
| Login event written at login success/failure | Modify login handler |
| Permission change events for `user_permissions` table | Automatic via `AuditInterceptor` once table exists |

### 9.5 Screen/Button Action Authorization

The existing `PermissionAuthorizationHandler` + `PermissionPolicyProvider` pattern handles this automatically via `RequireAuthorization("Permission:CODE")`. No framework change needed — only new permission codes must be registered and applied to new/split endpoints.

---

## 10. Risks / Edge Cases

| Risk | Description | Severity |
|---|---|---|
| **Hidden UI but callable API** | A user without `SALES_POST` has the Post button hidden — but they could call `POST /sales/{id}/post` directly with a valid token if the API endpoint doesn't enforce the permission. This is already mitigated by `RequireAuthorization` on endpoints, **but must be verified for every page where button-level hiding was added without a corresponding API check.** | High |
| **Stale client permissions** | After login, permissions are baked into the JWT (15-min TTL) and cached in `_permissions` list in `SessionService`. If an Admin changes a user's role or overrides mid-session, the user's running session reflects the OLD permissions until next token refresh (or 7 days if tokens keep refreshing). | High |
| **Manager creates user, not yet activated (if activation is required)** | If we introduce an "activation required" step (غير محسوم — needs a decision: does every new user start as active or inactive?), there is a window where a user exists with `IsActive = false` until the Admin activates them. Currently `CreateUserAsync` sets `IsActive = true` immediately. | Medium |
| **Soft delete vs deactivation confusion** | Current `DELETE /users/{id}` sets `IsActive = false`. If soft delete is added (`IsDeleted = true`), the semantics must be clarified: deactivated = cannot log in but exists; deleted = hidden from all lists. The API and UI must distinguish these clearly. | Medium |
| **Password reset flow — race condition** | If Admin resets a user's password and the user is actively logged in, the user's current tokens remain valid until expiry (15 min). No immediate revocation of existing sessions on password reset. | Medium |
| **Audit gaps for permission changes** | `AuditInterceptor` will capture inserts/updates to `role_permissions` and `user_permissions` tables generically (as "UPDATE" on table), but the `Module` field will be null and the `EntityName` will be "RolePermission" or "UserPermission" — not human-readable in an audit UI. | Low |
| **Menu caching / session restore** | Sidebar flags are set once at session start via `RefreshUserState()`. If `TryRestoreSessionAsync()` is called at startup, `MainViewModel` is constructed with stale permissions from a possibly old JWT. This is acceptable for read access but requires a deliberate decision for mid-session permission changes. | Low |
| **Deep-link / direct navigation to unauthorized screens** | Currently, navigating to an unimplemented page is caught by the `default` switch case in `NavigateTo` which falls back to Home. Once Users/Roles pages exist, navigating to them via keyboard shortcuts or programmatic navigation while lacking permission would succeed at the navigation layer. The ViewModel itself must guard in `OnNavigatedTo` or constructor. | Low |
| **Admin bootstrapping / self-lock** | AdminSeeder always ensures the `admin` user has all permissions. If an operator accidentally removes the Admin role from the only admin, they could be locked out. The Admin seeder re-assigns all permissions on startup, providing a recovery path — but only for the seeded `admin` account. | Medium |
| **Deleted/inactive users with active tokens** | If a user is deactivated or deleted, their existing JWT tokens remain valid until expiry (15 min). The login endpoint checks `IsActive`, but there is no per-request active-check on authenticated calls. A deactivated user can continue using the system for up to 15 minutes. | Medium |
| **Permission assignment UI — large permission list** | With 45+ existing codes and more to come, the hierarchical permission UI must be carefully designed for usability. Without section/screen metadata on permission records, the UI has no way to group them automatically. | Low |

---

## 11. Proposed Implementation Slicing

**Recommended safe implementation order. This is a recommendation only — not implementation.**

### Slice 1 — Auth/Login Foundation
- Add `last_login_at_utc` to User entity + migration.
- Add `force_password_change` to User entity + migration.
- Add `display_name_arabic` to User entity + migration.
- Update login endpoint: record `last_login_at_utc`, write login audit entry.
- Add login response field `force_password_change`.
- Add `POST /auth/change-password` endpoint.
- Update `GET /auth/me` to return `permissions[]` and `force_password_change`.
- Desktop: add first-login password change intercept in login flow.
- Desktop: show `display_name_arabic` (or username if null) in top bar.

### Slice 2 — Users Management
- Add `is_deleted`, `deleted_at_utc`, `deleted_by_user_id`, `created_by_user_id`, `updated_at_utc`, `updated_by_user_id` to User + migration.
- Split `USERS_WRITE` into granular codes: `USERS_CREATE`, `USERS_EDIT`, `USERS_ACTIVATE`, `USERS_DEACTIVATE`, `USERS_DELETE`, `USERS_RESET_PASSWORD`.
- Add new API endpoints: activate, deactivate (separate), soft-delete, reset-password.
- Update seeder: seed granular user management permissions, seed Manager role.
- Desktop: build `UsersViewModel` + `UsersPage` with per-permission button visibility.

### Slice 3 — Permission Skeleton + Storage
- Add section/screen/action metadata fields to `Permissions.cs` All list.
- Update seeder to sync metadata to `permissions` table.
- Add `user_permissions` table + entity + migration.
- Update JWT generation to merge role permissions + user grants – user denies.
- Add API endpoints: `GET /users/{id}/permissions`, `PUT /users/{id}/permissions`.

### Slice 4 — Permission Assignment UI
- Desktop: build `RolesViewModel` + `RolesPage` (role CRUD + role permission assignment).
- Desktop: add permission assignment panel to Users screen (user-specific overrides, hierarchical display).

### Slice 5 — UI Hiding Completion + API Enforcement Audit
- Complete button-level permission flags for all pages that are missing them: Products, Variants, Suppliers, Warehouses, Stock Movements, Production, Dashboard, Print Config.
- Verify every permissioned button in XAML has a corresponding `RequireAuthorization` on its API call.

### Slice 6 — Audit Log Closeout
- Add `GET /api/v1/audit` paged endpoint gated by `AUDIT_READ`.
- Desktop: build `AuditLogPage` / `AuditLogViewModel`.
- Review and standardize `Module` field in `AuditInterceptor` for key entity types.

---

## 12. Final Readiness Verdict

### Clear Enough to Start Implementation Immediately

- **Auth foundation (Slice 1)** — data model additions are straightforward migrations. The JWT pipeline is well-understood and modifiable. No architectural risk.
- **User entity extensions** (Slice 2 fields) — purely additive; no breaking changes to existing queries.
- **Granular user permission codes** — adding new constants to `Permissions.cs` and seeding them is safe. The dynamic policy provider handles new codes automatically.
- **UsersViewModel + UsersPage pattern** — the `PagedListViewModelBase` pattern is proven across 10+ existing pages. The only novelty is per-button admin/manager visibility flags.
- **`display_name_arabic`** — trivial field addition.

---

### Still غير محسوم — Must Be Decided Before Coding

| Decision Needed | Why It Blocks |
|---|---|
| Does a newly-created user start as `IsActive = true` or must an Admin explicitly activate? | Changes create-user behavior and Manager-creates-but-Admin-activates workflow. |
| Can a Manager assign roles to users they create, or only Admin? | Determines whether `USERS_ASSIGN_ROLES` goes to Manager role. |
| Can a Manager view **all** users or only users they created? | Determines query filter in `GET /users`. |
| What happens to existing permissions when `USERS_WRITE` is split? | Migration must re-seed Admin role with new granular codes. Any custom roles with `USERS_WRITE` will lose it — they need manual reassignment. |
| Is re-using the same password on first-login change allowed? (Phase brief says yes — final confirmation) | Password history check logic depends on this. |
| Should `DELETE /users/{id}` become soft-delete or hard-delete? | Affects query filters and data retention. |
| How should `MeResponse.permissions[]` be structured — flat list or hierarchical JSON? | Affects both API contract and desktop parsing. |
| What is the Arabic label for "Roles" in the system? (اللغة للـ screens) | Needed for `display_name_arabic` on Role equivalent — غير محسوم. |

---

### Can Be Safely Deferred to a Later Phase

| Item | Reason |
|---|---|
| **Full audit log viewer screen** (desktop) | `audit_logs` is already capturing everything. The viewer is a display-only screen; can be built any time after the endpoint is added. |
| **Permission metadata hierarchy for UI grouping** | The flat permission list works for Slice 3 storage. The hierarchical UI display can be refined in Slice 4+ without changing the data model. |
| **Password history table** (prevent re-use of old passwords) | Low priority; only relevant once force-change flow exists. Phase brief explicitly allows re-using the same password on first change. |
| **Dispositions desktop page** | Backend is complete; desktop page is missing but not related to auth/permissions. |
| **Production desktop page** | Same as above. |
| **Session mid-expiry permission refresh** (invalidate cached permissions without logout) | Complex to implement safely; the 15-minute JWT TTL provides a natural refresh point. Acceptable for V1. |
| **Login attempt rate limiting / account lockout** | Security hardening; not requested in this phase. |

---

*End of discovery report. No code was modified. No files were created apart from this report.*
