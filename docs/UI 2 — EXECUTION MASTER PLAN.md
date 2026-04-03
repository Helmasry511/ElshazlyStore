# UI 2 — EXECUTION MASTER PLAN

**Prepared by**: Claude OPUS 4.6 — PLAN MODE  
**Date**: 2026-03-02  
**Baseline**: UI 1 Foundation ✅ + UI 2.1 Auth+Navigation ✅  
**Scope**: Phase 0 (Contract Freeze) + UI 2.2 → UI 2.8 (7 execution phases)  
**Policy**: No invented facts. Unknown items marked **(غير مذكور في المدخلات)**.

---

## 1) Baseline Confirmed

### 1.1 UI 1 Foundation — ✅ COMPLETE (0 errors, 0 warnings)

| Deliverable | Evidence |
|-------------|----------|
| WPF Project `ElshazlyStore.Desktop` (`net8.0-windows`) | Separate from backend, no Domain/Infrastructure refs |
| App Shell: TopBar (48px) + Sidebar (220px) + Content region | MainWindow three-region layout, min 900×600, default 1200×780 |
| Per-Monitor V2 DPI awareness (`app.manifest`) | `UseLayoutRounding="True"` + `SnapsToDevicePixels="True"` on all |
| Dark/Light theme (runtime swap) | `DarkTheme.xaml`, `LightTheme.xaml`, all brushes via `DynamicResource`, persisted to `%LOCALAPPDATA%` |
| MVVM via `CommunityToolkit.Mvvm` | `ViewModelBase`, source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| DI container (`Microsoft.Extensions.DependencyInjection`) | All services/ViewModels registered in `App.xaml.cs` |
| `NavigationService` | Generic `NavigateTo<TViewModel>()`, DataTemplate-driven content swap |
| `ApiClient` (typed `HttpClient`) | `Get/Post/Put/DeleteAsync<T>`, base URL from `appsettings.json` |
| `AuthHeaderHandler` + `CorrelationIdHandler` | DelegatingHandlers in HTTP pipeline |
| `ProblemDetails` model + `ApiResult<T>` | RFC 7807 parsing, `ToUserMessage()` for error display |
| `IMessageService` | Standardized user-facing dialogs (error/info/confirm) |
| Serilog logging (rolling file + console) | 14-day retention, correlation IDs logged |
| Publish profiles (win-x64, win-x86) | Self-contained, single-file, no trimming |
| Home + Settings pages | Only business-free placeholder screens |

**Not included in UI 1**: No login UI, no business screens, no app icon, `InMemoryTokenStore` stub only.

### 1.2 UI 2.1 Auth + Core Navigation — ✅ COMPLETE (0 errors, 0 warnings)

| Deliverable | Evidence |
|-------------|----------|
| `LoginWindow` + `LoginViewModel` | Standalone window, username/password, async command, error display, busy state |
| `SecureTokenStore` (DPAPI) | `ProtectedData.Protect/Unprotect`, `DataProtectionScope.CurrentUser`, persisted to `%LOCALAPPDATA%\ElshazlyStore\tokens.dat`, thread-safe |
| `TokenRefreshHandler` | DelegatingHandler, 401 intercept, `SemaphoreSlim(1,1)`, separate `"AuthRefresh"` named HttpClient, static `SessionExpired` event |
| `ISessionService` / `SessionService` | Login, logout, `TryRestoreSession`, JWT `"permission"` claim parsing via `JwtClaimParser` |
| `IPermissionService` / `PermissionService` | `HasPermission` / `HasAll` / `HasAny`, case-insensitive |
| `PermissionCodes` class | 40+ static string constants mirroring backend |
| Permission-gated sidebar | 6 sections, 17 nav items, 25+ `[ObservableProperty]` bool flags on `MainViewModel` |
| Logout flow | `MainViewModel.LogoutAsync` → `SessionService` → API `/auth/logout` → clear tokens → show `LoginWindow` |
| `ShutdownMode="OnExplicitShutdown"` | Handles login ↔ main window transitions without app termination |
| `BoolToVisibilityConverter` + `InverseBoolToVisibilityConverter` | Registered in `SharedStyles.xaml` |
| App startup flow | `TryRestoreSession` → `/auth/me` success → MainWindow; fail → LoginWindow |

**15 new files created, 7 files modified in UI 2.1.**

### 1.3 Current File Inventory (42 files)

```
ElshazlyStore.Desktop/
├── App.xaml / App.xaml.cs                    (DI, startup, theme, login flow)
├── app.manifest / appsettings.json
├── ElshazlyStore.Desktop.csproj
├── Helpers/                                  (4 files: converters + template selector)
├── Models/                                   (6 files: ApiResult, ProblemDetails, PermissionCodes, Auth/*)
├── Resources/Themes/                         (3 files: Dark, Light, SharedStyles)
├── Services/                                 (14 files: navigation, theme, session, permission, message, preferences + Api/)
├── ViewModels/                               (5 files: Base, Main, Home, Settings, Login)
├── Views/                                    (8 files: LoginWindow, MainWindow + Pages/Home, Settings)
└── Properties/PublishProfiles/               (2 files: x64, x86)
```

### 1.4 Current NavigateTo + DI State

- `MainViewModel.NavigateTo(string)` handles only `"Home"` and `"Settings"`. All other cases fall through to `HomeViewModel`.
- DI registers: all services above + `MainViewModel`, `HomeViewModel`, `SettingsViewModel`, `LoginViewModel`, `MainWindow`, `LoginWindow`.

### 1.5 Execution Starts at UI 2.2

All code from here forward builds on the baseline above. No prior deliverables are re-implemented.

---

## 2) Phase 0 — Backend UI Contract Freeze (Gate قبل UI)

> **Purpose**: Before writing any UI screen code, freeze the exact contract surface so that UI development does not encounter moving targets.

### 2.0.1 What Must Be Verified / Frozen

| Contract Area | What to Freeze | Source of Truth |
|---------------|----------------|-----------------|
| **Endpoints** | Full route list, HTTP methods, request/response shapes | `api.md` (1246 lines, all endpoints documented) — **CONFIRMED present** |
| **DTOs** | All request/response record shapes, field names, types, nullability | `api.md` examples + `CODEBASE_INVENTORY.md` entity properties — **needs explicit DTO schema file** |
| **Permissions** | All 47 permission codes with descriptions | `api.md` Permissions section — **CONFIRMED: 47 codes** |
| **Error Codes** | All 65+ error codes with HTTP status + description | `api.md` Error Responses table — **CONFIRMED: 65+ codes** |
| **Paging Contract** | Standard `{ items, totalCount, page, pageSize }` shape | Most endpoints use this; Reason Codes / Print Profiles use anonymous paged object — **CONFIRMED discrepancy** |
| **Enum Values** | `MovementType`, `BarcodeStatus`, `PartyType`, `PaymentMethod`, `LedgerEntryType`, `DispositionType`, `ReasonCategory`, `PurchaseReceiptStatus`, `SalesInvoiceStatus`, `ProductionBatchStatus` | `CODEBASE_INVENTORY.md` + `api.md` — **CONFIRMED** |
| **Print Screen Codes** | Valid `screenCode` values for print policy lookup | **(غير مذكور في المدخلات — no official list; proposed: POS_RECEIPT, PURCHASE_RECEIPT, BARCODE_LABEL, SALES_RETURN_RECEIPT)** |

### 2.0.2 Known Contract Discrepancies to Resolve

| # | Discrepancy | Source A | Source B | Resolution Needed |
|---|-------------|----------|----------|-------------------|
| D1 | **PaymentMethod enum values** | `CODEBASE_INVENTORY.md`: `Cash, InstaPay, EWallet, Visa` | `api.md`: `Cash, Card, BankTransfer, EWallet, Cheque` | Verify actual enum in `Payment.cs` entity — code is truth |
| D2 | **Customer/Supplier name fields** | `api.md` + `CODEBASE_INVENTORY.md`: single `Name` field | `UI 2 PLAN REPORT`: proposed `NameAr, NameEn` | Backend has `Name` only — UI must use `Name` |
| D3 | **LedgerEntryType extended values** | `CODEBASE_INVENTORY.md`: `OpeningBalance, Invoice, Payment` | `api.md` RET phases: mentions `CreditNote, DebitNote` added | Verify actual enum — RET phases likely extended it |
| D4 | **Reason Codes paging shape** | `api.md`: returns `{ items, totalCount, page, pageSize }` | Standard list endpoints: use `PagedResult<T>` | UI `PagedListViewModelBase<T>` must handle both shapes |
| D5 | **Print Profiles paging** | Same anonymous paging as Reason Codes | Standard PagedResult | Same handling needed |

### 2.0.3 Phase 0 Deliverables

| Deliverable | Description |
|-------------|-------------|
| **DTO Verification Pass** | Read actual backend endpoint response shapes (run API or check Swagger) and confirm all field names/types match `api.md` |
| **Discrepancy Resolution** | Resolve D1–D5 above by checking actual code |
| **Frozen Contract Document** | Single reference table: Endpoint → Method → Request DTO → Response DTO → Permission → ErrorCodes (already 80% present in `api.md` + existing execution plan) |
| **UI Error Code Map** | Map all 65+ error codes to user-facing messages (extend existing `ProblemDetails.ToUserMessage()`) |

### 2.0.4 Gate Condition (Phase 0 → UI 2.2)

- [ ] All 5 discrepancies (D1–D5) resolved with confirmed values
- [ ] `api.md` confirmed as accurate against running backend (or Swagger)
- [ ] `ProblemDetails.ToUserMessage()` covers all 65+ error codes
- [ ] No blocking backend gaps for UI 2.2 scope

---

## 3) Phases UI 2.2 → UI 2.8

---

### Phase UI 2.2 — Master Data + Shared Styles

**Goal**: Build the first 10 CRUD screens (Products + Variants, Customers, Suppliers, Warehouses) and establish ALL reusable DataGrid/Form/Paging patterns for subsequent phases.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | `DataGridStyles.xaml` | Shared style — themed DataGrid (hover, alternating rows, header, Dark/Light) |
| D2 | `FormStyles.xaml` | Shared style — themed TextBox, ComboBox, DatePicker, Label, validation error template |
| D3 | `DialogStyles.xaml` | Shared style — modal confirmation/error dialog overlay |
| D4 | `PagedListViewModelBase<T>` | Abstract base — Items, Page, PageSize, TotalCount, Search, Sort, LoadAsync, next/prev commands. Must handle both `PagedResult<T>` and anonymous `{ items, totalCount }` |
| D5 | Product List | Paged DataGrid, search (name/category/desc), sort |
| D6 | Product Form | Create/Edit/Delete + embedded Variant management (sub-grid) |
| D7 | Customer List + Form | Paged DataGrid, CRUD, auto-code `CUST-NNNNNN` |
| D8 | Supplier List + Form | Same pattern as Customer |
| D9 | Warehouse List + Form | CRUD, code/name/address/isDefault |
| D10 | Parameterized navigation | Extend `INavigationService` with `NavigateTo<T>(Action<T> configure)` for edit scenarios |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Product List | `GET /products` (paged, q, sort) | `PRODUCTS_READ` | — |
| Product Form | `GET/POST/PUT/DELETE /products/{id}` | `PRODUCTS_READ`, `PRODUCTS_WRITE` | `NOT_FOUND`, `CONFLICT` |
| Variant (embedded) | `POST/PUT/DELETE /variants` | `PRODUCTS_WRITE` | `BARCODE_ALREADY_EXISTS`, `NOT_FOUND` |
| Customer List | `GET /customers` (paged, q, sort) | `CUSTOMERS_READ` | — |
| Customer Form | `GET/POST/PUT/DELETE /customers/{id}` | `CUSTOMERS_READ`, `CUSTOMERS_WRITE` | `NOT_FOUND`, `CONFLICT` |
| Supplier List | `GET /suppliers` (paged, q, sort) | `SUPPLIERS_READ` | — |
| Supplier Form | `GET/POST/PUT/DELETE /suppliers/{id}` | `SUPPLIERS_READ`, `SUPPLIERS_WRITE` | `NOT_FOUND`, `CONFLICT` |
| Warehouse List | `GET /warehouses` (paged, q) | `WAREHOUSES_READ` | — |
| Warehouse Form | `GET/POST/PUT/DELETE /warehouses/{id}` | `WAREHOUSES_READ`, `WAREHOUSES_WRITE` | `NOT_FOUND` |

#### DTO Shapes (from api.md + CODEBASE_INVENTORY)

```
ProductDto: Id, Name, Category?, Description?, IsActive, CreatedAtUtc, VariantCount
ProductDetailDto: (same) + Variants: List<VariantDto>
VariantDto: Id, ProductId, Sku, Barcode?, Color?, Size?, RetailPrice?, WholesalePrice?, IsActive, CreatedAtUtc
CustomerDto: Id, Code, Name, Phone?, Phone2?, Notes?, IsActive, CreatedAtUtc, UpdatedAtUtc?
SupplierDto: Id, Code, Name, Phone?, Phone2?, Notes?, IsActive, CreatedAtUtc, UpdatedAtUtc?
WarehouseDto: Id, Code, Name, Address?, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc?
```

> **Note**: Customer/Supplier entity has `Name` (single field), NOT `NameAr`/`NameEn` — confirmed from `api.md` and `CODEBASE_INVENTORY.md`.

#### Files to Create (~30 files)

- `Resources/Styles/DataGridStyles.xaml`, `FormStyles.xaml`, `DialogStyles.xaml`
- `ViewModels/PagedListViewModelBase.cs`
- `Models/Products/` (7 DTOs), `Models/Customers/` (3), `Models/Suppliers/` (3), `Models/Warehouses/` (3)
- `ViewModels/Products/ProductListViewModel.cs`, `ProductFormViewModel.cs`
- `ViewModels/Customers/CustomerListViewModel.cs`, `CustomerFormViewModel.cs`
- `ViewModels/Suppliers/SupplierListViewModel.cs`, `SupplierFormViewModel.cs`
- `ViewModels/Warehouses/WarehouseListViewModel.cs`, `WarehouseFormViewModel.cs`
- `Views/Pages/Products/`, `Views/Pages/Customers/`, `Views/Pages/Suppliers/`, `Views/Pages/Warehouses/` (8 pages × .xaml + .cs)

#### Files to Modify (~3 files)

- `App.xaml.cs` — register 8 new ViewModels + add style dictionaries
- `MainViewModel.cs` — add 8+ `case` entries to `NavigateTo()`
- `MainWindow.xaml` — add DataTemplates for all new ViewModels

#### Acceptance Criteria

- [ ] All 5 entity types have working list + form screens
- [ ] `PagedListViewModelBase<T>` proven: paging (next/prev/page), search (debounced), sort (column click)
- [ ] Create, edit, delete (soft-deactivate) work on all form screens
- [ ] `BARCODE_ALREADY_EXISTS` error shown correctly on variant form
- [ ] Permission-gated: screens hidden if user lacks READ; write buttons disabled/hidden if lacks WRITE
- [ ] DPI tested at 100%, 125%, 150% — no layout breakage on any screen
- [ ] Dark/Light theme switch — no visual glitches on new DataGrid/Form styles
- [ ] `dotnet build ElshazlyStore.sln` — 0 errors, 0 warnings
- [ ] All existing tests pass (`dotnet test`)

#### Dependencies & Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| DataGrid column sizing at high DPI | Clipping/overlapping at 150% | Use Star/Auto column widths, no fixed pixel widths |
| Product form with embedded variants is complex | Scope creep on first screen | Build product list first (prove pattern), then form |

---

### Phase UI 2.3 — Inventory & Stock

**Goal**: Stock balance browser, stock ledger viewer, manual stock movement posting. Introduce `StatusBadgeStyles.xaml` and `DecimalFormatConverter`.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | `StatusBadgeStyles.xaml` | Shared style — Draft (grey), Posted (green), Voided (red), Approved (blue) |
| D2 | `DecimalFormatConverter` | Helper — formats `1234.50` → `1,234.50` |
| D3 | Stock Balances | Paged DataGrid, warehouse ComboBox filter, search by SKU/product name |
| D4 | Stock Ledger | Paged DataGrid, filter by variant/warehouse/date range/movement type |
| D5 | Stock Movement Posting | Form: movement type selector, reference, editable line grid (variant picker, warehouse, direction, qty, cost), Post button |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Stock Balances | `GET /stock/balances` (warehouseId, q, page, sort) | `STOCK_READ` | — |
| Stock Ledger | `GET /stock/ledger` (variantId, warehouseId, from, to, page) | `STOCK_READ` | — |
| Stock Movement | `POST /stock-movements/post` | `STOCK_POST` | `STOCK_NEGATIVE_NOT_ALLOWED`, `MOVEMENT_EMPTY`, `WAREHOUSE_NOT_FOUND`, `VARIANT_NOT_FOUND`, `TRANSFER_UNBALANCED` |

#### DTO Shapes

```
BalanceRow: VariantId, Sku, Color?, Size?, ProductName, Barcode?,
            WarehouseId, WarehouseCode, WarehouseName, Quantity, LastUpdatedUtc
LedgerRow: MovementId, Type, Reference?, PostedAtUtc, PostedByUsername,
           VariantId, Sku, WarehouseId, WarehouseCode, QuantityDelta, UnitCost?, Reason?
PostRequest: Type (int), Reference?, Notes?, Lines: List<PostLineRequest>
PostLineRequest: VariantId, WarehouseId, QuantityDelta, UnitCost?, Reason?
MovementType enum: OpeningBalance=0, PurchaseReceipt=1, SaleIssue=2, Transfer=3,
                   Adjustment=4, ProductionConsume=5, ProductionProduce=6
```

#### Files to Create (~15 files)

- `Resources/Styles/StatusBadgeStyles.xaml`, `Helpers/DecimalFormatConverter.cs`
- `Models/Stock/` (BalanceRow, LedgerRow, PostRequest, PostLineRequest, MovementType enum)
- `ViewModels/Stock/StockBalanceViewModel.cs`, `StockLedgerViewModel.cs`, `StockMovementViewModel.cs`
- `Views/Pages/Stock/` (3 pages × .xaml + .cs)

#### Acceptance Criteria

- [ ] Balance screen shows correct stock per variant per warehouse
- [ ] Warehouse filter + search work on balance screen
- [ ] Ledger shows full movement history with variant/warehouse/date range filters
- [ ] Stock posting: OpeningBalance adds stock, Adjustment modifies, Transfer requires balanced In/Out lines
- [ ] `STOCK_NEGATIVE_NOT_ALLOWED` error displayed clearly with variant/warehouse context
- [ ] `StatusBadgeStyles` render correctly in both Dark/Light themes
- [ ] Permission-gated: `STOCK_READ`, `STOCK_POST`
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| Depends on UI 2.2 | `PagedListViewModelBase<T>`, `DataGridStyles.xaml`, `FormStyles.xaml` must exist |
| Transfer form UX | Complex (source+destination, balanced lines) — may need guided sub-form |

---

### Phase UI 2.4 — Procurement & Production

**Goal**: Purchase receipt and production batch workflows with Draft→Post lifecycle. Introduce `VariantPickerControl` and Draft/Post toolbar pattern.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | `VariantPickerControl` | Reusable UserControl — barcode scan + search-by-SKU/name, emits selected VariantId/Sku/ProductName |
| D2 | Draft→Post→Void toolbar pattern | Reusable button strip: Save (Draft), Post (Draft+POST perm), Delete (Draft), status-dependent |
| D3 | Purchase List | Paged DataGrid, status filter (Draft/Posted), search by document number |
| D4 | Purchase Form | Create draft → add lines via VariantPicker → save → post. Immutable after post |
| D5 | Production List | Paged DataGrid, status filter (Draft/Posted) |
| D6 | Production Form | Input lines + Output lines → save → post → two movements created |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Purchase List | `GET /purchases` (q, page, sort) | `PURCHASES_READ` | — |
| Purchase Form | `GET/POST/PUT/DELETE /purchases/{id}`, `POST /purchases/{id}/post` | `PURCHASES_READ`, `PURCHASES_WRITE`, `PURCHASES_POST` | `PURCHASE_RECEIPT_NOT_FOUND`, `PURCHASE_RECEIPT_ALREADY_POSTED`, `PURCHASE_RECEIPT_EMPTY`, `SUPPLIER_NOT_FOUND`, `DOCUMENT_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` |
| Production List | `GET /production` (q, page, sort) | `PRODUCTION_READ` | — |
| Production Form | `GET/POST/PUT/DELETE /production/{id}`, `POST /production/{id}/post` | `PRODUCTION_READ`, `PRODUCTION_WRITE`, `PRODUCTION_POST` | `PRODUCTION_BATCH_NOT_FOUND`, `PRODUCTION_BATCH_ALREADY_POSTED`, `PRODUCTION_BATCH_NO_INPUTS`, `PRODUCTION_BATCH_NO_OUTPUTS`, `BATCH_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` |

#### DTO Shapes

```
-- Purchase --
CreatePurchaseRequest: SupplierId, WarehouseId, DocumentNumber?, Notes?,
                       Lines: List<{ VariantId, Quantity, UnitCost }>
ReceiptDto: Id, DocumentNumber, ReceiptDateUtc, SupplierId, SupplierName,
            WarehouseId, WarehouseName, CreatedByUsername, Notes?, Status,
            StockMovementId?, TotalAmount, CreatedAtUtc, PostedAtUtc?,
            Lines: List<{ Id, VariantId, Sku, ProductName?, Quantity, UnitCost, LineTotal }>
PostResult: StockMovementId

-- Production --
CreateProductionRequest: WarehouseId, BatchNumber?, Notes?,
                         Inputs: List<{ VariantId, Quantity }>,
                         Outputs: List<{ VariantId, Quantity, UnitCost? }>
BatchDto: Id, BatchNumber, ProductionDateUtc, WarehouseId, WarehouseName,
          CreatedByUsername, Notes?, Status, ConsumeMovementId?, ProduceMovementId?,
          TotalInputCost, TotalOutputCost, CreatedAtUtc, PostedAtUtc?,
          InputLines: List<BatchLineDto>, OutputLines: List<BatchLineDto>
PostResult: ConsumeMovementId, ProduceMovementId
```

**Posting semantics**: Idempotent (already-posted returns 200 with existing movement IDs). TOCTOU-safe (`UPDATE WHERE Status=Draft`). Concurrent conflicts return `409 POST_CONCURRENCY_CONFLICT`.

#### Files to Create (~20 files)

- `Views/Controls/VariantPickerControl.xaml/.cs`
- `Models/Purchases/` (all DTOs), `Models/Production/` (all DTOs)
- `ViewModels/Purchases/PurchaseListViewModel.cs`, `PurchaseFormViewModel.cs`
- `ViewModels/Production/ProductionListViewModel.cs`, `ProductionFormViewModel.cs`
- `Views/Pages/Purchases/`, `Views/Pages/Production/` (4 pages × .xaml+.cs)

#### Acceptance Criteria

- [ ] Purchase: create draft → add lines via VariantPicker → save → post → stock increases (verified in balance screen)
- [ ] Production: create draft → input lines + output lines → post → consume + produce movements
- [ ] `ALREADY_POSTED` error handled gracefully (Post button disabled on Posted status)
- [ ] Concurrency conflict shows "Record was modified by another user, please reload" with reload action
- [ ] `VariantPickerControl` reused in both Purchase and Production forms
- [ ] Cannot edit/delete Posted documents (buttons disabled, fields read-only)
- [ ] Separate READ/WRITE/POST permissions respected on each screen
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| Depends on UI 2.2 | Shared styles, `PagedListViewModelBase<T>`, parameterized navigation |
| Depends on UI 2.3 | `StatusBadgeStyles.xaml` for Draft/Posted badges |
| `VariantPickerControl` is critical path | Used in 4 future phases — must be robust and fast |

---

### Phase UI 2.5 — POS & Sales

**Goal**: Fast POS screen with barcode scanner, sales list/form, print hook integration. Most performance-sensitive phase.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | POS Screen | Full-content-area optimized for speed: barcode input, line grid, running total, customer/warehouse selectors |
| D2 | Keyboard-wedge barcode scanner | Detect rapid keystrokes → on Enter → `GET /barcodes/{barcode}` → add line |
| D3 | Sales List | Paged DataGrid, status filter (Draft/Posted) |
| D4 | Sales Form | View/edit draft invoice (non-POS editing) |
| D5 | `IPrintService` / `PrintService` | WPF PrintDialog integration, receipt rendering from `ConfigJson` |
| D6 | POS Receipt print hook | After post → query `GET /print-policy/POS_RECEIPT` → if enabled → print |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| POS | `GET /barcodes/{barcode}`, `POST /sales`, `PUT /sales/{id}`, `POST /sales/{id}/post`, `GET /print-policy/{screenCode}` | `SALES_WRITE`, `SALES_POST`, `PRODUCTS_READ` | `SALES_INVOICE_EMPTY`, `SALES_INVOICE_ALREADY_POSTED`, `STOCK_NEGATIVE_NOT_ALLOWED`, `CUSTOMER_NOT_FOUND`, `POST_CONCURRENCY_CONFLICT` |
| POS Barcode | `GET /barcodes/{barcode}` | `PRODUCTS_READ` (cached 60s server-side) | `404` = not found, `410` = `BARCODE_RETIRED` |
| Sales List | `GET /sales` (paged) | `SALES_READ` | — |
| Sales Form | `GET/PUT/DELETE /sales/{id}`, `POST /sales/{id}/post` | `SALES_READ`, `SALES_WRITE`, `SALES_POST` | Same as POS |

#### DTO Shapes

```
BarcodeLookupResult: Barcode, Status, VariantId, Sku, Color?, Size?,
                     RetailPrice?, WholesalePrice?, IsActive,
                     ProductId, ProductName, ProductCategory?

CreateSalesInvoiceRequest: WarehouseId, CustomerId?, Notes?,
                           Lines: List<{ VariantId, Quantity, UnitPrice, DiscountAmount? }>
InvoiceDto: Id, InvoiceNumber, InvoiceDateUtc, CustomerId?, CustomerName?,
            WarehouseId, WarehouseName, CashierUserId, CashierUsername,
            Notes?, Status, StockMovementId?, TotalAmount, CreatedAtUtc, PostedAtUtc?,
            Lines: List<{ Id, VariantId, Sku, ProductName?, Quantity, UnitPrice, DiscountAmount, LineTotal }>
```

#### POS UX Requirements

- Auto-focused barcode TextBox at top of screen
- Keyboard-wedge scanner detection: rapid keystrokes → on Enter → call barcode lookup → add/increment line
- Line grid: SKU, Product, Qty (editable), Unit Price (editable), Discount, Line Total
- Running total display (large font)
- Customer selector (optional, ComboBox with search) — walk-in sale if omitted
- Warehouse selector
- Post button → atomic create+post → triggers print hook
- **Performance target**: Sub-second barcode lookup, POS startup < 1s

#### Print Hook

After successful post: query `GET /print-policy/POS_RECEIPT?profileId=...` → if rule exists and enabled → render receipt using WPF `PrintDialog` + `ConfigJson` template.

> **(غير مذكور في المدخلات: exact receipt template schema in ConfigJson — no JSON schema defined for POS_RECEIPT config. Use sensible defaults: invoice number, date, cashier, item lines, total.)**

#### Files to Create (~15 files)

- `Models/Sales/` (InvoiceDto, CreateSalesInvoiceRequest, etc.)
- `Models/Barcodes/BarcodeLookupResult.cs`
- `ViewModels/Sales/PosViewModel.cs`, `SalesListViewModel.cs`, `SalesFormViewModel.cs`
- `Views/Pages/Sales/PosPage.xaml/.cs`, `SalesListPage.xaml/.cs`, `SalesFormPage.xaml/.cs`
- `Services/IPrintService.cs`, `Services/PrintService.cs`

#### Acceptance Criteria

- [ ] Barcode scan adds line in < 500ms
- [ ] Walk-in sale (no customer) works end-to-end: scan → post → stock decreases
- [ ] Customer-linked sale works: post → stock decreases → AR ledger entry created
- [ ] Receipt prints if print rule configured for `POS_RECEIPT` screen code
- [ ] `STOCK_NEGATIVE_NOT_ALLOWED` shown if insufficient stock
- [ ] Retired barcode (410) shows clear "Barcode retired" message
- [ ] POS startup < 1s (no heavy preloads)
- [ ] Sales list: search, status filter, paging work
- [ ] Sales form: edit draft, post, delete draft work
- [ ] Permissions: `SALES_WRITE`, `SALES_POST`, `SALES_READ`, `PRODUCTS_READ` (barcode lookup)
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| Depends on UI 2.4 | `VariantPickerControl` (reused for non-barcode line adding) |
| Barcode scanner hardware | Keyboard-wedge is assumed — no USB HID driver needed |
| Print integration | WPF `PrintDialog` API — printer must be configured on OS. **(غير مذكور: receipt paper size — assumed 80mm thermal)** |
| POS performance | Most latency-sensitive screen — test with real barcode scan timing |

---

### Phase UI 2.6 — Returns & Dispositions

**Goal**: Sales returns, purchase returns, dispositions with reason codes and the full Draft→Approve→Post→Void lifecycle. Most complex phase by permission count.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | Reason Code List | CRUD grid, filter by category (General/SalesReturn/PurchaseReturn/Disposition), disable |
| D2 | Sales Return List | Paged DataGrid, status filter (Draft/Posted/Voided) |
| D3 | Sales Return Form | Draft → Post / Void. Lines require reason code + disposition type. Qty validation vs original invoice |
| D4 | Purchase Return List | Paged DataGrid, status filter (Draft/Posted/Voided) |
| D5 | Purchase Return Form | Draft → Post / Void. Supplier required. `ReturnToVendor` default disposition |
| D6 | Disposition List | Paged DataGrid, status filter (Draft/Approved/Posted/Voided) |
| D7 | Disposition Form | Draft → Approve (if required) → Post / Void. Manager approval gate |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Reason Codes | `GET/POST/PUT /reasons`, `POST /reasons/{id}/disable` | `VIEW_REASON_CODES`, `MANAGE_REASON_CODES` | `REASON_CODE_NOT_FOUND`, `REASON_CODE_ALREADY_EXISTS`, `REASON_CODE_IN_USE` |
| Sales Returns | `GET/POST/PUT/DELETE /sales-returns/{id}`, `POST /{id}/post`, `POST /{id}/void` | `VIEW_SALES_RETURNS`, `SALES_RETURN_CREATE`, `SALES_RETURN_POST`, `SALES_RETURN_VOID` | `SALES_RETURN_NOT_FOUND`, `SALES_RETURN_ALREADY_POSTED`, `SALES_RETURN_EMPTY`, `RETURN_QTY_EXCEEDS_SOLD`, `REASON_CODE_INACTIVE`, `SALES_RETURN_ALREADY_VOIDED`, `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST`, `SALES_RETURN_DISPOSITION_NOT_ALLOWED` |
| Purchase Returns | `GET/POST/PUT/DELETE /purchase-returns/{id}`, `POST /{id}/post`, `POST /{id}/void` | `VIEW_PURCHASE_RETURNS`, `PURCHASE_RETURN_CREATE`, `PURCHASE_RETURN_POST`, `PURCHASE_RETURN_VOID` | `PURCHASE_RETURN_NOT_FOUND`, `PURCHASE_RETURN_ALREADY_POSTED`, `PURCHASE_RETURN_EMPTY`, `RETURN_QTY_EXCEEDS_RECEIVED`, `PURCHASE_RETURN_ALREADY_VOIDED`, `PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST`, `PURCHASE_RETURN_NUMBER_EXISTS` |
| Dispositions | `GET/POST/PUT/DELETE /dispositions/{id}`, `POST /{id}/approve`, `POST /{id}/post`, `POST /{id}/void` | `VIEW_DISPOSITIONS`, `DISPOSITION_CREATE`, `DISPOSITION_APPROVE`, `DISPOSITION_POST`, `DISPOSITION_VOID` | `DISPOSITION_NOT_FOUND`, `DISPOSITION_ALREADY_POSTED`, `DISPOSITION_EMPTY`, `DISPOSITION_ALREADY_VOIDED`, `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST`, `DISPOSITION_REQUIRES_APPROVAL`, `DISPOSITION_INVALID_TYPE`, `DESTINATION_WAREHOUSE_NOT_FOUND`, `DISPOSITION_NUMBER_EXISTS` |

#### Status Flows (from api.md)

```
Sales Return:     Draft → Posted  |  Draft → Voided
Purchase Return:  Draft → Posted  |  Draft → Voided
Disposition:      Draft → Posted  |  Draft → Voided
                  (with optional Approve gate before Post when reason requires it)
```

- **Void is Draft-only** for all three types. Void on Posted returns `409 *_VOID_NOT_ALLOWED_AFTER_POST`.
- **Disposition Manager Approval**: If any line's reason code has `requiresManagerApproval=true`, posting is blocked until `POST /{id}/approve` is called. Updating lines after approval **clears** the approval.

#### Allowed Dispositions by Context

| Context | Allowed Disposition Types |
|---------|--------------------------|
| **Sales Return (RET 1)** | `ReturnToStock` (3), `Quarantine` (4) only. Others return `400 SALES_RETURN_DISPOSITION_NOT_ALLOWED` |
| **Purchase Return (RET 2)** | `ReturnToVendor` (2) — all lines default to this |
| **Pre-sale Disposition (RET 3)** | `Scrap` (0), `Quarantine` (4), `WriteOff` (5), `Rework` (1). `ReturnToVendor`/`ReturnToStock` return `400 DISPOSITION_INVALID_TYPE` |

#### Posting Effects (from api.md)

- **Sales Return Post**: Creates `SaleReturnReceipt` stock movement (+qty for ReturnToStock/Quarantine). If customer exists → creates `CreditNote` ledger entry (negative = reduces outstanding).
- **Purchase Return Post**: Creates `PurchaseReturnIssue` stock movement (−qty from warehouse). Creates `DebitNote` ledger entry (negative = reduces supplier outstanding).
- **Disposition Post**: Creates `Disposition` stock movement. Per line: negative on source warehouse + positive on destination special warehouse (SCRAP/QUARANTINE/REWORK). `WriteOff` has no positive delta.

#### Special Warehouses (auto-seeded)

| Warehouse | Purpose |
|-----------|---------|
| `QUARANTINE` | Items pending inspection |
| `SCRAP` | Scrapped items |
| `REWORK` | Items sent for repair |

#### Files to Create (~25 files)

- `Models/ReasonCodes/ReasonCodeDto.cs`, `CreateReasonCodeRequest.cs`, `UpdateReasonCodeRequest.cs`
- `Models/Returns/` (Sales Return DTOs, Purchase Return DTOs)
- `Models/Dispositions/` (Disposition DTOs)
- `Models/Enums/DispositionType.cs`, `ReasonCategory.cs`
- `ViewModels/Returns/SalesReturnList/FormViewModel.cs`, `PurchaseReturnList/FormViewModel.cs`
- `ViewModels/Returns/ReasonCodeListViewModel.cs`
- `ViewModels/Returns/DispositionList/FormViewModel.cs`
- `Views/Pages/Returns/` (7 pages × .xaml+.cs)

#### Acceptance Criteria

- [ ] Reason code CRUD works; filter by category; disable (no hard delete)
- [ ] Sales return: create draft with lines (reason code + disposition type) → post → stock increases → credit note created (if customer)
- [ ] Purchase return: create draft → post → stock decreases → debit note created
- [ ] Disposition: create draft → approve (if required) → post → stock moves to special warehouse
- [ ] Void works on Draft status only; `ALREADY_VOIDED` and `VOID_NOT_ALLOWED_AFTER_POST` handled
- [ ] `RETURN_QTY_EXCEEDS_SOLD` / `RETURN_QTY_EXCEEDS_RECEIVED` errors shown clearly
- [ ] Disposition approval clears if lines edited after approval **(confirmed from api.md)**
- [ ] Reason code + disposition type dropdowns populated correctly, filtered by context
- [ ] `DISPOSITION_INVALID_TYPE` and `SALES_RETURN_DISPOSITION_NOT_ALLOWED` errors handled
- [ ] All 14 return/disposition permissions respected (VIEW/CREATE/POST/VOID/APPROVE)
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| Depends on UI 2.4 | `VariantPickerControl` for return/disposition line items |
| Depends on UI 2.3 | `StatusBadgeStyles.xaml` for Draft/Posted/Voided/Approved badges |
| Most complex permission model | 14 separate permission codes across 4 screen areas |
| CreditNote/DebitNote ledger types | Must verify `LedgerEntryType` enum includes these (discrepancy D3) |

---

### Phase UI 2.7 — Accounting & Payments

**Goal**: AR/AP balance views, party ledger drill-down, payment creation/listing. Navigation flow: Balances → Ledger → Create Payment.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | Customer Balances | Paged DataGrid — party name, code, debits, credits, outstanding |
| D2 | Supplier Balances | Same pattern as customer balances |
| D3 | Party Ledger Viewer | Header (party name + outstanding) + paged entries. Button to create payment |
| D4 | Payment Form | Party type toggle, party selector, amount, method ComboBox, conditional wallet name |
| D5 | Payment List | Paged DataGrid with party type/id filters |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Balances | `GET /accounting/balances/customers`, `/suppliers`, `/{partyType}/{partyId}` | `ACCOUNTING_READ` | — |
| Ledger | `GET /accounting/ledger/{partyType}/{partyId}` (paged) | `ACCOUNTING_READ` | — |
| Payment Form | `POST /payments` | `PAYMENTS_WRITE` | `OVERPAYMENT_NOT_ALLOWED`, `WALLET_NAME_REQUIRED`, `INVALID_PAYMENT_METHOD`, `INVALID_PARTY_TYPE`, `PARTY_NOT_FOUND` |
| Payment List | `GET /payments` (paged, partyType, partyId) | `PAYMENTS_READ` | `PAYMENT_NOT_FOUND` |

#### DTO Shapes

```
PartyBalanceDto: PartyId, PartyName, PartyCode, PartyType,
                 TotalDebits, TotalCredits, Outstanding

LedgerEntryDto: Id, PartyType, PartyId, PartyName, EntryType, Amount,
                Reference?, Notes?, RelatedInvoiceId?, RelatedPaymentId?,
                CreatedAtUtc, CreatedByUserId, CreatedByUsername

CreatePaymentRequest: PartyType (Customer/Supplier), PartyId, Amount (>0),
                      Method, WalletName?, Reference?, PaymentDateUtc?

PaymentDto: Id, PartyType, PartyId, PartyName, Amount, Method, WalletName?,
            Reference?, PaymentDateUtc, CreatedAtUtc, CreatedByUserId, CreatedByUsername
```

> **PaymentMethod enum**: Verify actual values before implementation (discrepancy D1). Code entity shows `Cash, InstaPay, EWallet, Visa`.

#### Navigation Flow

```
Balances Tab → Click row → Party Ledger (pre-filled partyType + partyId)
                            → "Record Payment" button → Payment Form (pre-filled party)
```

#### Files to Create (~12 files)

- `Models/Accounting/PartyBalanceDto.cs`, `LedgerEntryDto.cs`, `PaymentDto.cs`, `CreatePaymentRequest.cs`
- `Models/Enums/PartyType.cs`, `PaymentMethod.cs`
- `ViewModels/Accounting/BalancesViewModel.cs`, `LedgerViewModel.cs`, `PaymentFormViewModel.cs`, `PaymentListViewModel.cs`
- `Views/Pages/Accounting/` (4 pages × .xaml+.cs)

#### Acceptance Criteria

- [ ] Balances show correct outstanding amounts (Debits − Credits, including CreditNote/DebitNote adjustments)
- [ ] Two tabs: Customers / Suppliers — each with paged list + search
- [ ] Click balance row → navigates to party ledger
- [ ] Ledger shows all entry types (Invoice, Payment, OpeningBalance, CreditNote, DebitNote)
- [ ] Payment creation: overpayment blocked (`OVERPAYMENT_NOT_ALLOWED`), wallet name enforced for EWallet
- [ ] Payment pre-filled from ledger screen navigation
- [ ] Permissions: `ACCOUNTING_READ`, `PAYMENTS_READ`, `PAYMENTS_WRITE`
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| Depends on UI 2.6 | CreditNote/DebitNote entries must exist for return-adjusted balances |
| PaymentMethod discrepancy | D1 must be resolved in Phase 0 |

---

### Phase UI 2.8 — Dashboard, Import, Print Config, Admin

**Goal**: Final phase. Dashboard KPIs, import wizard (3-step), print profile management, user/role administration.

#### Deliverables

| # | Screen/Component | Type |
|---|------------------|------|
| D1 | `PostMultipartAsync<T>` on `ApiClient` | PREREQUISITE — multipart/form-data upload for import |
| D2 | Dashboard | KPI cards + top products table + low-stock alerts + cashier performance. Date range picker |
| D3 | Import Wizard | 3-step: select type + upload → preview (errors) → commit |
| D4 | Print Profile List | CRUD for profiles |
| D5 | Print Rule Form | Nested rules per profile: screen code, ConfigJson, enabled toggle |
| D6 | User List | DataGrid: username, active, roles, created. NOT paged (small dataset) |
| D7 | User Form | Create/edit: username, password, isActive, assign roles |
| D8 | Role List | DataGrid: name, description, permission count. NOT paged |
| D9 | Role Form | Create/edit role + checklist of ALL 47 permissions |

#### APIs + Permissions + ErrorCodes

| Screen | Endpoints | Permissions | Key ErrorCodes |
|--------|-----------|-------------|----------------|
| Dashboard | `GET /dashboard/summary`, `/sales`, `/top-products`, `/low-stock`, `/cashier-performance` | `DASHBOARD_READ` | — |
| Import | `POST /imports/masterdata/preview`, `/commit`, `/opening-balances/*`, `/payments/*` | `IMPORT_MASTER_DATA`, `IMPORT_OPENING_BALANCES`, `IMPORT_PAYMENTS` | `IMPORT_PREVIEW_FAILED`, `IMPORT_COMMIT_FAILED`, `IMPORT_JOB_NOT_FOUND`, `IMPORT_JOB_ALREADY_COMMITTED` |
| Print Profiles | `GET/POST/PUT/DELETE /print-profiles/{id}`, rules CRUD nested | `MANAGE_PRINTING_POLICY` | `PRINT_PROFILE_NOT_FOUND`, `PRINT_PROFILE_NAME_EXISTS`, `PRINT_RULE_NOT_FOUND`, `PRINT_RULE_SCREEN_EXISTS` |
| Users | `GET/POST/PUT/DELETE /users/{id}` | `USERS_READ`, `USERS_WRITE` | `NOT_FOUND`, `CONFLICT` |
| Roles | `GET/POST/PUT/DELETE /roles/{id}`, `GET/PUT /roles/{id}/permissions`, `GET /roles/permissions/all` | `ROLES_READ`, `ROLES_WRITE` | `NOT_FOUND` |

#### Dashboard DTO Shapes

```
DashboardSummaryDto:
  Sales: { TotalSales, InvoiceCount, AverageTicket, TotalSalesReturns, ReturnCount, NetSales, NetAverageTicket }
  TopProductsByQuantity: List<TopProductDto>
  TopProductsByRevenue: List<TopProductDto>
  LowStockAlerts: List<LowStockAlertDto>
  CashierPerformance: List<CashierPerformanceDto>
  DispositionLoss: decimal

TopProductDto: ProductId, ProductName, VariantId, Sku, Color?, Size?,
               TotalQuantity, TotalRevenue, ReturnedQuantity, ReturnedRevenue, NetQuantity, NetRevenue
LowStockAlertDto: VariantId, Sku, ProductName, Color?, Size?,
                  WarehouseId, WarehouseCode, WarehouseName, CurrentStock, Threshold
CashierPerformanceDto: CashierUserId, CashierUsername, InvoiceCount, TotalSales, AverageTicket
```

> **Chart library**: **(غير مذكور في المدخلات)** — Use simple WPF data cards/tables. No third-party charting library unless user specifies.

#### Import Wizard Details

- Step 1: ComboBox (type: Products/Customers/Suppliers/OpeningBalances/Payments) + OpenFileDialog (`.csv`, `.xlsx`)
- Step 2: Preview → show TotalRows, ValidRows, row errors DataGrid (row#, column, message)
- Step 3: Commit button → imported count or error
- Content-Type: `multipart/form-data` — requires new `PostMultipartAsync<T>` on `ApiClient`
- Increase HttpClient timeout for import requests (large files)

#### User/Role Special Notes

- `GET /users` returns `List<UserDto>` — NOT paged. Use simple `ObservableCollection` (no `PagedListViewModelBase`).
- `GET /roles` returns `List<RoleDto>` — NOT paged.
- Role form: load all permissions via `GET /roles/permissions/all` → display as checklist (47 checkboxes). Save via `PUT /roles/{id}/permissions` with `{ permissionIds: [...] }`.
- DELETE `/users/{id}` is soft-delete (sets `IsActive=false`).

#### Print Profiles/Rules Special Notes

- Profile/Rule list responses use anonymous paged object `{ items, totalCount, page, pageSize }`, not `PagedResult<T>`.
- `ConfigJson` is freeform text — edited as raw JSON text field. **(غير مذكور في المدخلات: no schema defined)**
- One rule per `(profileId, screenCode)` — unique constraint enforced server-side.

#### Files to Create (~25 files)

- Add `PostMultipartAsync<T>` to `Services/Api/ApiClient.cs` (modification)
- `Models/Dashboard/`, `Models/Import/`, `Models/Printing/`, `Models/Admin/`
- ViewModels: `DashboardViewModel`, `ImportWizardViewModel`, `PrintProfileListViewModel`, `PrintRuleFormViewModel`, `UserListViewModel`, `UserFormViewModel`, `RoleListViewModel`, `RoleFormViewModel`
- `Views/Pages/Dashboard/`, `Views/Pages/Import/`, `Views/Pages/Printing/`, `Views/Pages/Admin/` (9 pages × .xaml+.cs)

#### Acceptance Criteria

- [ ] Dashboard KPIs correct for selected date range; date range defaults to current month
- [ ] Top products toggle between by-quantity and by-revenue
- [ ] Low stock alerts display using variant-level threshold when available, else global fallback
- [ ] Import: file upload via OpenFileDialog works, preview shows row errors, commit imports data
- [ ] `IMPORT_JOB_ALREADY_COMMITTED` handled with clear message
- [ ] Print profiles: CRUD works; nested rules display correctly
- [ ] Print rules: `PRINT_RULE_SCREEN_EXISTS` error shown when duplicate screen code
- [ ] Users: CRUD + role assignment works; deactivation (soft-delete) works
- [ ] Roles: CRUD works; all 47 permissions shown as checkboxes; toggle on/off saves correctly
- [ ] All screens permission-gated (`DASHBOARD_READ`, `IMPORT_*`, `MANAGE_PRINTING_POLICY`, `USERS_*`, `ROLES_*`)
- [ ] `dotnet build` — 0 errors, 0 warnings; all tests pass

#### Dependencies & Risks

| Item | Note |
|------|------|
| `PostMultipartAsync<T>` must be built first | Blocking for Import Wizard |
| Large import files (10K+ rows) | May timeout — set longer HttpClient timeout for import endpoint |
| 47-permission checklist UX | Needs grouping by category for usability |
| Dashboard date range queries | Server-side aggregation performance depends on data volume |

---

## 4) Global UI Patterns (إلزامي إعادة استخدامه)

### 4.1 MVVM Structure (per screen)

Every screen follows this mandatory pattern:

```
1. Model     → Models/{Domain}/XxxDto.cs        (C# record mirroring server DTO exactly)
2. ViewModel → ViewModels/{Domain}/XxxViewModel.cs  (inherits ViewModelBase or PagedListViewModelBase<T>)
3. View      → Views/Pages/{Domain}/XxxPage.xaml    (UserControl, zero code-behind logic)
4. Registration:
   a. DI transient in App.xaml.cs → ConfigureServices()
   b. DataTemplate in MainWindow.xaml.Resources mapping ViewModel → Page
   c. case in MainViewModel.NavigateTo() switch
```

### 4.2 PagedListViewModelBase\<T\> (introduced UI 2.2)

```
Abstract class providing:
├── ObservableCollection<T> Items
├── int Page, PageSize (default 25), TotalCount, TotalPages (computed)
├── string SearchText, SortColumn (default varies per screen)
├── bool IsLoading
├── abstract string Endpoint { get; }
├── async Task LoadAsync()
│   └── calls ApiClient.GetAsync, parses either PagedResult<T> or anonymous { items, totalCount } shape
├── RelayCommand: NextPageCommand, PrevPageCommand, RefreshCommand
├── SearchCommand with CancellationTokenSource (debounced ~300ms)
└── OnNavigatedTo() auto-calls LoadAsync()
```

**All list screens inherit this**: Products, Customers, Suppliers, Warehouses, Stock Balances, Stock Ledger, Purchases, Production, Sales, Sales Returns, Purchase Returns, Dispositions, Reason Codes, Balances, Payments, Print Profiles.

**Exception**: Users and Roles lists are NOT paged (small datasets) — use simple `ObservableCollection` directly.

### 4.3 Paging/Search/Sort Pattern

| Aspect | Implementation |
|--------|---------------|
| **Paging** | `Page` & `PageSize` query params. UI: prev/next buttons + page indicator ("Page 2 of 5"). Max `pageSize=100` |
| **Search** | `q` query param. UI: TextBox with debounced search (300ms after last keystroke). Clears page to 1 on new search |
| **Sort** | `sort` query param (e.g., `name`, `name_desc`, `created`). UI: DataGrid column header click toggles asc/desc |
| **Dual paging shape** | Some endpoints (Reason Codes, Print Profiles) return `{ items, totalCount }` without `page`/`pageSize` in response. `PagedListViewModelBase` must handle both |

### 4.4 ProblemDetails + ErrorCodes Mapping

```
ApiClient response handling:
├── 2xx → ApiResult<T>.Success(data) → ViewModel shows success / navigates / refreshes
├── 400 (VALIDATION_FAILED) → ApiResult<T>.Failure + Parse errors dict → field-level red borders
├── 401 → TokenRefreshHandler retries once → fails: SessionExpired → LoginWindow
├── 403 (FORBIDDEN) → IMessageService.ShowError("You don't have permission")
├── 404 (*_NOT_FOUND) → "Record not found or has been deleted"
├── 409 (CONFLICT / POST_CONCURRENCY_CONFLICT) → "Record was modified by another user"
├── 410 (BARCODE_RETIRED) → "This barcode has been retired"
├── 422 (business rules) → Show ProblemDetails.Detail directly:
│   ├── STOCK_NEGATIVE_NOT_ALLOWED → "Insufficient stock"
│   ├── OVERPAYMENT_NOT_ALLOWED → "Payment would exceed outstanding balance"
│   ├── RETURN_QTY_EXCEEDS_SOLD → "Return quantity exceeds sold quantity"
│   ├── DISPOSITION_REQUIRES_APPROVAL → "Manager approval required before posting"
│   └── (etc. — 65+ codes, all mapped through ProblemDetails.ToUserMessage())
└── 5xx / network error → "Server unreachable. Check connection." with retry option
```

### 4.5 Permission Gating (Nav + Buttons)

| Level | Implementation |
|-------|---------------|
| **Sidebar visibility** | `[ObservableProperty] bool CanViewXxx` on `MainViewModel` → `BoolToVisibilityConverter` in MainWindow.xaml. Section headers visible if ANY child is visible |
| **Screen-level guard** | On navigation, if user lacks required READ permission → show "Access denied" placeholder |
| **Button visibility** | Form ViewModels expose `bool CanEdit`, `CanDelete`, `CanPost`, `CanVoid`, `CanApprove` from `IPermissionService.HasPermission()` → bound to button `Visibility` or `IsEnabled` |
| **Status-dependent** | Draft→Post→Void toolbar adapts: Post button disabled if not Draft or if lacking POST permission |

### 4.6 DPI/Theme Regression Checklist (every phase)

| # | Check | Pass Criteria |
|---|-------|---------------|
| 1 | No fixed pixel widths in DataGrid columns | Use `Star`/`Auto` width modes |
| 2 | All Color/Brush references use `DynamicResource` | Never `StaticResource` for theme-dependent resources |
| 3 | Test at 100% DPI | No clipping, overlapping, or truncated text |
| 4 | Test at 125% DPI | Same |
| 5 | Test at 150% DPI | Same |
| 6 | Theme switch (Dark→Light→Dark) | No visual artifacts, all controls update instantly |
| 7 | Arabic text fields | Use `FlowDirection="RightToLeft"` on Arabic-content fields where appropriate |
| 8 | Font sizes | Use SharedStyles constants (`FontSizeSmall/Normal/Medium/Large/Title`) |
| 9 | Vector icons | All nav/status icons use `Path` geometry (resolution-independent) |

---

## 5) Strict Closeout Protocol (Mandatory)

### 5.1 Agent Verification (automated, per phase)

After every phase (UI 2.2, 2.3, ..., 2.8):

```powershell
# Step 1: Build verification
dotnet build ElshazlyStore.sln
# MUST report: 0 errors, 0 warnings for ALL projects

# Step 2: Test verification (no regressions)
dotnet test tests/ElshazlyStore.Tests/ElshazlyStore.Tests.csproj
# MUST pass: all existing tests still green

# Step 3: File manifest
# Agent produces complete list of new + modified files with line counts
```

### 5.2 Manual Test Script (per phase)

| # | Test | Expected Result |
|---|------|-----------------|
| T1 | Launch app → login with valid credentials | MainWindow loads, sidebar shows permission-gated items |
| T2 | Navigate to each NEW screen via sidebar | Page loads, data fetches (or empty state shown) |
| T3 | Create a new record (form screen) | Record saved, list refreshes, new item visible |
| T4 | Edit an existing record | Changes saved, list reflects update |
| T5 | Delete/deactivate a record | Record removed/deactivated, list refreshes |
| T6 | Search on list screen | Results filter correctly |
| T7 | Page through list (next/prev) | Correct page shown, page counter updates |
| T8 | Switch Dark↔Light theme | All new controls render correctly in both themes |
| T9 | Set DPI to 125% → check new screen layouts | No clipping, overlapping, or broken alignment |
| T10 | Login with limited-permission user | New screens/buttons hidden per permission |
| T11 | Trigger a known API error (e.g., duplicate barcode) | Error message displayed clearly via IMessageService |

### 5.3 Human Vision Gate (فتح البرنامج ورؤية بشرية من المستخدم)

> **This gate is MANDATORY and cannot be skipped by the agent.**

After the agent completes a phase and all automated checks pass:

1. Agent presents: build output, file manifest, acceptance criteria checklist
2. **User must physically launch the application** (`dotnet run --project src/ElshazlyStore.Desktop`) on their machine
3. User visually inspects: layout, theme rendering, DPI behavior, navigation, data flow
4. User reports any visual or behavioral issues
5. Agent fixes all reported issues before closeout

**No phase is closed until the user explicitly confirms visual inspection passed.**

### 5.4 Required Artifacts

| Artifact | Requirement |
|----------|-------------|
| `docs/UI 2.X — CLOSEOUT REPORT.md` | Phase-specific closeout doc containing: scope delivered, files created/modified (with line counts), architecture decisions, known issues, acceptance criteria results |
| Build output | Must show `0 errors, 0 warnings` for entire solution |
| File manifest | Complete list of all new + modified files |
| Known issues list | Any deferred items, workarounds, or open questions — with priority |

### 5.5 STOP Rule

**A phase is NOT complete until ALL of the following are true:**

1. ✅ `dotnet build ElshazlyStore.sln` → 0 errors, 0 warnings
2. ✅ `dotnet test` → all existing tests pass (no regressions)
3. ✅ All acceptance criteria for the phase are checked off
4. ✅ Closeout report (`UI 2.X — CLOSEOUT REPORT.md`) is written
5. ✅ **User has visually inspected the running application and approved**

⛔ **If ANY criterion fails, the phase remains OPEN. The agent MUST NOT proceed to the next phase.**

⛔ **No forward movement without explicit user approval message: "Phase 2.X approved, proceed to 2.Y"**

---

## 6) Backend Gaps (confirmed from inputs)

| # | Gap | Source | Impact on UI | Proposed Fix | Priority |
|---|-----|--------|-------------|-------------|----------|
| G1 | **No audit log read endpoint** | `AUDIT_READ` permission exists (in 47 codes) but no `GET /audit` endpoint in `api.md` or endpoint files | Cannot build audit log viewer screen | Add `GET /api/v1/audit` with paged results, filter by entity/user/date range | P2 — defer, not in UI 2.2–2.8 scope |
| G2 | **ApiClient lacks multipart upload** | Current `ApiClient` only supports JSON `Post/Put/Delete` | Import Wizard (UI 2.8) cannot upload files | Add `PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content)` to `ApiClient` — **client-side change only** | P1 — planned for UI 2.8 |
| G3 | **Print screen codes not documented** | `ConfigJson` in print rules uses `screenCode` but valid codes not enumerated anywhere | Print config UI has no ComboBox options | Propose constants: `POS_RECEIPT`, `PURCHASE_RECEIPT`, `BARCODE_LABEL`, `SALES_RETURN_RECEIPT` **(غير مذكور في المدخلات)** — or use free-text input | P2 |
| G4 | **No self-password change** | Only `PUT /users/{id}` exists (requires `USERS_WRITE`) — regular users can't change own password | No "Change Password" option in Settings | Add `POST /auth/change-password` endpoint requiring only Authenticated | P2 |
| G5 | **User and Role lists not paged** | `GET /users` / `GET /roles` return flat arrays | Acceptable for small datasets; UI uses simple `ObservableCollection` | No change needed — monitor if user/role count grows | P3 — by design |
| G6 | **Receipt template schema undefined** | `ConfigJson` is freeform text — no JSON schema for receipt layout | Print config screen cannot validate or provide structured editor | Define JSON schema for `POS_RECEIPT` config (paper width, header, footer, logo toggle) **(غير مذكور في المدخلات)** | P2 |
| G7 | **PaymentMethod enum discrepancy** | Code entity shows `Cash, InstaPay, EWallet, Visa` but `api.md` shows `Cash, Card, BankTransfer, EWallet, Cheque` | UI ComboBox may show wrong options | Verify actual enum in `Payment.cs` — code is truth | P1 — resolve in Phase 0 |
| G8 | **LedgerEntryType enum extension** | Original enum: `OpeningBalance, Invoice, Payment`. RET phases added `CreditNote, DebitNote` | Ledger viewer must handle all 5 types | Verify actual enum after RET phases — likely resolved | P1 — verify in Phase 0 |
| G9 | **Paging shape inconsistency** | Most endpoints: `PagedResult<T>`. Reason Codes + Print Profiles: anonymous `{ items, totalCount }` | `PagedListViewModelBase<T>` must handle both shapes | Build flexible deserialization in base class | P1 — planned for UI 2.2 |

---

## 7) Next Actions

### Immediate: Phase 0 (Backend UI Contract Freeze)

| # | Action | Output |
|---|--------|--------|
| 1 | **Resolve G7**: Read `Payment.cs` entity in code → confirm actual `PaymentMethod` enum values | Confirmed enum values for UI ComboBox |
| 2 | **Resolve G8**: Read `LedgerEntry.cs` entity → confirm `LedgerEntryType` includes `CreditNote`, `DebitNote` | Confirmed enum values for ledger viewer |
| 3 | **Resolve D2**: Confirm Customer/Supplier use single `Name` field (not `NameAr`/`NameEn`) | Confirmed DTO shape |
| 4 | **Verify api.md accuracy**: Run backend API (or check Swagger) → spot-check 3-4 endpoint responses against documented shapes | Confirmation that api.md is accurate |
| 5 | **Test `ProblemDetails.ToUserMessage()`** for coverage of all 65+ error codes | List of any unmapped codes to add |

### After Phase 0 Passes Gate: Begin UI 2.2

| # | Action | Phase |
|---|--------|-------|
| 6 | Create `Resources/Styles/DataGridStyles.xaml` — themed DataGrid with DynamicResource brushes | UI 2.2 |
| 7 | Create `Resources/Styles/FormStyles.xaml` — themed form controls | UI 2.2 |
| 8 | Create `Resources/Styles/DialogStyles.xaml` — modal overlay style | UI 2.2 |
| 9 | Build `PagedListViewModelBase<T>` — abstract base with dual paging shape support | UI 2.2 |
| 10 | Implement Product List → Product Form (first CRUD — proves the pattern) | UI 2.2 |
| 11 | Clone pattern for Customers, Suppliers, Warehouses | UI 2.2 |
| 12 | Extend `INavigationService` with parameterized `NavigateTo<T>(Action<T>)` | UI 2.2 |
| 13 | Register all new ViewModels + DataTemplates + navigation cases | UI 2.2 |
| 14 | Test DPI (100/125/150) + theme (Dark/Light) on all new screens | UI 2.2 |
| 15 | Write `docs/UI 2.2 — CLOSEOUT REPORT.md` + obtain user visual approval | UI 2.2 |

### Per-Phase Recurring Actions

| Action | When |
|--------|------|
| `dotnet build ElshazlyStore.sln` → 0 errors, 0 warnings | End of every phase |
| `dotnet test` → all tests pass | End of every phase |
| DPI regression test (3 levels) | End of every phase |
| Theme regression test (Dark/Light switch) | End of every phase |
| Write `UI 2.X — CLOSEOUT REPORT.md` | End of every phase |
| **User visual inspection + explicit approval** | **End of every phase — MANDATORY** |

---

## Appendix A — Full Permission Code Reference (47 codes)

| Code | Phase | Screen(s) |
|------|-------|-----------|
| `PRODUCTS_READ` | UI 2.2 | Product List, Product Form, POS (barcode lookup) |
| `PRODUCTS_WRITE` | UI 2.2 | Product Form, Variant Form |
| `CUSTOMERS_READ` | UI 2.2 | Customer List, Customer Form |
| `CUSTOMERS_WRITE` | UI 2.2 | Customer Form |
| `SUPPLIERS_READ` | UI 2.2 | Supplier List, Supplier Form |
| `SUPPLIERS_WRITE` | UI 2.2 | Supplier Form |
| `WAREHOUSES_READ` | UI 2.2 | Warehouse List, Warehouse Form |
| `WAREHOUSES_WRITE` | UI 2.2 | Warehouse Form |
| `STOCK_READ` | UI 2.3 | Stock Balances, Stock Ledger |
| `STOCK_POST` | UI 2.3 | Stock Movement Posting |
| `PURCHASES_READ` | UI 2.4 | Purchase List |
| `PURCHASES_WRITE` | UI 2.4 | Purchase Form |
| `PURCHASES_POST` | UI 2.4 | Purchase Form (Post button) |
| `PRODUCTION_READ` | UI 2.4 | Production List |
| `PRODUCTION_WRITE` | UI 2.4 | Production Form |
| `PRODUCTION_POST` | UI 2.4 | Production Form (Post button) |
| `SALES_READ` | UI 2.5 | Sales List, Sales Form |
| `SALES_WRITE` | UI 2.5 | POS, Sales Form |
| `SALES_POST` | UI 2.5 | POS (Post button), Sales Form |
| `VIEW_REASON_CODES` | UI 2.6 | Reason Code List |
| `MANAGE_REASON_CODES` | UI 2.6 | Reason Code CRUD buttons |
| `VIEW_SALES_RETURNS` | UI 2.6 | Sales Return List |
| `SALES_RETURN_CREATE` | UI 2.6 | Sales Return Form (create/edit/delete) |
| `SALES_RETURN_POST` | UI 2.6 | Sales Return Form (Post button) |
| `SALES_RETURN_VOID` | UI 2.6 | Sales Return Form (Void button) |
| `VIEW_PURCHASE_RETURNS` | UI 2.6 | Purchase Return List |
| `PURCHASE_RETURN_CREATE` | UI 2.6 | Purchase Return Form (create/edit/delete) |
| `PURCHASE_RETURN_POST` | UI 2.6 | Purchase Return Form (Post button) |
| `PURCHASE_RETURN_VOID` | UI 2.6 | Purchase Return Form (Void button) |
| `VIEW_DISPOSITIONS` | UI 2.6 | Disposition List |
| `DISPOSITION_CREATE` | UI 2.6 | Disposition Form (create/edit/delete) |
| `DISPOSITION_APPROVE` | UI 2.6 | Disposition Form (Approve button) |
| `DISPOSITION_POST` | UI 2.6 | Disposition Form (Post button) |
| `DISPOSITION_VOID` | UI 2.6 | Disposition Form (Void button) |
| `ACCOUNTING_READ` | UI 2.7 | Balances, Ledger |
| `PAYMENTS_READ` | UI 2.7 | Payment List |
| `PAYMENTS_WRITE` | UI 2.7 | Payment Form |
| `IMPORT_OPENING_BALANCES` | UI 2.8 | Import Wizard (Opening Balances type) |
| `IMPORT_PAYMENTS` | UI 2.8 | Import Wizard (Payments type) |
| `DASHBOARD_READ` | UI 2.8 | Dashboard |
| `IMPORT_MASTER_DATA` | UI 2.8 | Import Wizard (Products/Customers/Suppliers types) |
| `MANAGE_PRINTING_POLICY` | UI 2.8 | Print Profile List, Print Rule Form |
| `USERS_READ` | UI 2.8 | User List |
| `USERS_WRITE` | UI 2.8 | User Form |
| `ROLES_READ` | UI 2.8 | Role List |
| `ROLES_WRITE` | UI 2.8 | Role Form |
| `AUDIT_READ` | Deferred | No endpoint exists (Gap G1) |

## Appendix B — Error Code Quick Reference (65+ codes by phase)

| Phase | Error Codes |
|-------|-------------|
| Auth (UI 2.1 ✅) | `INVALID_CREDENTIALS`, `ACCOUNT_INACTIVE`, `TOKEN_EXPIRED`, `TOKEN_INVALID`, `UNAUTHORIZED`, `FORBIDDEN` |
| UI 2.2 | `NOT_FOUND`, `CONFLICT`, `BARCODE_ALREADY_EXISTS`, `BARCODE_RETIRED`, `VALIDATION_FAILED` |
| UI 2.3 | `STOCK_NEGATIVE_NOT_ALLOWED`, `MOVEMENT_EMPTY`, `WAREHOUSE_NOT_FOUND`, `VARIANT_NOT_FOUND`, `TRANSFER_UNBALANCED` |
| UI 2.4 | `PURCHASE_RECEIPT_NOT_FOUND`, `PURCHASE_RECEIPT_ALREADY_POSTED`, `PURCHASE_RECEIPT_EMPTY`, `SUPPLIER_NOT_FOUND`, `DOCUMENT_NUMBER_EXISTS`, `PRODUCTION_BATCH_NOT_FOUND`, `PRODUCTION_BATCH_ALREADY_POSTED`, `PRODUCTION_BATCH_NO_INPUTS`, `PRODUCTION_BATCH_NO_OUTPUTS`, `BATCH_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` |
| UI 2.5 | `SALES_INVOICE_NOT_FOUND`, `SALES_INVOICE_ALREADY_POSTED`, `SALES_INVOICE_EMPTY`, `CUSTOMER_NOT_FOUND`, `POST_ALREADY_POSTED` |
| UI 2.6 | `REASON_CODE_NOT_FOUND`, `REASON_CODE_ALREADY_EXISTS`, `REASON_CODE_IN_USE`, `SALES_RETURN_NOT_FOUND`, `SALES_RETURN_ALREADY_POSTED`, `SALES_RETURN_EMPTY`, `RETURN_NUMBER_EXISTS`, `RETURN_QTY_EXCEEDS_SOLD`, `REASON_CODE_INACTIVE`, `SALES_RETURN_ALREADY_VOIDED`, `SALES_RETURN_NOT_POSTED`, `SALES_RETURN_DISPOSITION_NOT_ALLOWED`, `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST`, `PURCHASE_RETURN_NOT_FOUND`, `PURCHASE_RETURN_ALREADY_POSTED`, `PURCHASE_RETURN_EMPTY`, `PURCHASE_RETURN_NUMBER_EXISTS`, `RETURN_QTY_EXCEEDS_RECEIVED`, `PURCHASE_RETURN_ALREADY_VOIDED`, `PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST`, `DISPOSITION_NOT_FOUND`, `DISPOSITION_ALREADY_POSTED`, `DISPOSITION_EMPTY`, `DISPOSITION_ALREADY_VOIDED`, `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST`, `DISPOSITION_NUMBER_EXISTS`, `DISPOSITION_REQUIRES_APPROVAL`, `DISPOSITION_INVALID_TYPE`, `DESTINATION_WAREHOUSE_NOT_FOUND` |
| UI 2.7 | `OVERPAYMENT_NOT_ALLOWED`, `WALLET_NAME_REQUIRED`, `INVALID_PAYMENT_METHOD`, `INVALID_PARTY_TYPE`, `PARTY_NOT_FOUND`, `PAYMENT_NOT_FOUND` |
| UI 2.8 | `IMPORT_PREVIEW_FAILED`, `IMPORT_COMMIT_FAILED`, `IMPORT_JOB_NOT_FOUND`, `IMPORT_JOB_ALREADY_COMMITTED`, `PRINT_PROFILE_NOT_FOUND`, `PRINT_PROFILE_NAME_EXISTS`, `PRINT_RULE_NOT_FOUND`, `PRINT_RULE_SCREEN_EXISTS` |
| Generic | `INTERNAL_ERROR` (500) |

## Appendix C — Phase Summary Table

| Phase | Goal | Screens | New Files (est.) | Key Risk |
|-------|------|---------|-----------------|----------|
| **Phase 0** | Contract Freeze | — | 0 | Discrepancies in enum values |
| **UI 2.2** | Master Data + Shared Styles | 10 (5 entities × list+form) | ~30 | DataGrid DPI regression |
| **UI 2.3** | Inventory & Stock | 3 | ~15 | Transfer form complexity |
| **UI 2.4** | Procurement & Production | 4 + reusable VariantPicker | ~20 | VariantPicker is critical path |
| **UI 2.5** | POS & Sales | 4 + PrintService | ~15 | Barcode scan latency |
| **UI 2.6** | Returns & Dispositions | 7 | ~25 | 14 permission combinations |
| **UI 2.7** | Accounting & Payments | 5 | ~12 | PaymentMethod enum (G7) |
| **UI 2.8** | Dashboard, Import, Print, Admin | 9 + ApiClient multipart | ~25 | Large import file timeout |
| **TOTAL** | — | **42 screens** | **~142 files** | — |

---

*End of UI 2 — EXECUTION MASTER PLAN*
