# UI 2 — PLAN REPORT

## 1) Scope & Goal

### Goal
Build all business screens on top of the UI 1 WPF Foundation shell, turning the desktop client into a fully functional ERP/POS application. UI 2 covers: **Login → Master Data CRUD → Inventory → Procurement → Production → POS/Sales → Returns & Dispositions → Accounting/Payments → Dashboard → Import Wizard → Printing → Admin (Users/Roles/Permissions)**.

### In Scope
| Area | Details |
|------|---------|
| **Login / Session** | Login screen, token persistence, auto-refresh, logout, session expiry handling |
| **Master Data** | Products + Variants + Barcodes, Customers, Suppliers — full CRUD grids/forms |
| **Warehouses** | CRUD grid/form |
| **Stock** | Balance browser, ledger viewer, stock movement posting (opening balance / adjustment / transfer) |
| **Purchases** | Draft → Post workflow: list, create/edit, post |
| **Production** | Draft → Post workflow for production batches |
| **POS / Sales** | Fast POS screen with barcode scan, line editing, posting |
| **Returns** | Sales Returns, Purchase Returns — Draft → Post / Void |
| **Dispositions** | Draft → Approve → Post / Void with reason codes |
| **Reason Codes** | Admin CRUD for reason code catalog |
| **Accounting** | Customer/Supplier AR/AP balances, ledger viewer |
| **Payments** | Create payment, payment list |
| **Dashboard** | KPI cards, top products, low stock alerts, cashier performance |
| **Import Wizard** | Preview/Commit for master data, opening balances, payments |
| **Printing** | Print Profile/Rule management, receipt print integration hooks |
| **Admin — Users/Roles** | User CRUD, role CRUD, permission assignment |
| **Permission-Aware UI** | Show/hide/disable screens + buttons based on user permissions |
| **Arabic/RTL** | Right-to-left text support for Arabic fields/reason code names |

### Out of Scope
| Area | Reason |
|------|--------|
| **Offline mode / local cache** | Policy: UI is always-connected, server-only truth |
| **External HTTP integrations** | Policy: no external API calls from UI; future server-to-server only |
| **Direct DB access from UI** | Policy: all data via API only |
| **Multi-language full i18n** | (غير مذكور في المدخلات) — Arabic field support ≠ full localization |
| **Audit log viewer** | `AUDIT_READ` permission exists but no audit log read endpoint is exposed (غير مذكور في المدخلات — endpoint missing) |
| **Mobile / Web UI** | Desktop WPF only |

---

## 2) Assumptions & Unknowns

### Assumptions
| # | Assumption |
|---|-----------|
| A1 | UI 1 Foundation is stable — shell, themes, DI, `ApiClient`, `NavigationService` are all confirmed working (build 0 errors) |
| A2 | Backend is running and reachable at the configured `Api:BaseUrl` during UI usage |
| A3 | JWT access token lifetime ≈15 min, refresh token ≈7 days (from backend auth service defaults) |
| A4 | All list endpoints return `PagedResult<T>` with `{ items, totalCount, page, pageSize }` |
| A5 | All write/post errors follow RFC 7807 ProblemDetails with `title` = one of the 65 ErrorCodes |
| A6 | The `InMemoryTokenStore` stub will be replaced by a secure persistent store (DPAPI/ProtectedData or similar) |
| A7 | POS does not need to work offline — a network connection to the API is always required |
| A8 | Printing sends data to a local printer via WPF printing APIs; print layout is controlled by `ConfigJson` from PrintRule |
| A9 | Barcode scanning is via keyboard-wedge scanner (input treated as rapid keystrokes) |
| A10 | Admin seeds with all 47 permissions on first run; no bootstrap UI needed |

### Unknowns
| # | Unknown | Impact |
|---|---------|--------|
| U1 | Receipt template design / paper size (80mm thermal? A4?) | (غير مذكور في المدخلات) — will implement with configurable width from `ConfigJson` |
| U2 | Barcode label printing format (ZPL, image, PDF?) | (غير مذكور في المدخلات) — defer to printing phase |
| U3 | Exact POS payment collection flow (cash drawer, change calculation, split payment) | (غير مذكور في المدخلات) — POS currently only creates sales invoice + posts; payment is a separate action |
| U4 | Multi-warehouse POS (can a cashier switch warehouses mid-shift?) | (غير مذكور في المدخلات) — assume warehouse is selected per-invoice |
| U5 | Session timeout UX (auto-logout after idle? warning dialog?) | (غير مذكور في المدخلات) — will implement token-refresh; show re-login dialog on 401 |
| U6 | Audit log viewer endpoint | `AUDIT_READ` permission exists but no `/audit` read endpoint (غير مذكور في المدخلات) |
| U7 | Dashboard chart library / visualization style | (غير مذكور في المدخلات) — will use lightweight WPF charting or simple data cards |
| U8 | Arabic-only or Arabic+English UI labels | (غير مذكور في المدخلات) — will keep English UI labels with Arabic data field support |

---

## 3) UI Architecture Proposal (WPF)

### MVVM Pattern
- Continue using **CommunityToolkit.Mvvm** with source generators (`[ObservableProperty]`, `[RelayCommand]`)
- Each screen = one `Page` (XAML) + one `ViewModel` (C#)
- Complex screens (POS, Import) get additional child ViewModels for sub-sections
- No code-behind logic; all behavior in ViewModels/Services

### Project Structure (Proposed)

```
src/ElshazlyStore.Desktop/
├── App.xaml / App.xaml.cs              # DI, startup, theme init
├── app.manifest                        # DPI awareness
├── appsettings.json                    # API base URL, logging
│
├── Helpers/                            # Converters, template selectors
│   ├── BoolToVisibilityConverter.cs
│   ├── InverseBoolConverter.cs
│   ├── DecimalFormatConverter.cs
│   ├── StatusToColorConverter.cs
│   ├── PageDataTemplateSelector.cs
│   └── ...
│
├── Models/                             # Client-side DTOs mirroring API
│   ├── ApiResult.cs                    # (exists)
│   ├── ProblemDetails.cs               # (exists)
│   ├── Auth/
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   └── MeResponse.cs
│   ├── Products/
│   │   ├── ProductDto.cs
│   │   ├── VariantDto.cs
│   │   └── ...
│   ├── Sales/
│   ├── Purchases/
│   ├── Stock/
│   ├── Accounting/
│   ├── Dashboard/
│   ├── Import/
│   ├── Printing/
│   ├── Returns/
│   └── Admin/
│
├── Resources/
│   ├── Themes/                         # (exists: Dark/Light/Shared)
│   ├── Icons/                          # Vector icon geometries
│   └── Styles/
│       ├── DataGridStyles.xaml         # Themed DataGrid
│       ├── FormStyles.xaml             # TextBox, ComboBox, etc.
│       ├── DialogStyles.xaml           # Modal dialogs
│       └── StatusBadgeStyles.xaml      # Draft/Posted/Voided badges
│
├── Services/
│   ├── INavigationService.cs           # (exists)
│   ├── NavigationService.cs            # (exists)
│   ├── IThemeService.cs                # (exists)
│   ├── ThemeService.cs                 # (exists)
│   ├── IMessageService.cs             # (exists)
│   ├── MessageService.cs              # (exists)
│   ├── IUserPreferencesService.cs     # (exists)
│   ├── ISessionService.cs             # NEW: login/logout/token refresh/permissions
│   ├── SessionService.cs
│   ├── IPermissionService.cs          # NEW: check if current user has permission
│   ├── PermissionService.cs
│   ├── IPrintService.cs               # NEW: local printing via WPF
│   ├── PrintService.cs
│   └── Api/
│       ├── ApiClient.cs               # (exists — add multipart upload)
│       ├── AuthHeaderHandler.cs       # (exists)
│       ├── CorrelationIdHandler.cs    # (exists)
│       ├── ITokenStore.cs             # (exists)
│       ├── SecureTokenStore.cs        # NEW: replaces InMemoryTokenStore
│       └── TokenRefreshHandler.cs     # NEW: auto-refresh on 401
│
├── ViewModels/
│   ├── ViewModelBase.cs               # (exists)
│   ├── MainViewModel.cs              # (exists — extend nav items)
│   ├── LoginViewModel.cs
│   ├── HomeViewModel.cs              # (exists)
│   ├── SettingsViewModel.cs          # (exists)
│   ├── Products/
│   │   ├── ProductListViewModel.cs
│   │   ├── ProductFormViewModel.cs
│   │   └── VariantFormViewModel.cs
│   ├── Customers/
│   │   ├── CustomerListViewModel.cs
│   │   └── CustomerFormViewModel.cs
│   ├── Suppliers/
│   │   ├── SupplierListViewModel.cs
│   │   └── SupplierFormViewModel.cs
│   ├── Warehouses/
│   │   ├── WarehouseListViewModel.cs
│   │   └── WarehouseFormViewModel.cs
│   ├── Stock/
│   │   ├── StockBalanceViewModel.cs
│   │   ├── StockLedgerViewModel.cs
│   │   └── StockMovementViewModel.cs
│   ├── Purchases/
│   │   ├── PurchaseListViewModel.cs
│   │   └── PurchaseFormViewModel.cs
│   ├── Production/
│   │   ├── ProductionListViewModel.cs
│   │   └── ProductionFormViewModel.cs
│   ├── Sales/
│   │   ├── SalesListViewModel.cs
│   │   ├── SalesFormViewModel.cs
│   │   └── PosViewModel.cs
│   ├── Returns/
│   │   ├── SalesReturnListViewModel.cs
│   │   ├── SalesReturnFormViewModel.cs
│   │   ├── PurchaseReturnListViewModel.cs
│   │   ├── PurchaseReturnFormViewModel.cs
│   │   ├── DispositionListViewModel.cs
│   │   ├── DispositionFormViewModel.cs
│   │   └── ReasonCodeListViewModel.cs
│   ├── Accounting/
│   │   ├── BalancesViewModel.cs
│   │   ├── LedgerViewModel.cs
│   │   └── PaymentFormViewModel.cs
│   ├── Dashboard/
│   │   └── DashboardViewModel.cs
│   ├── Import/
│   │   └── ImportWizardViewModel.cs
│   ├── Printing/
│   │   ├── PrintProfileListViewModel.cs
│   │   └── PrintRuleFormViewModel.cs
│   └── Admin/
│       ├── UserListViewModel.cs
│       ├── UserFormViewModel.cs
│       ├── RoleListViewModel.cs
│       └── RoleFormViewModel.cs
│
├── Views/
│   ├── MainWindow.xaml / .cs          # (exists — extend sidebar)
│   ├── LoginWindow.xaml / .cs         # Separate window (no shell chrome)
│   └── Pages/                         # One Page per ViewModel
│       ├── HomePage.xaml              # (exists)
│       ├── SettingsPage.xaml          # (exists)
│       ├── Products/
│       ├── Customers/
│       ├── Suppliers/
│       ├── Warehouses/
│       ├── Stock/
│       ├── Purchases/
│       ├── Production/
│       ├── Sales/
│       ├── Returns/
│       ├── Accounting/
│       ├── Dashboard/
│       ├── Import/
│       ├── Printing/
│       └── Admin/
│
└── Properties/
    └── PublishProfiles/               # (exists: x64/x86)
```

### API Client Strategy
| Concern | Approach |
|---------|----------|
| **Typed client** | Existing `ApiClient` with `Get/Post/Put/DeleteAsync<T>` — sufficient for all JSON endpoints |
| **Multipart upload** | Add `PostMultipartAsync<T>` method for Import file uploads |
| **Auth header** | `AuthHeaderHandler` (exists) injects Bearer token from `ITokenStore` |
| **Token refresh** | New `TokenRefreshHandler` (DelegatingHandler): intercepts 401 → calls `/auth/refresh` → retries original request once |
| **Secure token storage** | `SecureTokenStore` using `System.Security.Cryptography.ProtectedData` (DPAPI) persisted to `%LOCALAPPDATA%` |
| **Correlation ID** | `CorrelationIdHandler` (exists) adds `X-Correlation-Id` to every request |
| **Timeout** | 30s default (exists); Import uploads may need longer timeout |

### Error Handling Mapping
| API Response | UI Action |
|-------------|-----------|
| 200-204 | Success → refresh data / navigate / show toast |
| 400 (`VALIDATION_FAILED`) | Parse `errors` dict → show field-level validation |
| 401 (`UNAUTHORIZED` / `TOKEN_EXPIRED`) | Attempt refresh → if fails, redirect to Login |
| 403 (`FORBIDDEN`) | Show "You don't have permission" message |
| 404 (`NOT_FOUND`) | Show "Record not found or deleted" |
| 409 (`CONFLICT` / `POST_CONCURRENCY_CONFLICT`) | Show "Record was modified elsewhere, please reload" |
| 422 (business rules: `STOCK_NEGATIVE_NOT_ALLOWED`, `OVERPAYMENT_NOT_ALLOWED`, etc.) | Show specific error message from `ProblemDetails.Detail` |
| 5xx / network error | Show "Server unreachable" with retry option |

All 65 error codes map through `ProblemDetails.ToUserMessage()` (already implemented) → displayed via `IMessageService`.

### Offline / Retry / Caching
- **No offline mode** (policy).
- **No client-side cache** for mutable data (server = truth). UI always fetches fresh on navigation.
- **Barcode lookup**: Server already caches 60s; UI won't add extra caching.
- **Retry**: Only on token refresh (auto-retry once after 401). No general retry — user manually retries.
- **Connection loss**: show blocking overlay "Connection lost — reconnect?" with retry button.

---

## 4) Screen Backlog (MVP → Next)

| # | Screen | User Goal | Dependencies | Acceptance Criteria | Priority |
|---|--------|-----------|-------------|---------------------|----------|
| S01 | **Login** | Authenticate and obtain session | `POST /auth/login`, `POST /auth/refresh` | User enters username+password → receives token → shell loads with permissions → token auto-refreshes before expiry | **P0** |
| S02 | **Dashboard** | View business KPIs at a glance | `GET /dashboard/*` (4 endpoints), `DASHBOARD_READ` | Shows sales total, invoice count, avg ticket, top products list, low-stock alerts, cashier table. Date range filter works | **P0** |
| S03 | **Product List** | Browse/search/filter products | `GET /products` (paged, q, sort), `PRODUCTS_READ` | Paged DataGrid with search, sort by name/category/created. Click row → opens detail form | **P0** |
| S04 | **Product Form** | Create/edit product + manage variants | `POST/PUT/DELETE /products`, `POST/PUT/DELETE /variants`, `PRODUCTS_WRITE` | Create product → add variants with SKU/barcode/prices → save. Edit inline. Barcode uniqueness enforced (server error shown) | **P0** |
| S05 | **Customer List** | Browse/search customers | `GET /customers` (paged, q, sort), `CUSTOMERS_READ` | Paged DataGrid with search on code/name/phone | **P0** |
| S06 | **Customer Form** | Create/edit customer | `POST/PUT /customers`, `CUSTOMERS_WRITE` | Auto-generated code if omitted. Toggle active/inactive | **P0** |
| S07 | **Supplier List** | Browse/search suppliers | `GET /suppliers`, `SUPPLIERS_READ` | Same pattern as customers | **P0** |
| S08 | **Supplier Form** | Create/edit supplier | `POST/PUT /suppliers`, `SUPPLIERS_WRITE` | Same pattern as customers | **P0** |
| S09 | **Warehouse List** | Browse warehouses | `GET /warehouses`, `WAREHOUSES_READ` | Shows code, name, address, isDefault. Mark active/inactive | **P1** |
| S10 | **Warehouse Form** | Create/edit warehouse | `POST/PUT /warehouses`, `WAREHOUSES_WRITE` | Create with code + name. One default warehouse | **P1** |
| S11 | **Stock Balances** | View current stock levels | `GET /stock/balances` (paged, warehouseId, q), `STOCK_READ` | Filter by warehouse, search by SKU/product. Shows qty per variant per warehouse | **P0** |
| S12 | **Stock Ledger** | View movement history | `GET /stock/ledger` (variantId, warehouseId, from, to), `STOCK_READ` | Date range filter. Shows movement type, reference, qty delta, username | **P1** |
| S13 | **Stock Movement** | Post manual movements (opening balance, adjustment, transfer) | `POST /stock-movements/post`, `STOCK_POST` | Select type → enter lines → post. Negative stock blocked. Transfer requires balanced lines | **P1** |
| S14 | **Purchase List** | Browse purchase receipts | `GET /purchases`, `PURCHASES_READ` | Paged DataGrid. Filter by status (Draft/Posted). Search by document number | **P0** |
| S15 | **Purchase Form** | Create/edit/post purchase receipt | `POST/PUT/DELETE /purchases`, `POST /purchases/{id}/post`, `PURCHASES_WRITE`, `PURCHASES_POST` | Select supplier + warehouse → add lines (variant, qty, cost) → Draft. Post → stock in + AP entry. Cannot edit after post | **P0** |
| S16 | **Production List** | Browse production batches | `GET /production`, `PRODUCTION_READ` | Same list pattern. Filter Draft/Posted | **P1** |
| S17 | **Production Form** | Create/edit/post production batch | `POST/PUT/DELETE /production`, `POST /production/{id}/post`, `PRODUCTION_WRITE`, `PRODUCTION_POST` | Input lines (raw materials) + Output lines (finished goods) → Post creates consume + produce movements | **P1** |
| S18 | **POS / Sales Screen** | Fast point-of-sale invoice creation | `GET /barcodes/{barcode}`, `POST/PUT /sales`, `POST /sales/{id}/post`, `SALES_WRITE`, `SALES_POST`, `PRODUCTS_READ` | Barcode scan → adds line → running total. Select customer (optional). Post → prints receipt (if print rule exists). Sub-second barcode lookup | **P0** |
| S19 | **Sales List** | Browse sales invoices | `GET /sales`, `SALES_READ` | Paged DataGrid, search, status filter | **P0** |
| S20 | **Sales Form** | View/edit draft sales invoice (non-POS) | `PUT /sales/{id}`, `SALES_WRITE` | Edit draft lines, change customer/warehouse → save or post | **P1** |
| S21 | **Sales Return List** | Browse sales returns | `GET /sales-returns`, `VIEW_SALES_RETURNS` | Paged, status filter (Draft/Posted/Voided) | **P1** |
| S22 | **Sales Return Form** | Create/edit/post/void sales return | All sales return endpoints, `SALES_RETURN_CREATE/POST/VOID` | Select warehouse, optional customer + original invoice, add lines with reason code + disposition type → Post or Void. Qty validation against original invoice | **P1** |
| S23 | **Purchase Return List** | Browse purchase returns | `GET /purchase-returns`, `VIEW_PURCHASE_RETURNS` | Same list pattern | **P1** |
| S24 | **Purchase Return Form** | Create/edit/post/void purchase return | All purchase return endpoints, `PURCHASE_RETURN_CREATE/POST/VOID` | Same workflow as sales return but for supplier | **P1** |
| S25 | **Disposition List** | Browse inventory dispositions | `GET /dispositions`, `VIEW_DISPOSITIONS` | Paged, status filter | **P1** |
| S26 | **Disposition Form** | Create/edit/approve/post/void disposition | All disposition endpoints, `DISPOSITION_CREATE/APPROVE/POST/VOID` | Add lines with reason + disposition type (Scrap/Quarantine/Rework/WriteOff). Manager approval required for flagged reasons. Post moves stock to special warehouse | **P1** |
| S27 | **Reason Code List** | Manage reason codes | `GET/POST/PUT /reasons`, `MANAGE_REASON_CODES`, `VIEW_REASON_CODES` | CRUD grid. Filter by category. Disable (no hard delete). Flag requiresManagerApproval | **P1** |
| S28 | **AR/AP Balances** | View customer/supplier outstanding | `GET /accounting/balances/*`, `ACCOUNTING_READ` | Two tabs: Customers / Suppliers. Search, paged | **P1** |
| S29 | **Party Ledger** | View ledger entries for a party | `GET /accounting/ledger/{partyType}/{partyId}`, `ACCOUNTING_READ` | Date paged list of entries (type, amount, reference) | **P1** |
| S30 | **Payment Form** | Record payment | `POST /payments`, `PAYMENTS_WRITE` | Select party, enter amount, select method (Cash/InstaPay/EWallet/Visa), EWallet requires wallet name. Overpayment blocked | **P1** |
| S31 | **Payment List** | Browse payments | `GET /payments`, `PAYMENTS_READ` | Paged, filter by party type/id | **P1** |
| S32 | **Import Wizard** | Import CSV/XLSX data | Import endpoints (preview + commit), `IMPORT_MASTER_DATA` / `IMPORT_OPENING_BALANCES` / `IMPORT_PAYMENTS` | Step 1: select type + upload file → Step 2: preview validation results (row errors) → Step 3: commit. Show imported count. Admin-only | **P2** |
| S33 | **Print Profile List** | Manage print profiles | Print profile endpoints, `MANAGE_PRINTING_POLICY` | CRUD grid for profiles + nested rules | **P2** |
| S34 | **Print Rule Form** | Configure print rules per screen | Print rule endpoints, `MANAGE_PRINTING_POLICY` | Edit screen code + ConfigJson + Enabled toggle per profile | **P2** |
| S35 | **User List** | Manage users | `GET /users`, `USERS_READ` | DataGrid showing username, active, roles, created date | **P1** |
| S36 | **User Form** | Create/edit user | `POST/PUT /users`, `USERS_WRITE` | Username, password, isActive, assign roles | **P1** |
| S37 | **Role List** | Manage roles | `GET /roles`, `ROLES_READ` | DataGrid showing name, description, permission count | **P1** |
| S38 | **Role Form** | Create/edit role + assign permissions | Role endpoints + permission endpoints, `ROLES_WRITE` | Edit name/description + checklist of 47 permissions | **P1** |

---

## 5) UI Contract Matrix

### Legend
- **Perm** = Required permission policy code
- **EC** = Key ErrorCodes that screen must handle beyond generic errors
- **Print** = Print hook (screenCode for `GET /print-policy/{screenCode}`)
- **Perf** = Performance note
- **(غير مذكور)** = not specified in inputs

| Screen | Endpoints (Method Route) | Perm | DTOs (Request / Response) | ErrorCodes | Print Hook | Perf Notes |
|--------|--------------------------|------|---------------------------|------------|------------|------------|
| **Login** | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me` | Anonymous / Authenticated | `LoginRequest` → `LoginResponse`, `RefreshRequest`, `MeResponse` | `INVALID_CREDENTIALS`, `ACCOUNT_INACTIVE`, `TOKEN_EXPIRED`, `TOKEN_INVALID` | — | Fast (<500ms) |
| **Dashboard** | `GET /dashboard/summary`, `/sales`, `/top-products`, `/low-stock`, `/cashier-performance` | `DASHBOARD_READ` | Dashboard DTOs (summary, top products, low stock, cashier) | — | — | Date range queries; topN default=10 |
| **Product List** | `GET /products` | `PRODUCTS_READ` | `PagedResult<ProductDto>` | — | — | Paged; server-side search |
| **Product Form** | `GET /products/{id}`, `POST /products`, `PUT /products/{id}`, `DELETE /products/{id}` | `PRODUCTS_READ`, `PRODUCTS_WRITE` | `ProductDetailDto`, `CreateProductRequest`, `UpdateProductRequest` | `NOT_FOUND`, `CONFLICT` | — | — |
| **Variant Form** | `GET /variants/{id}`, `POST /variants`, `PUT /variants/{id}`, `DELETE /variants/{id}` | `PRODUCTS_READ`, `PRODUCTS_WRITE` | `VariantListDto`, `CreateVariantRequest`, `UpdateVariantRequest` | `BARCODE_ALREADY_EXISTS`, `NOT_FOUND` | — | Barcode uniqueness |
| **Customer List** | `GET /customers` | `CUSTOMERS_READ` | `PagedResult<CustomerDto>` | — | — | Search on code/name/phone |
| **Customer Form** | `GET /customers/{id}`, `POST /customers`, `PUT /customers/{id}`, `DELETE /customers/{id}` | `CUSTOMERS_READ`, `CUSTOMERS_WRITE` | `CustomerDto`, `CreateCustomerRequest`, `UpdateCustomerRequest` | `NOT_FOUND`, `CONFLICT` | — | Auto-gen code CUST-NNNNNN |
| **Supplier List** | `GET /suppliers` | `SUPPLIERS_READ` | `PagedResult<SupplierDto>` | — | — | Search on code/name/phone |
| **Supplier Form** | `GET /suppliers/{id}`, `POST /suppliers`, `PUT /suppliers/{id}`, `DELETE /suppliers/{id}` | `SUPPLIERS_READ`, `SUPPLIERS_WRITE` | `SupplierDto`, `CreateSupplierRequest`, `UpdateSupplierRequest` | `NOT_FOUND`, `CONFLICT` | — | Auto-gen code SUP-NNNNNN |
| **Warehouse List** | `GET /warehouses` | `WAREHOUSES_READ` | `PagedResult<WarehouseDto>` | — | — | — |
| **Warehouse Form** | `POST /warehouses`, `PUT /warehouses/{id}`, `DELETE /warehouses/{id}` | `WAREHOUSES_WRITE` | `CreateWarehouseRequest`, `UpdateWarehouseRequest` | `NOT_FOUND` | — | — |
| **Stock Balances** | `GET /stock/balances` | `STOCK_READ` | Balance items (variant, warehouse, qty) | — | — | Paged; filter by warehouseId |
| **Stock Ledger** | `GET /stock/ledger` | `STOCK_READ` | Ledger items (movement, type, delta, cost) | — | — | Filter by variant, warehouse, date range |
| **Stock Movement** | `POST /stock-movements/post` | `STOCK_POST` | `PostRequest` (type, reference, lines[]) | `STOCK_NEGATIVE_NOT_ALLOWED`, `MOVEMENT_EMPTY`, `WAREHOUSE_NOT_FOUND`, `VARIANT_NOT_FOUND`, `TRANSFER_UNBALANCED` | — | Serializable isolation server-side |
| **Purchase List** | `GET /purchases` | `PURCHASES_READ` | `PagedResult<ReceiptDto>` | — | — | — |
| **Purchase Form** | `GET /purchases/{id}`, `POST /purchases`, `PUT /purchases/{id}`, `DELETE /purchases/{id}`, `POST /purchases/{id}/post` | `PURCHASES_READ`, `PURCHASES_WRITE`, `PURCHASES_POST` | `ReceiptDetailDto`, `CreatePurchaseRequest`, `UpdatePurchaseRequest` | `PURCHASE_RECEIPT_NOT_FOUND`, `PURCHASE_RECEIPT_ALREADY_POSTED`, `PURCHASE_RECEIPT_EMPTY`, `SUPPLIER_NOT_FOUND`, `DOCUMENT_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` | (غير مذكور) | Posting is atomic + idempotent |
| **Production List** | `GET /production` | `PRODUCTION_READ` | `PagedResult<BatchDto>` | — | — | — |
| **Production Form** | `GET /production/{id}`, `POST /production`, `PUT /production/{id}`, `DELETE /production/{id}`, `POST /production/{id}/post` | `PRODUCTION_READ`, `PRODUCTION_WRITE`, `PRODUCTION_POST` | `BatchDetailDto`, `CreateProductionRequest`, `UpdateProductionRequest` | `PRODUCTION_BATCH_NOT_FOUND`, `PRODUCTION_BATCH_ALREADY_POSTED`, `PRODUCTION_BATCH_NO_INPUTS`, `PRODUCTION_BATCH_NO_OUTPUTS`, `BATCH_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` | (غير مذكور) | Two movements created on post |
| **POS / Sales** | `GET /barcodes/{barcode}`, `POST /sales`, `PUT /sales/{id}`, `POST /sales/{id}/post` | `SALES_WRITE`, `SALES_POST`, `PRODUCTS_READ` | `BarcodeLookupResult`, `CreateSalesInvoiceRequest`, `SalesInvoiceLineRequest` | `SALES_INVOICE_EMPTY`, `SALES_INVOICE_ALREADY_POSTED`, `STOCK_NEGATIVE_NOT_ALLOWED`, `BARCODE_RETIRED`, `CUSTOMER_NOT_FOUND`, `POST_CONCURRENCY_CONFLICT` | `POS_RECEIPT` (غير مذكور — proposed screen code) | Barcode cached 60s server-side; sub-second UX |
| **Sales List** | `GET /sales` | `SALES_READ` | `PagedResult<InvoiceDto>` | — | — | — |
| **Sales Form** | `GET /sales/{id}`, `PUT /sales/{id}`, `DELETE /sales/{id}`, `POST /sales/{id}/post` | `SALES_READ`, `SALES_WRITE`, `SALES_POST` | `InvoiceDetailDto`, `UpdateSalesInvoiceRequest` | Same as POS | — | — |
| **Sales Return List** | `GET /sales-returns` | `VIEW_SALES_RETURNS` | `PagedResult<SalesReturnDto>` | — | — | — |
| **Sales Return Form** | `GET /sales-returns/{id}`, `POST /sales-returns`, `PUT /sales-returns/{id}`, `DELETE /sales-returns/{id}`, `POST /sales-returns/{id}/post`, `POST /sales-returns/{id}/void` | `VIEW_SALES_RETURNS`, `SALES_RETURN_CREATE`, `SALES_RETURN_POST`, `SALES_RETURN_VOID` | `CreateSalesReturnRequest`, `SalesReturnLineRequest` | `SALES_RETURN_NOT_FOUND`, `SALES_RETURN_ALREADY_POSTED`, `SALES_RETURN_EMPTY`, `RETURN_QTY_EXCEEDS_SOLD`, `REASON_CODE_INACTIVE`, `SALES_RETURN_ALREADY_VOIDED`, `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST` | (غير مذكور) | Qty validation against original invoice |
| **Purchase Return List** | `GET /purchase-returns` | `VIEW_PURCHASE_RETURNS` | `PagedResult<PurchaseReturnDto>` | — | — | — |
| **Purchase Return Form** | `GET /purchase-returns/{id}`, `POST /purchase-returns`, `PUT /purchase-returns/{id}`, `DELETE /purchase-returns/{id}`, `POST /purchase-returns/{id}/post`, `POST /purchase-returns/{id}/void` | `VIEW_PURCHASE_RETURNS`, `PURCHASE_RETURN_CREATE`, `PURCHASE_RETURN_POST`, `PURCHASE_RETURN_VOID` | `CreatePurchaseReturnRequest`, `PurchaseReturnLineRequest` | `PURCHASE_RETURN_NOT_FOUND`, `PURCHASE_RETURN_ALREADY_POSTED`, `PURCHASE_RETURN_EMPTY`, `RETURN_QTY_EXCEEDS_RECEIVED`, `PURCHASE_RETURN_ALREADY_VOIDED`, `PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST`, `PURCHASE_RETURN_NUMBER_EXISTS` | (غير مذكور) | — |
| **Disposition List** | `GET /dispositions` | `VIEW_DISPOSITIONS` | `PagedResult<DispositionDto>` | — | — | — |
| **Disposition Form** | `GET /dispositions/{id}`, `POST /dispositions`, `PUT /dispositions/{id}`, `DELETE /dispositions/{id}`, `POST /dispositions/{id}/approve`, `POST /dispositions/{id}/post`, `POST /dispositions/{id}/void` | `VIEW_DISPOSITIONS`, `DISPOSITION_CREATE`, `DISPOSITION_APPROVE`, `DISPOSITION_POST`, `DISPOSITION_VOID` | `CreateDispositionRequest`, `DispositionLineRequest` | `DISPOSITION_NOT_FOUND`, `DISPOSITION_ALREADY_POSTED`, `DISPOSITION_EMPTY`, `DISPOSITION_ALREADY_VOIDED`, `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST`, `DISPOSITION_REQUIRES_APPROVAL`, `DISPOSITION_INVALID_TYPE`, `DESTINATION_WAREHOUSE_NOT_FOUND`, `DISPOSITION_NUMBER_EXISTS` | (غير مذكور) | Manager approval gate; Row-version concurrency |
| **Reason Code List** | `GET /reasons`, `POST /reasons`, `PUT /reasons/{id}`, `POST /reasons/{id}/disable` | `VIEW_REASON_CODES`, `MANAGE_REASON_CODES` | `CreateReasonCodeRequest`, `UpdateReasonCodeRequest` | `REASON_CODE_NOT_FOUND`, `REASON_CODE_ALREADY_EXISTS`, `REASON_CODE_IN_USE` | — | Filter by category |
| **AR/AP Balances** | `GET /accounting/balances/customers`, `/suppliers`, `/{partyType}/{partyId}` | `ACCOUNTING_READ` | Balance DTOs | — | — | Paged |
| **Party Ledger** | `GET /accounting/ledger/{partyType}/{partyId}` | `ACCOUNTING_READ` | `LedgerEntryDto` | — | — | Paged |
| **Payment Form** | `POST /payments` | `PAYMENTS_WRITE` | `CreatePaymentRequest` | `OVERPAYMENT_NOT_ALLOWED`, `WALLET_NAME_REQUIRED`, `INVALID_PAYMENT_METHOD`, `INVALID_PARTY_TYPE`, `PARTY_NOT_FOUND`, `PAYMENT_NOT_FOUND` | — | — |
| **Payment List** | `GET /payments` | `PAYMENTS_READ` | `PagedResult<PaymentDto>` | — | — | Filter by partyType, partyId |
| **Import Wizard** | `POST /imports/masterdata/preview`, `/commit`, `/opening-balances/*`, `/payments/*` | `IMPORT_MASTER_DATA`, `IMPORT_OPENING_BALANCES`, `IMPORT_PAYMENTS` | Multipart upload → `{ jobId, totalRows, validRows, rowErrors }` → `CommitRequest` → `{ success, importedCount }` | `IMPORT_PREVIEW_FAILED`, `IMPORT_COMMIT_FAILED`, `IMPORT_JOB_NOT_FOUND`, `IMPORT_JOB_ALREADY_COMMITTED` | — | Large file; longer timeout needed |
| **Print Profile List** | `GET /print-profiles`, `POST`, `PUT`, `DELETE` | `MANAGE_PRINTING_POLICY` | Profile DTOs | `PRINT_PROFILE_NOT_FOUND`, `PRINT_PROFILE_NAME_EXISTS` | — | — |
| **Print Rule Form** | `GET /print-profiles/{id}/rules`, `POST`, `PUT`, `DELETE` | `MANAGE_PRINTING_POLICY` | Rule DTOs | `PRINT_RULE_NOT_FOUND`, `PRINT_RULE_SCREEN_EXISTS` | — | — |
| **User List** | `GET /users` | `USERS_READ` | `UserDto[]` | — | — | Not paged (small dataset) |
| **User Form** | `POST /users`, `PUT /users/{id}`, `DELETE /users/{id}` | `USERS_WRITE` | `CreateUserRequest`, `UpdateUserRequest` | `NOT_FOUND`, `CONFLICT` | — | — |
| **Role List** | `GET /roles` | `ROLES_READ` | `RoleDto[]` | — | — | Not paged |
| **Role Form** | `POST /roles`, `PUT /roles/{id}`, `DELETE /roles/{id}`, `GET /roles/permissions/all`, `PUT /roles/{id}/permissions` | `ROLES_WRITE` | `CreateRoleRequest`, `SetPermissionsRequest`, `PermissionDto[]` | `NOT_FOUND` | — | 47 permission checkboxes |

---

## 6) Milestones & Phasing

### UI 2.1 — Auth + Core Navigation (Sprint 1)
| Deliverable | Detail |
|-------------|--------|
| Login Window | Username + password → call `/auth/login` → store tokens → navigate to shell |
| Secure Token Store | Replace `InMemoryTokenStore` with DPAPI-backed `SecureTokenStore` |
| Token Refresh Handler | `TokenRefreshHandler` DelegatingHandler: auto-refresh on 401, redirect to login on failure |
| Session Service | `ISessionService`: login, logout, current user info, permissions list |
| Permission Service | `IPermissionService.HasPermission(string code)` — drives UI visibility |
| Permission-Aware Sidebar | Sidebar nav items show/hide based on user permissions |
| Logout | Calls `/auth/logout`, clears token, returns to Login window |

**Definition of Done:**
- User can login → sees shell with permission-filtered sidebar → tokens auto-refresh → can logout
- Invalid credentials show error. Inactive account blocked. Token expiry handled gracefully
- Tested: login, wrong password, inactive user, token refresh, logout

---

### UI 2.2 — Master Data Screens (Sprint 2)
| Deliverable | Detail |
|-------------|--------|
| Product List + Form | Paged DataGrid with search/sort. Create/Edit/Delete product. Manage variants inline or in sub-form |
| Variant Form | Create/edit variant with SKU, barcode, prices, color, size |
| Customer List + Form | Paged DataGrid. CRUD. Auto-code. Soft-deactivate |
| Supplier List + Form | Same pattern as customers |
| Warehouse List + Form | CRUD with code, name, address, isDefault |
| Shared DataGrid Style | Themed DataGrid (`DataGridStyles.xaml`) with DPI-safe columns, Dark/Light support |
| Shared Form Style | Themed TextBox, ComboBox, labels (`FormStyles.xaml`) |

**Definition of Done:**
- All 5 entity types have working list + form screens
- Search, pagination, sort work on list screens
- Create, edit, delete (soft-deactivate for customers/suppliers/warehouses) work
- Barcode uniqueness error shown on variant form
- Permission-gated: screens hidden if user lacks READ; write buttons hidden if user lacks WRITE
- DPI 100/125/150 tested: no layout breakage

---

### UI 2.3 — Inventory & Stock (Sprint 3)
| Deliverable | Detail |
|-------------|--------|
| Stock Balance Browser | Paged grid. Filter by warehouse. Search by SKU/product name |
| Stock Ledger Viewer | Filter by variant, warehouse, date range. Movement history |
| Stock Movement Posting | Form for manual movements: Opening Balance, Adjustment, Transfer |
| Status Badge Styles | Visual badges for Draft/Posted/Voided (`StatusBadgeStyles.xaml`) |

**Definition of Done:**
- Balance screen shows correct stock per variant per warehouse
- Ledger shows full movement history with filters
- Manual posting: opening balance adds stock; adjustment modifies; transfer is balanced
- `STOCK_NEGATIVE_NOT_ALLOWED` error shown clearly
- Permission-gated: `STOCK_READ`, `STOCK_POST`

---

### UI 2.4 — Procurement & Production (Sprint 4)
| Deliverable | Detail |
|-------------|--------|
| Purchase List | Paged grid, status filter |
| Purchase Form | Create draft → add lines (variant picker, qty, cost) → save → post. Cannot edit after post |
| Production List | Paged grid, status filter |
| Production Form | Input lines + Output lines → save → post. Two movements created |
| Variant Picker Component | Reusable barcode/search selector used across Purchase/Production/Sales forms |

**Definition of Done:**
- Purchase: create draft → add lines → post → stock increases + AP ledger entry created
- Production: create draft → input/output lines → post → consume + produce movements
- `ALREADY_POSTED` error handled. Concurrency conflict shown
- Variant picker reused in both forms
- Permission-gated: separate READ/WRITE/POST permissions respected

---

### UI 2.5 — POS & Sales (Sprint 5)
| Deliverable | Detail |
|-------------|--------|
| POS Screen | Full-screen optimized: barcode input field (auto-focus), line grid, running total, customer selector, warehouse selector, Post button |
| Barcode Scanner Integration | Keyboard-wedge: rapid keystroke detection → call `/barcodes/{barcode}` → add line |
| Sales List | Paged grid, status filter (Draft/Posted) |
| Sales Form | View/edit draft invoice (non-POS editing) |
| Print Hook — POS Receipt | After posting: query `/print-policy/POS_RECEIPT` → if rule exists → render and print receipt |

**Definition of Done:**
- Barcode scan adds line in <500ms
- Walk-in sale (no customer) works
- Customer-linked sale works
- Post → stock decreases → AR entry (if customer)
- Receipt prints if print rule configured
- `STOCK_NEGATIVE_NOT_ALLOWED` shown if insufficient stock
- POS is fast: startup < 1s, no heavy resources loaded

---

### UI 2.6 — Returns & Dispositions (Sprint 6)
| Deliverable | Detail |
|-------------|--------|
| Reason Code List | CRUD for reason codes. Filter by category. Disable/enable |
| Sales Return List + Form | Draft → Post / Void. Lines require reason code + disposition type (ReturnToStock/Quarantine). Qty validation vs original invoice |
| Purchase Return List + Form | Draft → Post / Void. Disposition defaults to ReturnToVendor |
| Disposition List + Form | Draft → Approve (if required) → Post / Void. Disposition types: Scrap/Quarantine/Rework/WriteOff. Manager approval with `requiresManagerApproval` |

**Definition of Done:**
- All return types create correct stock movements on post
- Void only works on Draft status
- `RETURN_QTY_EXCEEDS_SOLD` / `RETURN_QTY_EXCEEDS_RECEIVED` errors shown
- Disposition requires approval when reason code is flagged
- Approval clears if lines are edited after approval
- All reason codes + disposition types shown in dropdowns
- Permission-gated: separate VIEW/CREATE/POST/VOID/APPROVE permissions

---

### UI 2.7 — Accounting & Payments (Sprint 7)
| Deliverable | Detail |
|-------------|--------|
| Customer Balances | Paged list of customers with outstanding AR |
| Supplier Balances | Paged list of suppliers with outstanding AP |
| Party Ledger Viewer | Ledger entries for a specific customer/supplier |
| Payment Form | Create payment: select party, amount, method (Cash/InstaPay/EWallet/Visa). Wallet name required for EWallet |
| Payment List | Paged list with filters |

**Definition of Done:**
- Balances show correct outstanding amounts
- Ledger shows all entry types (Invoice, Payment, CreditNote, DebitNote, OpeningBalance)
- Payment creation: overpayment blocked, wallet name enforced for EWallet
- Navigation: click customer balance → drill into ledger → create payment from there
- Permission-gated: `ACCOUNTING_READ`, `PAYMENTS_READ`, `PAYMENTS_WRITE`

---

### UI 2.8 — Dashboard, Import, Print Config, Admin (Sprint 8)
| Deliverable | Detail |
|-------------|--------|
| Dashboard | KPI cards (sales total, invoice count, avg ticket), top products table, low-stock alerts, cashier performance table. Date range picker |
| Import Wizard | 3-step wizard: select type + upload → preview results (errors highlighted) → commit. Admin-only |
| Print Profile Management | List/Create/Edit/Delete print profiles. Nested rule management per profile |
| User Management | List/Create/Edit/Deactivate users. Assign roles |
| Role Management | List/Create/Edit/Delete roles. Permission checklist (47 permissions) |

**Definition of Done:**
- Dashboard shows correct KPIs for selected date range
- Import: file upload works, preview shows row errors, commit imports data, `ALREADY_COMMITTED` handled
- Print profiles: CRUD + nested rules work. ConfigJson edited as JSON text
- Users: CRUD + role assignment works. Deactivation works
- Roles: CRUD + all 47 permissions shown as checkboxes, toggle on/off
- All screens permission-gated

---

## 7) Backend Gaps / Required Additions

| # | Gap | Impact | Proposed Server Change | Priority |
|---|-----|--------|----------------------|----------|
| G1 | **No audit log read endpoint** | `AUDIT_READ` permission exists but no `GET /audit` endpoint — Admin UI cannot display audit trail | Add `GET /api/v1/audit` endpoint with paged results, filter by entity/user/date | P2 |
| G2 | **ApiClient lacks multipart upload** | Import Wizard needs `multipart/form-data` file upload; current `ApiClient` only supports JSON `Post/Put` | Add `PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content)` method to `ApiClient` (client-side change only) | P1 |
| G3 | **No screen codes documented** | Print policy uses `screenCode` (e.g., `POS_RECEIPT`) but available screen codes are not enumerated in inputs | Document valid screen codes as constants or enum. Propose: `POS_RECEIPT`, `PURCHASE_RECEIPT`, `BARCODE_LABEL`, `SALES_RETURN_RECEIPT` (غير مذكور في المدخلات) | P2 |
| G4 | **Password change for self** | Only `PUT /users/{id}` exists (requires `USERS_WRITE`) — users cannot change their own password without admin permission | Add `POST /auth/change-password` endpoint (requires only Authenticated) | P2 |
| G5 | **User list not paged** | `GET /users` returns all users without paging — acceptable for small user count but may need pagination later | No change needed now — monitor | P3 |
| G6 | **No stock count / physical inventory endpoint** | Stock adjustments exist via `POST /stock-movements/post` with `Adjustment` type, but no dedicated inventory count flow | (غير مذكور في المدخلات) — defer; manual adjustment is sufficient for MVP | P3 |
| G7 | **Receipt template content** | `ConfigJson` in print rules is freeform text — no schema defined for receipt templates | Define a JSON schema for `POS_RECEIPT` config (paper width, header text, show logo, footer text, etc.) (غير مذكور في المدخلات) | P2 |

---

## 8) Risks & Mitigations

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|-----------|-----------|
| R1 | **Token refresh race condition** — Multiple concurrent API calls hit 401 simultaneously, all try to refresh | Token rotation fails (old refresh token revoked) → user forced to re-login | Medium | Implement lock/semaphore in `TokenRefreshHandler`: only one refresh at a time; queue other requests |
| R2 | **POS performance** — Barcode lookup latency affects cashier speed | Slow sales; user frustration | Low | Server caches 60s; ensure keyboard-wedge debounce in UI; pre-focus input field; measure and optimize |
| R3 | **Large Import files** — Uploading 10K+ row CSV/XLSX files | Timeout; memory issues | Medium | Increase HttpClient timeout for import endpoint; show progress indicator; server already flushes per 500 rows |
| R4 | **Permission model complexity** — 47 permissions × many screens = complex visibility logic | Bugs where buttons visible but API rejects; or buttons hidden incorrectly | Medium | Centralize permission checks in `IPermissionService`; unit test permission→screen mapping; show clear error on 403 |
| R5 | **Concurrent posting conflicts** — Two users try to post same draft | 409 CONFLICT returned; user confused | Medium | Show clear error message "This document was modified by another user. Please reload." with reload button |
| R6 | **DPI/Scaling regression** — New data-heavy screens (DataGrids, forms) break at 125/150% | Layout clipping, overlapping controls | Medium | Use relative sizing (Star columns, Auto rows); test each screen at 3 DPI levels; no fixed pixel widths in DataGrids |
| R7 | **Dark/Light theme regression** — New controls/styles miss DynamicResource binding | Visual glitches when switching themes | Low | All new styles use `DynamicResource`; theme switch test as part of each screen's acceptance |
| R8 | **Arabic text rendering** — Arabic reason code names, customer names, RTL text in LTR layout | Corrupted text or wrong alignment | Medium | Use `FlowDirection="RightToLeft"` on Arabic text fields only (not full UI RTL); test with Arabic seeded data |

---

## 9) Next Actions (Actionable)

| # | Action | Phase |
|---|--------|-------|
| 1 | **Create `LoginWindow.xaml`** — separate window (no shell) with username/password fields, login button, error label | UI 2.1 |
| 2 | **Implement `SecureTokenStore`** — replace `InMemoryTokenStore` with DPAPI-backed persistent storage in `%LOCALAPPDATA%\ElshazlyStore\tokens.dat` | UI 2.1 |
| 3 | **Implement `TokenRefreshHandler`** — DelegatingHandler that intercepts 401, calls `/auth/refresh` with semaphore lock, retries original request | UI 2.1 |
| 4 | **Implement `ISessionService` + `IPermissionService`** — login/logout flow, store `MeResponse` (user info + permissions), expose `HasPermission(code)` | UI 2.1 |
| 5 | **Extend `MainViewModel` sidebar** — add all nav items grouped by section (MAIN/COMMERCE/INVENTORY/RETURNS/ACCOUNTING/ADMIN), show/hide based on `IPermissionService` | UI 2.1 |
| 6 | **Create `DataGridStyles.xaml` + `FormStyles.xaml`** — themed reusable styles for DataGrid (row hover, alternating rows, header) + form controls (TextBox, ComboBox, DatePicker, labels) | UI 2.2 |
| 7 | **Build reusable `PagedListViewModelBase<T>`** — abstract ViewModel with paging, search, sort, load/refresh logic using `ApiClient` | UI 2.2 |
| 8 | **Implement Product List + Form** — first full CRUD screen, establish the pattern for all subsequent master data screens | UI 2.2 |
| 9 | **Add `PostMultipartAsync<T>` to `ApiClient`** — support `multipart/form-data` uploads for Import Wizard | UI 2.8 |
| 10 | **Create `VariantPickerControl`** — reusable UserControl with barcode scan / search / select variant, used in Purchase/Production/Sales/Return forms | UI 2.4 |
| 11 | **Build POS screen** — optimized for speed: barcode input → fast lookup → add line → running total → post → print hook | UI 2.5 |
| 12 | **Implement Draft→Post→Void state machine UI pattern** — reusable toolbar/button strip (Save Draft / Post / Void / Delete) that adapts to document status | UI 2.4 |
| 13 | **Register all new ViewModels + Pages in DI** — update `App.xaml.cs` `ConfigureServices` and add `DataTemplate` mappings as each phase delivers screens | Every phase |
| 14 | **Test at 3 DPI levels after each phase** — 100%, 125%, 150% manual check for layout breakage in new screens | Every phase |
| 15 | **Write UI 2.x REPORT after each sub-phase** — per project methodology: PLAN → implement → REPORT/CLOSEOUT before moving to next phase | Every phase |
