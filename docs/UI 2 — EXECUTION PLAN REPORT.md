# UI 2 — EXECUTION PLAN REPORT

**Prepared by**: Claude OPUS 4.6 — PLAN MODE  
**Baseline**: UI 1 Foundation ✅ + UI 2.1 Auth+Navigation ✅  
**Scope**: UI 2.2 → UI 2.8 (36 screens, 7 execution phases)  
**Policy**: No invented facts. Unknown items marked **(غير مذكور في المدخلات)**.

---

## Section 1 — Baseline State

### 1.1 UI 1 Foundation (COMPLETE)

| Deliverable | Status |
|-------------|--------|
| WPF Shell (`net8.0-windows`, Per-Monitor V2 DPI) | ✅ |
| TopBar (48px) + Sidebar (220px) + Content region | ✅ |
| DarkTheme.xaml / LightTheme.xaml + SharedStyles.xaml | ✅ |
| DI container (Microsoft.Extensions.DependencyInjection) | ✅ |
| NavigationService + PageDataTemplateSelector | ✅ |
| ApiClient (Get/Post/Put/DeleteAsync\<T\>, ProblemDetails parsing) | ✅ |
| Serilog logging (rolling file + console) | ✅ |
| Publish profiles (win-x64, win-x86) | ✅ |

### 1.2 UI 2.1 Auth + Core Navigation (COMPLETE)

| Deliverable | Status |
|-------------|--------|
| LoginWindow + LoginViewModel (async login, error display) | ✅ |
| SecureTokenStore (DPAPI, `%LOCALAPPDATA%\ElshazlyStore\tokens.dat`) | ✅ |
| TokenRefreshHandler (401 intercept, SemaphoreSlim, retry) | ✅ |
| SessionService (login/logout/restore, JWT permission parsing) | ✅ |
| PermissionService (HasPermission/HasAll/HasAny) | ✅ |
| PermissionCodes (40+ constants mirroring backend) | ✅ |
| Permission-gated sidebar (6 sections, 17 nav items) | ✅ |
| Logout flow (API revoke → clear tokens → LoginWindow) | ✅ |
| ShutdownMode=OnExplicitShutdown (login↔main transitions) | ✅ |
| BoolToVisibility + InverseBoolToVisibility converters | ✅ |

### 1.3 Current File Inventory (42 files)

```
ElshazlyStore.Desktop
├── App.xaml / App.xaml.cs
├── app.manifest / appsettings.json
├── ElshazlyStore.Desktop.csproj
├── Helpers/ (4 files)
│   ├── BoolToVisibilityConverter.cs
│   ├── NavItemActiveConverter.cs
│   ├── PageDataTemplateSelector.cs
│   └── StringEqualityToVisibilityConverter.cs
├── Models/ (6 files)
│   ├── ApiResult.cs, PermissionCodes.cs, ProblemDetails.cs
│   └── Auth/ (LoginRequest, LoginResponse, LogoutRequest, MeResponse, RefreshRequest)
├── Resources/Themes/ (3 files)
│   ├── DarkTheme.xaml, LightTheme.xaml, SharedStyles.xaml
├── Services/ (14 files)
│   ├── IMessageService, INavigationService, IPermissionService,
│   │   ISessionService, IThemeService, IUserPreferencesService
│   ├── JwtClaimParser, MessageService, NavigationService,
│   │   PermissionService, SessionService, ThemeService, UserPreferencesService
│   └── Api/ (ApiClient, AuthHeaderHandler, CorrelationIdHandler,
│             InMemoryTokenStore, ITokenStore, SecureTokenStore, TokenRefreshHandler)
├── ViewModels/ (5 files)
│   ├── ViewModelBase, MainViewModel, HomeViewModel, SettingsViewModel, LoginViewModel
├── Views/ (4+4 files)
│   ├── LoginWindow.xaml/.cs, MainWindow.xaml/.cs
│   └── Pages/ (HomePage.xaml/.cs, SettingsPage.xaml/.cs)
└── Properties/PublishProfiles/ (win-x64.pubxml, win-x86.pubxml)
```

### 1.4 MainViewModel NavigateTo — Current State

The `NavigateTo(string)` switch currently handles only `"Home"` and `"Settings"`. All other cases fall through to `HomeViewModel` with a log message. Each phase must add new `case` entries.

### 1.5 DI Registrations — Current State

`App.xaml.cs` registers: `IUserPreferencesService`, `IThemeService`, `INavigationService`, `IMessageService`, `ITokenStore`, `ISessionService`, `IPermissionService`, `AuthHeaderHandler`, `CorrelationIdHandler`, `TokenRefreshHandler`, `ApiClient` (typed HttpClient), `"AuthRefresh"` named client, `MainViewModel`, `HomeViewModel`, `SettingsViewModel`, `LoginViewModel`, `MainWindow`, `LoginWindow`.

### 1.6 Execution Starts at UI 2.2

All code from here forward builds on the baseline above. No prior deliverables are re-implemented.

---

## Section 2 — Execution Phases (UI 2.2 → UI 2.8)

---

### Phase UI 2.2 — Master Data + Shared Styles

**Goal**: Build the first 10 CRUD screens (Products, Variants, Customers, Suppliers, Warehouses) and establish the reusable DataGrid/Form/Paging patterns used by ALL subsequent phases.

#### 2.2.1 Shared Infrastructure (built FIRST in this phase)

| Deliverable | Description |
|-------------|-------------|
| `DataGridStyles.xaml` | Themed DataGrid: row hover, alternating rows, header style, column sizing, Dark/Light DynamicResources. Added to App.xaml MergedDictionaries. |
| `FormStyles.xaml` | Themed TextBox, ComboBox, DatePicker, Label, validation error template. All using DynamicResource for theme support. |
| `PagedListViewModelBase<T>` | Abstract ViewModel providing: `ObservableCollection<T> Items`, `int Page/PageSize/TotalCount`, `string SearchText`, `string SortColumn`, `bool IsLoading`, `LoadAsync()` calling `ApiClient.GetAsync<PagedResult<T>>`, `NextPage/PrevPage/Refresh` commands, `CancellationToken` support. All list screens inherit this. |
| Client-side DTO models | `Models/Products/`, `Models/Customers/`, `Models/Suppliers/`, `Models/Warehouses/` — mirror server DTOs exactly. |

#### 2.2.2 Screens — Products + Variants (S03, S04, Variant Form)

**API Contracts:**

| Endpoint | Method | Request DTO | Response DTO | Permission |
|----------|--------|-------------|--------------|------------|
| `/api/v1/products` | GET | q?, page, pageSize, sort, includeTotal | `PagedResult<ProductDto>` | `PRODUCTS_READ` |
| `/api/v1/products/{id}` | GET | — | `ProductDetailDto` (has `List<VariantDto>`) | `PRODUCTS_READ` |
| `/api/v1/products` | POST | `CreateProductRequest(Name, Category?, Description?)` | `ProductDto` | `PRODUCTS_WRITE` |
| `/api/v1/products/{id}` | PUT | `UpdateProductRequest(Name?, Category?, Description?, IsActive?)` | `{ message }` | `PRODUCTS_WRITE` |
| `/api/v1/products/{id}` | DELETE | — | `{ message }` | `PRODUCTS_WRITE` |
| `/api/v1/variants` | POST | `CreateVariantRequest(ProductId, Sku, Barcode?, Color?, Size?, RetailPrice?, WholesalePrice?)` | `VariantListDto` | `PRODUCTS_WRITE` |
| `/api/v1/variants/{id}` | PUT | `UpdateVariantRequest(Sku?, Barcode?, Color?, Size?, RetailPrice?, WholesalePrice?, IsActive?)` | `{ message }` | `PRODUCTS_WRITE` |
| `/api/v1/variants/{id}` | DELETE | — | `{ message }` | `PRODUCTS_WRITE` |

**DTO Shapes:**
```
ProductDto: Id, Name, Category?, Description?, IsActive, CreatedAtUtc, UpdatedAtUtc?, VariantCount
ProductDetailDto: Id, Name, Category?, Description?, IsActive, CreatedAtUtc, UpdatedAtUtc?, Variants: List<VariantDto>
VariantDto: Id, ProductId, Sku, Barcode?, Color?, Size?, RetailPrice?, WholesalePrice?, IsActive, CreatedAtUtc
VariantListDto: Id, ProductId, ProductName, Sku, Barcode?, Color?, Size?, RetailPrice?, WholesalePrice?, IsActive, CreatedAtUtc
```

**Error Codes**: `NOT_FOUND`, `CONFLICT`, `BARCODE_ALREADY_EXISTS`, `VALIDATION_FAILED`

**Files to Create:**
- `Models/Products/ProductDto.cs`, `ProductDetailDto.cs`, `VariantDto.cs`, `CreateProductRequest.cs`, `UpdateProductRequest.cs`, `CreateVariantRequest.cs`, `UpdateVariantRequest.cs`
- `ViewModels/Products/ProductListViewModel.cs` (extends `PagedListViewModelBase<ProductDto>`)
- `ViewModels/Products/ProductFormViewModel.cs` (CRUD + embedded variant management)
- `Views/Pages/Products/ProductListPage.xaml/.cs`
- `Views/Pages/Products/ProductFormPage.xaml/.cs`

#### 2.2.3 Screens — Customers (S05, S06)

**API Contracts:**

| Endpoint | Method | Request DTO | Response DTO | Permission |
|----------|--------|-------------|--------------|------------|
| `/api/v1/customers` | GET | q?, page, pageSize, sort, includeTotal | `PagedResult<CustomerDto>` | `CUSTOMERS_READ` |
| `/api/v1/customers/{id}` | GET | — | `CustomerDto` | `CUSTOMERS_READ` |
| `/api/v1/customers` | POST | `CreateCustomerRequest(Code?, NameAr, NameEn?, Phone?, Address?, TaxId?)` | `CustomerDto` | `CUSTOMERS_WRITE` |
| `/api/v1/customers/{id}` | PUT | `UpdateCustomerRequest(Code?, NameAr?, NameEn?, Phone?, Address?, TaxId?, IsActive?)` | `{ message }` | `CUSTOMERS_WRITE` |
| `/api/v1/customers/{id}` | DELETE | — | `{ message }` | `CUSTOMERS_WRITE` |

**DTO Shape:**
```
CustomerDto: Id, Code, NameAr, NameEn?, Phone?, Address?, TaxId?, IsActive, CreatedAtUtc, UpdatedAtUtc?
```

**Error Codes**: `NOT_FOUND`, `CONFLICT`, `VALIDATION_FAILED`

**Files to Create:**
- `Models/Customers/CustomerDto.cs`, `CreateCustomerRequest.cs`, `UpdateCustomerRequest.cs`
- `ViewModels/Customers/CustomerListViewModel.cs`, `CustomerFormViewModel.cs`
- `Views/Pages/Customers/CustomerListPage.xaml/.cs`, `CustomerFormPage.xaml/.cs`

#### 2.2.4 Screens — Suppliers (S07, S08)

**API Contracts:** Same pattern as Customers using `/api/v1/suppliers`.

**DTO Shape:**
```
SupplierDto: Id, Code, NameAr, NameEn?, Phone?, Address?, TaxId?, IsActive, CreatedAtUtc, UpdatedAtUtc?
CreateSupplierRequest: Code?, NameAr, NameEn?, Phone?, Address?, TaxId?
UpdateSupplierRequest: Code?, NameAr?, NameEn?, Phone?, Address?, TaxId?, IsActive?
```

**Permissions**: `SUPPLIERS_READ`, `SUPPLIERS_WRITE`

**Files to Create:**
- `Models/Suppliers/SupplierDto.cs`, `CreateSupplierRequest.cs`, `UpdateSupplierRequest.cs`
- `ViewModels/Suppliers/SupplierListViewModel.cs`, `SupplierFormViewModel.cs`
- `Views/Pages/Suppliers/SupplierListPage.xaml/.cs`, `SupplierFormPage.xaml/.cs`

#### 2.2.5 Screens — Warehouses (S09, S10)

**API Contracts:**

| Endpoint | Method | Request DTO | Response DTO | Permission |
|----------|--------|-------------|--------------|------------|
| `/api/v1/warehouses` | GET | q?, page, pageSize, includeTotal | `PagedResult<WarehouseDto>` | `WAREHOUSES_READ` |
| `/api/v1/warehouses/{id}` | GET | — | `WarehouseDto` | `WAREHOUSES_READ` |
| `/api/v1/warehouses` | POST | `CreateWarehouseRequest(Code, Name, Address?, IsDefault?)` | `WarehouseDto` | `WAREHOUSES_WRITE` |
| `/api/v1/warehouses/{id}` | PUT | `UpdateWarehouseRequest(Code?, Name?, Address?, IsDefault?, IsActive?)` | `{ message }` | `WAREHOUSES_WRITE` |
| `/api/v1/warehouses/{id}` | DELETE | — | `{ message }` | `WAREHOUSES_WRITE` |

**DTO Shape:**
```
WarehouseDto: Id, Code, Name, Address?, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc?
```

**Files to Create:**
- `Models/Warehouses/WarehouseDto.cs`, `CreateWarehouseRequest.cs`, `UpdateWarehouseRequest.cs`
- `ViewModels/Warehouses/WarehouseListViewModel.cs`, `WarehouseFormViewModel.cs`
- `Views/Pages/Warehouses/WarehouseListPage.xaml/.cs`, `WarehouseFormPage.xaml/.cs`

#### 2.2.6 Registration & Wiring

For EVERY screen added:
1. Register ViewModel as `Transient` in `App.xaml.cs` → `ConfigureServices()`
2. Add `DataTemplate` in `MainWindow.xaml.Resources` mapping ViewModel → Page
3. Add `case` in `MainViewModel.NavigateTo()` switch
4. Verify sidebar nav button `CommandParameter` matches the case string

Phase UI 2.2 adds 10 new `case` entries: `Products`, `ProductForm`, `Customers`, `CustomerForm`, `Suppliers`, `SupplierForm`, `Warehouses`, `WarehouseForm` (+ `ProductList`, `VariantForm` if separate navigation is needed).

#### 2.2.7 Acceptance Criteria

- [ ] All 5 entity types have working list + form screens
- [ ] Search, pagination (next/prev/page number), sort work on all list screens
- [ ] Create, edit, delete (soft-deactivate) work on all form screens
- [ ] `BARCODE_ALREADY_EXISTS` error shown on variant form
- [ ] Permission-gated: screens hidden if user lacks READ; write buttons hidden if lacks WRITE
- [ ] DPI tested at 100%, 125%, 150% — no layout breakage
- [ ] Dark/Light theme switch — no visual glitches
- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] ~30 new files created, ~3 files modified (App.xaml.cs, MainViewModel.cs, MainWindow.xaml)

---

### Phase UI 2.3 — Inventory & Stock

**Goal**: Stock balance browser, stock ledger viewer, and manual stock movement posting. Introduce `StatusBadgeStyles.xaml` for Draft/Posted/Voided visual states.

#### 2.3.1 Shared Infrastructure

| Deliverable | Description |
|-------------|-------------|
| `StatusBadgeStyles.xaml` | Visual badges: Draft (grey), Posted (green), Voided (red). Reused by Purchases, Production, Sales, Returns, Dispositions. |
| `DecimalFormatConverter` | Formats decimal values for display (e.g., `1,234.50`). |

#### 2.3.2 Screens — Stock Balances (S11)

**API Contract:**

| Endpoint | Method | Query Params | Permission |
|----------|--------|--------------|------------|
| `/api/v1/stock/balances` | GET | `warehouseId?`, `q?`, `page=1`, `pageSize=25`, `includeTotal=true` | `STOCK_READ` |

**Response Shape:**
```
BalanceRow: VariantId, Sku, ProductName, Color?, Size?, WarehouseId, WarehouseCode, WarehouseName, Quantity
→ PagedResult<BalanceRow>
```

**UI**: Paged DataGrid with warehouse ComboBox filter (populated from `GET /warehouses`), search by SKU/product name.

#### 2.3.3 Screens — Stock Ledger (S12)

**API Contract:**

| Endpoint | Method | Query Params | Permission |
|----------|--------|--------------|------------|
| `/api/v1/stock/ledger` | GET | `variantId?`, `warehouseId?`, `from?`, `to?`, `movementType?`, `page=1`, `pageSize=25`, `includeTotal=true` | `STOCK_READ` |

**Response Shape:**
```
LedgerRow: Id, VariantId, Sku, ProductName, WarehouseId, WarehouseCode,
           MovementType, Direction (In/Out), Quantity, UnitCost, Reference?,
           CreatedAtUtc, CreatedByUsername
→ PagedResult<LedgerRow>
```

**UI**: Filter pane (variant search, warehouse dropdown, date range, movement type dropdown). Paged DataGrid.

#### 2.3.4 Screens — Stock Movement Posting (S13)

**API Contract:**

| Endpoint | Method | Request DTO | Permission |
|----------|--------|-------------|------------|
| `/api/v1/stock-movements/post` | POST | `PostRequest` | `STOCK_POST` |

**Request Shape:**
```
PostRequest: MovementType (enum int), Reference?, Notes?, Lines: List<PostLineRequest>
PostLineRequest: VariantId, WarehouseId, Direction (In/Out), Quantity, UnitCost?

MovementType enum: OpeningBalance=0, Purchase=1, PurchaseReturn=2, Sale=3,
  SalesReturn=4, Transfer=5, Adjustment=6, Production=7, Disposition=8, Other=9
```

**Validation:** Lines ≥ 1. Quantity > 0. Transfer requires balanced In/Out lines. `STOCK_NEGATIVE_NOT_ALLOWED` if result would go negative.

**Error Codes**: `STOCK_NEGATIVE_NOT_ALLOWED`, `MOVEMENT_EMPTY`, `WAREHOUSE_NOT_FOUND`, `VARIANT_NOT_FOUND`, `TRANSFER_UNBALANCED`

**UI**: Form with movement type selector, reference field, editable line grid (variant picker, warehouse dropdown, direction toggle, qty, cost). Post button.

#### 2.3.5 Files to Create

- `Resources/Styles/StatusBadgeStyles.xaml`
- `Helpers/DecimalFormatConverter.cs`
- `Models/Stock/BalanceRow.cs`, `LedgerRow.cs`, `PostRequest.cs`, `PostLineRequest.cs`, `MovementType.cs`
- `ViewModels/Stock/StockBalanceViewModel.cs`, `StockLedgerViewModel.cs`, `StockMovementViewModel.cs`
- `Views/Pages/Stock/StockBalancePage.xaml/.cs`, `StockLedgerPage.xaml/.cs`, `StockMovementPage.xaml/.cs`
- ~15 new files

#### 2.3.6 Acceptance Criteria

- [ ] Balance screen shows correct stock per variant per warehouse
- [ ] Warehouse filter works; search filters by SKU/product name
- [ ] Ledger shows full movement history with date range filter
- [ ] Stock movement posting: OpeningBalance adds stock, Adjustment modifies, Transfer is balanced
- [ ] `STOCK_NEGATIVE_NOT_ALLOWED` error displayed clearly
- [ ] StatusBadge styles render correctly in Dark/Light themes
- [ ] Permission-gated: `STOCK_READ`, `STOCK_POST`
- [ ] `dotnet build` — 0 errors, 0 warnings

---

### Phase UI 2.4 — Procurement & Production

**Goal**: Purchase receipt and production batch workflows with Draft→Post lifecycle. Introduce the reusable VariantPicker control and Draft/Post/Void toolbar pattern.

#### 2.4.1 Shared Infrastructure

| Deliverable | Description |
|-------------|-------------|
| `VariantPickerControl` | Reusable UserControl: barcode scan field + search-by-SKU/name + dropdown result → emits selected `VariantId, Sku, ProductName`. Used in Purchases, Production, Sales, Returns. |
| Draft→Post→Void toolbar pattern | Reusable button strip logic: Save Draft (enabled when Draft), Post (enabled when Draft + has POST permission), Delete (enabled when Draft), Void (enabled when Posted + has VOID permission). Status-dependent. |

#### 2.4.2 Screens — Purchases (S14, S15)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/purchases` | GET | q?, page, pageSize, sort → `PagedResult<ReceiptDto>` | `PURCHASES_READ` |
| `/api/v1/purchases/{id}` | GET | → `ReceiptDto` (with Lines) | `PURCHASES_READ` |
| `/api/v1/purchases` | POST | `CreatePurchaseRequest` → `ReceiptDto` | `PURCHASES_WRITE` |
| `/api/v1/purchases/{id}` | PUT | `UpdatePurchaseRequest` → `{ message }` | `PURCHASES_WRITE` |
| `/api/v1/purchases/{id}` | DELETE | → `{ message }` | `PURCHASES_WRITE` |
| `/api/v1/purchases/{id}/post` | POST | → `{ stockMovementId }` | `PURCHASES_POST` |

**DTO Shapes:**
```
CreatePurchaseRequest: SupplierId, WarehouseId, DocumentNumber?, ReceiptDateUtc?, Notes?,
                       Lines: List<PurchaseLineRequest>
PurchaseLineRequest: VariantId, Quantity, UnitCost
UpdatePurchaseRequest: SupplierId?, WarehouseId?, DocumentNumber?, Notes?,
                       Lines?: List<PurchaseLineRequest>

ReceiptDto: Id, DocumentNumber, ReceiptDateUtc, SupplierId, SupplierName,
            WarehouseId, WarehouseName, CreatedByUserId, CreatedByUsername,
            Notes?, Status (Draft/Posted), StockMovementId?,
            TotalAmount, CreatedAtUtc, PostedAtUtc?, Lines: List<ReceiptLineDto>
ReceiptLineDto: Id, VariantId, Sku, ProductName?, Quantity, UnitCost, LineTotal
PostResult: StockMovementId
```

**Error Codes**: `PURCHASE_RECEIPT_NOT_FOUND`, `PURCHASE_RECEIPT_ALREADY_POSTED`, `PURCHASE_RECEIPT_EMPTY`, `SUPPLIER_NOT_FOUND`, `DOCUMENT_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT`

**Files to Create:**
- `Models/Purchases/` (all DTOs)
- `ViewModels/Purchases/PurchaseListViewModel.cs`, `PurchaseFormViewModel.cs`
- `Views/Pages/Purchases/PurchaseListPage.xaml/.cs`, `PurchaseFormPage.xaml/.cs`
- `Views/Controls/VariantPickerControl.xaml/.cs` (reusable)

#### 2.4.3 Screens — Production (S16, S17)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/production` | GET | q?, page, pageSize, sort → `PagedResult<BatchDto>` | `PRODUCTION_READ` |
| `/api/v1/production/{id}` | GET | → `BatchDto` (with Lines) | `PRODUCTION_READ` |
| `/api/v1/production` | POST | `CreateProductionRequest` → `BatchDto` | `PRODUCTION_WRITE` |
| `/api/v1/production/{id}` | PUT | `UpdateProductionRequest` → `{ message }` | `PRODUCTION_WRITE` |
| `/api/v1/production/{id}` | DELETE | → `{ message }` | `PRODUCTION_WRITE` |
| `/api/v1/production/{id}/post` | POST | → `PostResult` | `PRODUCTION_POST` |

**DTO Shapes:**
```
CreateProductionRequest: WarehouseId, BatchNumber?, ProductionDateUtc?, Notes?,
                         InputLines: List<ProductionLineRequest>,
                         OutputLines: List<ProductionLineRequest>
ProductionLineRequest: VariantId, Quantity, UnitCost?
UpdateProductionRequest: WarehouseId?, BatchNumber?, Notes?,
                         InputLines?: List<ProductionLineRequest>,
                         OutputLines?: List<ProductionLineRequest>

BatchDto: Id, BatchNumber, ProductionDateUtc, WarehouseId, WarehouseName,
          CreatedByUserId, CreatedByUsername, Notes?, Status,
          StockMovementId?, InputStockMovementId?, OutputStockMovementId?,
          TotalInputCost, TotalOutputCost,
          CreatedAtUtc, PostedAtUtc?,
          InputLines: List<BatchLineDto>, OutputLines: List<BatchLineDto>
BatchLineDto: Id, VariantId, Sku, ProductName?, Quantity, UnitCost?, LineTotal?
PostResult: InputStockMovementId, OutputStockMovementId
```

**Error Codes**: `PRODUCTION_BATCH_NOT_FOUND`, `PRODUCTION_BATCH_ALREADY_POSTED`, `PRODUCTION_BATCH_NO_INPUTS`, `PRODUCTION_BATCH_NO_OUTPUTS`, `BATCH_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT`

**Files to Create:**
- `Models/Production/` (all DTOs)
- `ViewModels/Production/ProductionListViewModel.cs`, `ProductionFormViewModel.cs`
- `Views/Pages/Production/ProductionListPage.xaml/.cs`, `ProductionFormPage.xaml/.cs`

#### 2.4.4 Acceptance Criteria

- [ ] Purchase: create draft → add lines via VariantPicker → save → post → stock increases
- [ ] Production: create draft → input lines + output lines → post → consume + produce movements
- [ ] `ALREADY_POSTED` error handled gracefully (disable Post button on Posted status)
- [ ] Concurrency conflict shows "Record modified elsewhere, please reload" with reload button
- [ ] VariantPicker reused in both Purchase and Production forms
- [ ] Cannot edit/delete Posted documents (buttons disabled)
- [ ] Separate READ/WRITE/POST permissions respected
- [ ] ~20 new files

---

### Phase UI 2.5 — POS & Sales

**Goal**: Fast POS screen with barcode scanner, sales list/form, and print hook integration.

#### 2.5.1 Screens — POS (S18)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/barcodes/{barcode}` | GET | → `BarcodeLookupResult` | (authenticated) |
| `/api/v1/sales` | POST | `CreateSalesInvoiceRequest` → `InvoiceDto` | `SALES_WRITE` |
| `/api/v1/sales/{id}` | PUT | `UpdateSalesInvoiceRequest` → `{ message }` | `SALES_WRITE` |
| `/api/v1/sales/{id}/post` | POST | → `{ stockMovementId }` | `SALES_POST` |
| `/api/v1/print-policy/{screenCode}` | GET | screenCode=`POS_RECEIPT` → print rules | (authenticated) |

**BarcodeLookupResult Shape:**
```
Barcode, Status (Active/Retired), VariantId, Sku, Color?, Size?,
RetailPrice?, WholesalePrice?, IsActive, ProductId, ProductName, ProductCategory?
→ 404 = not found, 410 = retired
```

**Sales Invoice DTOs:**
```
CreateSalesInvoiceRequest: WarehouseId, CustomerId?, InvoiceDateUtc?, Notes?,
                           Lines: List<SalesInvoiceLineRequest>
SalesInvoiceLineRequest: VariantId, Quantity, UnitPrice, DiscountAmount?

InvoiceDto: Id, InvoiceNumber, InvoiceDateUtc, CustomerId?, CustomerName?,
            WarehouseId, WarehouseName, CashierUserId, CashierUsername,
            Notes?, Status (Draft/Posted), StockMovementId?,
            TotalAmount, CreatedAtUtc, PostedAtUtc?,
            Lines: List<InvoiceLineDto>
InvoiceLineDto: Id, VariantId, Sku, ProductName?, Quantity, UnitPrice,
                DiscountAmount, LineTotal
```

**Error Codes**: `SALES_INVOICE_EMPTY`, `SALES_INVOICE_ALREADY_POSTED`, `STOCK_NEGATIVE_NOT_ALLOWED`, `CUSTOMER_NOT_FOUND`, `POST_CONCURRENCY_CONFLICT`  
Barcode-specific: 404 = not found, 410 = `BARCODE_RETIRED`

**POS UX Requirements:**
- Full-content-area layout (no sidebar clutter)
- Auto-focused barcode TextBox at top
- Keyboard-wedge scanner: detect rapid keystrokes → on Enter → call barcode lookup → add line
- Line grid: SKU, Product, Qty (editable), Unit Price (editable), Discount, Line Total
- Running total display (large font)
- Customer selector (optional, ComboBox with search)
- Warehouse selector
- Post button → creates invoice + posts atomically → triggers print hook
- Sub-second barcode lookup UX target

**Print Hook:** After successful post, query `GET /print-policy/POS_RECEIPT?profileId=...` → if rule exists and enabled → render receipt using WPF PrintDialog + `ConfigJson` template. **(غير مذكور في المدخلات: exact receipt template schema — use sensible defaults: invoice number, date, cashier, lines, total)**

#### 2.5.2 Screens — Sales List + Form (S19, S20)

**Sales List**: Standard `PagedListViewModelBase<InvoiceDto>` with search, status filter (Draft/Posted).

**Sales Form**: View/edit draft invoice. Same DTOs as POS. Edit lines, change customer/warehouse. Post or delete.

#### 2.5.3 Files to Create

- `Models/Sales/` (InvoiceDto, InvoiceLineDto, CreateSalesInvoiceRequest, etc.)
- `Models/Barcodes/BarcodeLookupResult.cs`
- `ViewModels/Sales/PosViewModel.cs`, `SalesListViewModel.cs`, `SalesFormViewModel.cs`
- `Views/Pages/Sales/PosPage.xaml/.cs`, `SalesListPage.xaml/.cs`, `SalesFormPage.xaml/.cs`
- `Services/IPrintService.cs`, `Services/PrintService.cs` (WPF printing)
- ~15 new files

#### 2.5.4 Acceptance Criteria

- [ ] Barcode scan adds line in <500ms
- [ ] Walk-in sale (no customer) works
- [ ] Customer-linked sale works
- [ ] Post → stock decreases → AR entry if customer assigned
- [ ] Receipt prints if print rule configured for `POS_RECEIPT`
- [ ] `STOCK_NEGATIVE_NOT_ALLOWED` shown if insufficient stock
- [ ] POS startup < 1s (no heavy preloads)
- [ ] Retired barcode (410) shows clear message
- [ ] Sales list + form: standard operations work
- [ ] Permissions: `SALES_WRITE`, `SALES_POST`, `SALES_READ`

---

### Phase UI 2.6 — Returns & Dispositions

**Goal**: Sales returns, purchase returns, dispositions with reason codes and the full Draft→Approve→Post→Void lifecycle.

#### 2.6.1 Screens — Reason Code List (S27)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/reasons` | GET | category?, isActive?, q?, page, pageSize → items+totalCount | `VIEW_REASON_CODES` |
| `/api/v1/reasons/{id}` | GET | → `ReasonCodeDto` | `VIEW_REASON_CODES` |
| `/api/v1/reasons` | POST | `CreateReasonCodeRequest(Code, NameAr, Description?, Category, RequiresManagerApproval?)` | `MANAGE_REASON_CODES` |
| `/api/v1/reasons/{id}` | PUT | `UpdateReasonCodeRequest(NameAr?, Description?, Category?, RequiresManagerApproval?)` | `MANAGE_REASON_CODES` |
| `/api/v1/reasons/{id}/disable` | POST | → `{ message }` | `MANAGE_REASON_CODES` |

**DTO Shape:**
```
ReasonCodeDto: Id, Code, NameAr, Description?, Category (General/SalesReturn/PurchaseReturn/Disposition),
               IsActive, RequiresManagerApproval, CreatedAtUtc, UpdatedAtUtc?
```

**Note:** Response uses anonymous paged object `{ items, totalCount, page, pageSize }`, NOT `PagedResult<T>`.

#### 2.6.2 Screens — Sales Returns (S21, S22)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/sales-returns` | GET | Paged → `PagedResult<ReturnDto>` | `VIEW_SALES_RETURNS` |
| `/api/v1/sales-returns/{id}` | GET | → `ReturnDto` with Lines | `VIEW_SALES_RETURNS` |
| `/api/v1/sales-returns` | POST | `CreateSalesReturnRequest` | `SALES_RETURN_CREATE` |
| `/api/v1/sales-returns/{id}` | PUT | `UpdateSalesReturnRequest` | `SALES_RETURN_CREATE` |
| `/api/v1/sales-returns/{id}` | DELETE | → `{ message }` | `SALES_RETURN_CREATE` |
| `/api/v1/sales-returns/{id}/post` | POST | → `{ stockMovementId }` | `SALES_RETURN_POST` |
| `/api/v1/sales-returns/{id}/void` | POST | → `{ message }` | `SALES_RETURN_VOID` |

**DTOs:**
```
CreateSalesReturnRequest: WarehouseId, CustomerId?, OriginalSalesInvoiceId?, ReturnDateUtc?, Notes?,
                          Lines: List<SalesReturnLineRequest>
SalesReturnLineRequest: VariantId, Quantity, UnitPrice, ReasonCodeId, DispositionType (enum int), Notes?

ReturnDto: Id, ReturnNumber, ReturnDateUtc, CustomerId?, CustomerName?,
           OriginalSalesInvoiceId?, OriginalInvoiceNumber?,
           WarehouseId, WarehouseName, CreatedByUserId, CreatedByUsername,
           Notes?, Status (Draft/Posted/Voided), StockMovementId?, TotalAmount,
           CreatedAtUtc, PostedAtUtc?, PostedByUserId?,
           Lines: List<ReturnLineDto>
ReturnLineDto: Id, VariantId, Sku, ProductName?, Quantity, UnitPrice, LineTotal,
               ReasonCodeId, ReasonCodeCode, ReasonCodeNameAr,
               DispositionType, Notes?
```

**Error Codes**: `SALES_RETURN_NOT_FOUND`, `SALES_RETURN_ALREADY_POSTED`, `SALES_RETURN_EMPTY`, `RETURN_QTY_EXCEEDS_SOLD`, `REASON_CODE_INACTIVE`, `SALES_RETURN_ALREADY_VOIDED`, `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST`

#### 2.6.3 Screens — Purchase Returns (S23, S24)

Same pattern as Sales Returns, endpoint `/api/v1/purchase-returns`.

**Key Differences:**
- `SupplierId` required (not optional like CustomerId in sales returns)
- Line uses `UnitCost` (not `UnitPrice`)
- Error codes: `PURCHASE_RETURN_*` variants, `RETURN_QTY_EXCEEDS_RECEIVED`

#### 2.6.4 Screens — Dispositions (S25, S26)

**API Contracts:** Routes: GET `/`, GET `/{id}`, POST `/`, PUT `/{id}`, DELETE `/{id}`, POST `/{id}/approve`, POST `/{id}/post`, POST `/{id}/void`

**DTOs:**
```
CreateDispositionRequest: WarehouseId, DispositionDateUtc?, Notes?,
                          Lines: List<DispositionLineRequest>
DispositionLineRequest: VariantId, Quantity, ReasonCodeId, DispositionType (enum int), Notes?

DispositionDto: Id, DispositionNumber, DispositionDateUtc, WarehouseId, WarehouseName,
                CreatedByUserId, CreatedByUsername, Notes?, Status (Draft/Approved/Posted/Voided),
                StockMovementId?, ApprovedByUserId?, ApprovedByUsername?, ApprovedAtUtc?,
                CreatedAtUtc, PostedAtUtc?, PostedByUserId?,
                Lines: List<DispositionLineDto>
DispositionLineDto: Id, VariantId, Sku, ProductName?, Quantity,
                    ReasonCodeId, ReasonCodeCode, ReasonCodeNameAr,
                    RequiresManagerApproval, DispositionType, Notes?

DispositionType enum: Scrap=0, Rework=1, ReturnToVendor=2, ReturnToStock=3, Quarantine=4, WriteOff=5
Pre-sale dispositions only allow: Scrap, Quarantine, WriteOff, Rework (NOT ReturnToVendor/ReturnToStock)
```

**Error Codes**: `DISPOSITION_NOT_FOUND`, `DISPOSITION_ALREADY_POSTED`, `DISPOSITION_EMPTY`, `DISPOSITION_ALREADY_VOIDED`, `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST`, `DISPOSITION_REQUIRES_APPROVAL`, `DISPOSITION_INVALID_TYPE`, `DESTINATION_WAREHOUSE_NOT_FOUND`, `DISPOSITION_NUMBER_EXISTS`

**Special UX**: Approve button visible only to users with `DISPOSITION_APPROVE`. If any line has a reason code with `RequiresManagerApproval=true`, posting is blocked until approved.

#### 2.6.5 Files to Create (~25 files)

- `Models/Returns/` (SalesReturn DTOs, PurchaseReturn DTOs)
- `Models/Dispositions/` (DispositionDto, etc.)
- `Models/ReasonCodes/` (ReasonCodeDto, etc.)
- Enums: `DispositionType.cs`, `ReasonCategory.cs`
- ViewModels: `ReasonCodeListViewModel`, `SalesReturnListViewModel`, `SalesReturnFormViewModel`, `PurchaseReturnListViewModel`, `PurchaseReturnFormViewModel`, `DispositionListViewModel`, `DispositionFormViewModel`
- Views/Pages for each

#### 2.6.6 Acceptance Criteria

- [ ] All return types create correct stock movements on post
- [ ] Void only works on Draft status; `ALREADY_VOIDED` handled
- [ ] `RETURN_QTY_EXCEEDS_SOLD` / `RETURN_QTY_EXCEEDS_RECEIVED` errors shown
- [ ] Disposition requires approval when reason code flagged
- [ ] Approval clears if lines edited after approval **(غير مذكور في المدخلات — assumed based on typical workflow)**
- [ ] Reason code + disposition type dropdowns populated correctly, filtered by category
- [ ] Permissions: `VIEW_SALES_RETURNS`, `SALES_RETURN_CREATE/POST/VOID`, `VIEW_PURCHASE_RETURNS`, `PURCHASE_RETURN_CREATE/POST/VOID`, `VIEW_DISPOSITIONS`, `DISPOSITION_CREATE/APPROVE/POST/VOID`, `VIEW_REASON_CODES`, `MANAGE_REASON_CODES`

---

### Phase UI 2.7 — Accounting & Payments

**Goal**: AR/AP balance views, party ledger viewer, and payment creation/listing.

#### 2.7.1 Screens — Balances (S28)

**API Contracts:**

| Endpoint | Method | Response | Permission |
|----------|--------|----------|------------|
| `/api/v1/accounting/balances/customers` | GET | `PagedResult<PartyBalanceDto>` | `ACCOUNTING_READ` |
| `/api/v1/accounting/balances/suppliers` | GET | `PagedResult<PartyBalanceDto>` | `ACCOUNTING_READ` |
| `/api/v1/accounting/balances/{partyType}/{partyId}` | GET | `{ partyId, partyType, outstanding }` | `ACCOUNTING_READ` |

**DTO Shape:**
```
PartyBalanceDto: PartyId, PartyName, PartyCode, PartyType (enum),
                 TotalDebits, TotalCredits, Outstanding
```

**UI**: Tab control with Customers / Suppliers tabs. Each tab: paged DataGrid (PartyName, Code, Debits, Credits, Outstanding). Search. Click row → navigate to Party Ledger.

#### 2.7.2 Screens — Party Ledger (S29)

**API Contract:**

| Endpoint | Method | Response | Permission |
|----------|--------|----------|------------|
| `/api/v1/accounting/ledger/{partyType}/{partyId}` | GET | `PagedResult<LedgerEntryDto>` | `ACCOUNTING_READ` |

**DTO Shape:**
```
LedgerEntryDto: Id, PartyType, PartyId, PartyName, EntryType, Amount,
                Reference?, Notes?, RelatedInvoiceId?, RelatedPaymentId?,
                CreatedAtUtc, CreatedByUserId, CreatedByUsername
```

**UI**: Header showing party name + outstanding. Paged list of entries. Button to create payment (navigates to Payment Form with party pre-filled).

#### 2.7.3 Screens — Payments (S30, S31)

**API Contracts:**

| Endpoint | Method | Request/Response | Permission |
|----------|--------|-----------------|------------|
| `/api/v1/payments` | GET | partyType?, partyId?, q?, page, pageSize → `PagedResult<PaymentDto>` | `PAYMENTS_READ` |
| `/api/v1/payments/{id}` | GET | → `PaymentDto` | `PAYMENTS_READ` |
| `/api/v1/payments` | POST | `CreatePaymentRequest` → `PaymentDto` | `PAYMENTS_WRITE` |

**DTOs:**
```
CreatePaymentRequest: PartyType (customer/supplier), PartyId, Amount (>0),
                      Method (Cash/InstaPay/EWallet/Visa), WalletName? (required if EWallet),
                      Reference?, PaymentDateUtc?

PaymentDto: Id, PartyType, PartyId, PartyName, Amount, Method, WalletName?, Reference?,
            PaymentDateUtc, CreatedAtUtc, CreatedByUserId, CreatedByUsername
```

**Error Codes**: `OVERPAYMENT_NOT_ALLOWED`, `WALLET_NAME_REQUIRED`, `INVALID_PAYMENT_METHOD`, `INVALID_PARTY_TYPE`, `PARTY_NOT_FOUND`, `PAYMENT_NOT_FOUND`

**UI**: Payment form with party type toggle, party selector, amount, method ComboBox (Cash/InstaPay/EWallet/Visa), conditional wallet name field. Payment list with party filters.

#### 2.7.4 Files to Create (~12 files)

- `Models/Accounting/PartyBalanceDto.cs`, `LedgerEntryDto.cs`, `PaymentDto.cs`, `CreatePaymentRequest.cs`
- Enums: `PartyType.cs`, `PaymentMethod.cs`
- `ViewModels/Accounting/BalancesViewModel.cs`, `LedgerViewModel.cs`, `PaymentFormViewModel.cs`, `PaymentListViewModel.cs`
- `Views/Pages/Accounting/` — 4 pages

#### 2.7.5 Acceptance Criteria

- [ ] Balances show correct outstanding amounts (Debits - Credits)
- [ ] Ledger shows all entry types
- [ ] Navigation: click balance row → drill into ledger → create payment from ledger
- [ ] Payment: overpayment blocked, wallet name enforced for EWallet method
- [ ] Permissions: `ACCOUNTING_READ`, `PAYMENTS_READ`, `PAYMENTS_WRITE`

---

### Phase UI 2.8 — Dashboard, Import, Print Config, Admin

**Goal**: Final phase. Dashboard KPIs, import wizard, print profile management, user/role administration.

#### 2.8.1 ApiClient Extension — PREREQUISITE

Add `PostMultipartAsync<T>` method to `ApiClient` for file uploads. Increase timeout for import requests.

#### 2.8.2 Screens — Dashboard (S02)

**API Contracts:**

| Endpoint | Method | Query Params | Permission |
|----------|--------|-------------|------------|
| `/api/v1/dashboard/summary` | GET | from?, to?, topN=10, lowStockThreshold=5 | `DASHBOARD_READ` |
| `/api/v1/dashboard/sales` | GET | from?, to? | `DASHBOARD_READ` |
| `/api/v1/dashboard/top-products` | GET | from?, to?, topN=10, orderBy=quantity | `DASHBOARD_READ` |
| `/api/v1/dashboard/low-stock` | GET | threshold=5 | `DASHBOARD_READ` |
| `/api/v1/dashboard/cashier-performance` | GET | from?, to? | `DASHBOARD_READ` |

**DTO Shapes:**
```
DashboardSummaryDto:
  Sales: SalesSummaryDto(TotalSales, InvoiceCount, AverageTicket,
         TotalSalesReturns, ReturnCount, NetSales, NetAverageTicket)
  TopProductsByQuantity: List<TopProductDto>
  TopProductsByRevenue: List<TopProductDto>
  LowStockAlerts: List<LowStockAlertDto>
  CashierPerformance: List<CashierPerformanceDto>
  DispositionLoss: decimal

TopProductDto: ProductId, ProductName, VariantId, Sku, Color?, Size?,
               TotalQuantity, TotalRevenue, ReturnedQuantity, ReturnedRevenue,
               NetQuantity, NetRevenue

LowStockAlertDto: VariantId, Sku, ProductName, Color?, Size?,
                  WarehouseId, WarehouseCode, WarehouseName,
                  CurrentStock, Threshold

CashierPerformanceDto: CashierUserId, CashierUsername,
                       InvoiceCount, TotalSales, AverageTicket
```

**UI**: KPI cards row (Total Sales, Invoice Count, Avg Ticket, Net Sales, Disposition Loss), Top Products table (by qty + by revenue toggle), Low Stock Alerts list, Cashier Performance table. Date range picker (defaults: 1st of month → now).

**Chart Library**: **(غير مذكور في المدخلات)** — Use simple WPF data cards/tables. No third-party charting library unless user specifies.

#### 2.8.3 Screens — Import Wizard (S32)

**API Contracts:**

| Endpoint | Method | Content-Type | Permission |
|----------|--------|-------------|------------|
| `/api/v1/imports/masterdata/preview` | POST | multipart/form-data + query `type=Products\|Customers\|Suppliers` | `IMPORT_MASTER_DATA` |
| `/api/v1/imports/masterdata/commit` | POST | JSON `CommitRequest(JobId)` | `IMPORT_MASTER_DATA` |
| `/api/v1/imports/opening-balances/preview` | POST | multipart/form-data | `IMPORT_OPENING_BALANCES` |
| `/api/v1/imports/opening-balances/commit` | POST | JSON `CommitRequest(JobId)` | `IMPORT_OPENING_BALANCES` |
| `/api/v1/imports/payments/preview` | POST | multipart/form-data | `IMPORT_PAYMENTS` |
| `/api/v1/imports/payments/commit` | POST | JSON `CommitRequest(JobId)` | `IMPORT_PAYMENTS` |

**DTOs:**
```
CommitRequest: JobId (Guid)
ImportPreviewResult: JobId, TotalRows, ValidRows, RowErrors: List<List<ImportRowError>>
ImportRowError: Column, Message
ImportCommitResult: Success, ImportedCount, ErrorMessage?, ErrorCode?
```

**Error Codes**: `IMPORT_PREVIEW_FAILED`, `IMPORT_COMMIT_FAILED`, `IMPORT_JOB_NOT_FOUND`, `IMPORT_JOB_ALREADY_COMMITTED`

**UI**: 3-step wizard:
1. Select import type (ComboBox) + file upload (OpenFileDialog, `.csv`/`.xlsx`)
2. Preview: show TotalRows, ValidRows, row errors in DataGrid (row#, column, message)
3. Commit: button → shows imported count or error

#### 2.8.4 Screens — Print Profile Management (S33, S34)

**API Contracts:**

| Endpoint | Method | Permission |
|----------|--------|------------|
| `/api/v1/print-profiles` | GET/POST | `MANAGE_PRINTING_POLICY` |
| `/api/v1/print-profiles/{id}` | GET/PUT/DELETE | `MANAGE_PRINTING_POLICY` |
| `/api/v1/print-profiles/{profileId}/rules` | GET/POST | `MANAGE_PRINTING_POLICY` |
| `/api/v1/print-profiles/{profileId}/rules/{ruleId}` | GET/PUT/DELETE | `MANAGE_PRINTING_POLICY` |

**DTOs:**
```
PrintProfileDto: Id, Name, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc?, CreatedByUserId?
PrintProfileDetailDto: (same + Rules: List<PrintRuleDto>)
PrintRuleDto: Id, PrintProfileId, ScreenCode, ConfigJson, Enabled, CreatedAtUtc, UpdatedAtUtc?

CreateProfileRequest: Name, IsDefault?
UpdateProfileRequest: Name?, IsDefault?, IsActive?
CreateRuleRequest: ScreenCode, ConfigJson?, Enabled?
UpdateRuleRequest: ScreenCode?, ConfigJson?, Enabled?
```

**Note:** Profile/Rule list responses use anonymous `{ items, totalCount, page, pageSize }`, NOT `PagedResult<T>`.

#### 2.8.5 Screens — User Management (S35, S36)

**API Contracts:**

| Endpoint | Method | Permission |
|----------|--------|------------|
| `/api/v1/users` | GET | `USERS_READ` |
| `/api/v1/users/{id}` | GET | `USERS_READ` |
| `/api/v1/users` | POST | `USERS_WRITE` |
| `/api/v1/users/{id}` | PUT | `USERS_WRITE` |
| `/api/v1/users/{id}` | DELETE (soft) | `USERS_WRITE` |

**DTOs:**
```
UserDto: Id, Username, IsActive, CreatedAtUtc, Roles: List<string>
CreateUserRequest: Username (≥3), Password (≥6), RoleIds?: List<Guid>
UpdateUserRequest: Username?, Password?, IsActive?, RoleIds?
```

**Note:** `GET /users` returns `List<UserDto>` — NOT paged. DELETE is soft-delete (sets IsActive=false).

#### 2.8.6 Screens — Role Management (S37, S38)

**API Contracts:**

| Endpoint | Method | Permission |
|----------|--------|------------|
| `/api/v1/roles` | GET | `ROLES_READ` |
| `/api/v1/roles/{id}` | GET | `ROLES_READ` |
| `/api/v1/roles` | POST | `ROLES_WRITE` |
| `/api/v1/roles/{id}` | PUT | `ROLES_WRITE` |
| `/api/v1/roles/{id}` | DELETE | `ROLES_WRITE` |
| `/api/v1/roles/permissions/all` | GET | `ROLES_READ` |
| `/api/v1/roles/{id}/permissions` | GET | `ROLES_READ` |
| `/api/v1/roles/{id}/permissions` | PUT | `ROLES_WRITE` |

**DTOs:**
```
RoleDto: Id, Name, Description?, CreatedAtUtc, Permissions: List<string>
PermissionDto: Id, Code, Description?
CreateRoleRequest: Name, Description?
UpdateRoleRequest: Name?, Description?
SetPermissionsRequest: PermissionCodes: List<string>
```

**Note:** `GET /roles` returns `List<RoleDto>` — NOT paged. Role form must load all 47 permissions via `/permissions/all` and display as checklist.

#### 2.8.7 Files to Create (~25 files)

- Add `PostMultipartAsync<T>` to `Services/Api/ApiClient.cs`
- `Models/Dashboard/`, `Models/Import/`, `Models/Printing/`, `Models/Admin/`
- ViewModels: `DashboardViewModel`, `ImportWizardViewModel`, `PrintProfileListViewModel`, `PrintRuleFormViewModel`, `UserListViewModel`, `UserFormViewModel`, `RoleListViewModel`, `RoleFormViewModel`
- Views/Pages for each

#### 2.8.8 Acceptance Criteria

- [ ] Dashboard KPIs correct for selected date range; date range defaults to current month
- [ ] Import: file upload works via OpenFileDialog, preview shows row errors, commit imports data
- [ ] `IMPORT_JOB_ALREADY_COMMITTED` handled
- [ ] Print profiles: CRUD + nested rules work; ConfigJson edited as JSON text
- [ ] Users: CRUD + role assignment works; deactivation (soft-delete) works
- [ ] Roles: CRUD + all 47 permissions shown as checkboxes, toggle on/off
- [ ] All screens permission-gated
- [ ] `dotnet build` — 0 errors, 0 warnings

---

## Section 3 — Global UI Patterns

### 3.1 MVVM Structure (per screen)

Every screen follows this pattern:
1. **Model** (`Models/{Domain}/XxxDto.cs`) — C# `record` mirroring server DTO exactly
2. **ViewModel** (`ViewModels/{Domain}/XxxViewModel.cs`) — inherits `ViewModelBase` or `PagedListViewModelBase<T>`; uses `[ObservableProperty]`, `[RelayCommand]`
3. **View** (`Views/Pages/{Domain}/XxxPage.xaml`) — XAML UserControl, no code-behind logic
4. **Registration**: DI transient in `App.xaml.cs`, DataTemplate in `MainWindow.xaml`, case in `MainViewModel.NavigateTo()`

### 3.2 PagedListViewModelBase\<T\> (introduced UI 2.2)

```
Abstract class providing:
- ObservableCollection<T> Items
- int Page, PageSize, TotalCount, TotalPages (computed)
- string SearchText, SortColumn
- bool IsLoading
- abstract string Endpoint { get; }
- async Task LoadAsync() → calls ApiClient.GetAsync<PagedResult<T>>(...)
- RelayCommand: NextPageCommand, PrevPageCommand, RefreshCommand, SearchCommand
- CancellationTokenSource for debounced search
```

All list screens (Products, Customers, Suppliers, Warehouses, Stock, Purchases, Production, Sales, Returns, Dispositions, Balances, Payments) inherit this.

### 3.3 Shared Styles

| Style File | Content | Phase |
|-----------|---------|-------|
| `SharedStyles.xaml` | Converters, fonts, spacing, nav buttons, card style, accent button, theme toggle | ✅ exists |
| `DataGridStyles.xaml` | Themed DataGrid (row hover, alternating, header, column sizing) | UI 2.2 |
| `FormStyles.xaml` | TextBox, ComboBox, DatePicker, Label, validation error template | UI 2.2 |
| `StatusBadgeStyles.xaml` | Draft (grey), Posted (green), Voided (red), Approved (blue) badges | UI 2.3 |
| `DialogStyles.xaml` | Modal dialog overlay (confirmation, error detail) | UI 2.2 |

### 3.4 Error Handling Flow

```
ApiClient.SendAsync
  ├── 2xx → ApiResult<T>.Success(data)
  │         → ViewModel shows success toast / navigates away
  ├── 400 (VALIDATION_FAILED) → ApiResult<T>.Failure(message)
  │         → Parse errors dict → show field-level red borders + messages
  ├── 401 → TokenRefreshHandler auto-retries once
  │         → If refresh fails: SessionExpired → LoginWindow
  ├── 403 (FORBIDDEN) → ApiResult<T>.Failure
  │         → Show "You don't have permission" via IMessageService
  ├── 404 → "Record not found or deleted"
  ├── 409 → "Modified by another user, please reload"
  ├── 422 (business rules) → ProblemDetails.Detail displayed
  │         Examples: STOCK_NEGATIVE_NOT_ALLOWED, OVERPAYMENT_NOT_ALLOWED,
  │                   RETURN_QTY_EXCEEDS_SOLD, DISPOSITION_REQUIRES_APPROVAL
  └── 5xx / network error → "Server unreachable. Check connection."
```

### 3.5 Permission Gating Pattern

**Sidebar visibility**: `[ObservableProperty] bool CanViewXxx` bound via `BoolToVisibilityConverter` in MainWindow.xaml.

**Button visibility**: Form ViewModels expose `bool CanEdit` / `bool CanDelete` / `bool CanPost` computed from `IPermissionService.HasPermission()`. Bound to button `Visibility` or `IsEnabled`.

**Screen-level guard**: On navigation, if user lacks required READ permission, show "Access denied" placeholder instead of the page content.

### 3.6 Draft → Post → Void State Machine

| Status | Allowed Actions | UI State |
|--------|----------------|----------|
| Draft | Save, Post, Delete | All edit controls enabled. Save/Post/Delete buttons visible. |
| Posted | (none — immutable) | All edit controls disabled. Post/Delete hidden. |
| Voided | (none — immutable) | All edit controls disabled. All action buttons hidden. |
| Draft (Disposition) | Save, Post (if approved), Delete, Request Approval | Post disabled until approved. |
| Approved (Disposition) | Post, Void | Edit disabled. Post enabled. |

Toolbar control adapts button visibility based on `Status` string + user permissions.

### 3.7 DPI / Theme Checklist (every phase)

- [ ] No fixed pixel widths in DataGrid columns (use Star/Auto)
- [ ] All Color/Brush references use `DynamicResource` (not `StaticResource`)
- [ ] Test at 100%, 125%, 150% DPI — no clipping or overlapping
- [ ] Theme switch (Dark↔Light) — no visual artifacts
- [ ] Arabic text fields use `FlowDirection="RightToLeft"` where appropriate
- [ ] Font sizes use SharedStyles constants (`FontSizeSmall/Normal/Medium/Large/Title`)

### 3.8 Navigation Parameter Pattern

For screens needing context (e.g., "Edit Customer with ID X"):

`NavigateTo(string pageName)` switch creates the ViewModel. For parameterized navigation, extend `INavigationService` with `NavigateTo<TViewModel>(Action<TViewModel> configure)` — the configure callback sets properties (e.g., `vm.EditId = customerId`) before the page renders.

---

## Section 4 — Closeout Protocol

### 4.1 Per-Phase Verification Commands

After every phase (UI 2.2, 2.3, ..., 2.8):

```
# 1. Build verification
dotnet build ElshazlyStore.sln

# 2. Error check
# Must report: 0 errors, 0 warnings for ElshazlyStore.Desktop project

# 3. Run existing tests (must not regress)
dotnet test ElshazlyStore.Tests.csproj
```

### 4.2 Manual Test Script (per phase)

| # | Test | Expected |
|---|------|----------|
| T1 | Launch app → login with valid credentials | MainWindow loads, sidebar shows permission-gated items |
| T2 | Navigate to each new screen via sidebar | Page loads, data fetches (or empty state shown) |
| T3 | Create a new record (form screen) | Record saved, list refreshes, new item visible |
| T4 | Edit an existing record | Changes saved, list reflects update |
| T5 | Delete/deactivate a record | Record removed/deactivated, list refreshes |
| T6 | Search on list screen | Results filter correctly |
| T7 | Page through list (next/prev) | Correct page shown, page counter updates |
| T8 | Switch Dark/Light theme | All new controls render correctly in both themes |
| T9 | Set DPI to 125% → check new screen layouts | No clipping, overlapping, or broken alignment |
| T10 | Login with limited-permission user | Screens/buttons hidden per permission |
| T11 | Trigger a known API error (e.g., duplicate barcode) | Error message displayed clearly |

### 4.3 Required Artifacts (per phase)

| Artifact | Description |
|----------|-------------|
| `docs/UI 2.X — CLOSEOUT REPORT.md` | Phase-specific closeout doc (scope delivered, files created/modified, decisions, known issues) |
| Build output: `0 errors, 0 warnings` | Must pass before phase is closed |
| File manifest | List of all new + modified files with line counts |
| Known issues list | Any deferred items or workarounds |

### 4.4 STOP Rule

**A phase is NOT complete until:**
1. `dotnet build ElshazlyStore.sln` → 0 errors, 0 warnings
2. All acceptance criteria checked off
3. Closeout report written
4. No test regressions

**If ANY acceptance criterion fails**, the phase remains open. No forward movement to the next phase.

---

## Section 5 — Backend Gaps

| # | Gap | Impact | Proposed Fix | Priority | Status |
|---|-----|--------|-------------|----------|--------|
| G1 | **No audit log read endpoint** | `AUDIT_READ` permission exists but no `GET /audit` endpoint | Add `GET /api/v1/audit` with paged results | P2 | Open |
| G2 | **ApiClient lacks multipart upload** | Import Wizard needs `PostMultipartAsync<T>` | Add method to `ApiClient` (client-side change in UI 2.8) | P1 | Planned UI 2.8 |
| G3 | **No screen codes documented** | Print policy `screenCode` values not enumerated | Propose constants: `POS_RECEIPT`, `PURCHASE_RECEIPT`, `BARCODE_LABEL`, `SALES_RETURN_RECEIPT` **(غير مذكور في المدخلات)** | P2 | Open |
| G4 | **No self password change** | `PUT /users/{id}` requires `USERS_WRITE` — users can't change own password | Add `POST /auth/change-password` (Authenticated only) | P2 | Open |
| G5 | **User list not paged** | `GET /users` returns all users as flat array | Acceptable for small user count; monitor | P3 | Open |
| G6 | **No stock count / physical inventory** | Manual adjustment via stock movements is sufficient for MVP | Defer | P3 | Deferred |
| G7 | **Receipt template schema undefined** | `ConfigJson` is freeform — no schema for `POS_RECEIPT` config | Define JSON schema (paper width, header, footer, logo) **(غير مذكور في المدخلات)** | P2 | Open |
| G8 | **Reason codes / Print profiles use anonymous paging** | These endpoints return `{ items, totalCount, page, pageSize }` instead of `PagedResult<T>` | `PagedListViewModelBase<T>` must handle both shapes OR use a separate deserialization | P1 | Planned |
| G9 | **Roles and Users lists not paged** | `GET /roles` and `GET /users` return plain `List<T>` | Use simple `ObservableCollection` (no `PagedListViewModelBase`) for these screens | P3 | By design |

---

## Section 6 — Next Actions (to start UI 2.2 immediately)

| # | Action | Detail |
|---|--------|--------|
| 1 | **Create `Resources/Styles/DataGridStyles.xaml`** | Themed DataGrid style with DynamicResource brushes. Add to App.xaml MergedDictionaries. |
| 2 | **Create `Resources/Styles/FormStyles.xaml`** | Themed TextBox, ComboBox, DatePicker, Label styles. |
| 3 | **Create `Resources/Styles/DialogStyles.xaml`** | Modal confirmation/error dialog overlay style. |
| 4 | **Create `ViewModels/PagedListViewModelBase<T>`** | Abstract base with Items, Page, PageSize, TotalCount, Search, Sort, LoadAsync, navigation commands. Must handle both `PagedResult<T>` and anonymous `{ items, totalCount }` shapes. |
| 5 | **Create `Models/Products/` folder** with all 7 DTO records mirroring backend exactly. |
| 6 | **Implement `ProductListViewModel`** | First `PagedListViewModelBase<ProductDto>` instance. Proves the pattern. |
| 7 | **Implement `ProductFormViewModel`** | First CRUD form. Create/Edit/Delete product + embedded variant management. |
| 8 | **Create `Views/Pages/Products/ProductListPage.xaml`** | DataGrid using new `DataGridStyles`, search box, paging controls. |
| 9 | **Create `Views/Pages/Products/ProductFormPage.xaml`** | Form layout using `FormStyles`, variant sub-grid. |
| 10 | **Register in DI + wire navigation** | Add `ProductListViewModel`, `ProductFormViewModel` to DI. Add DataTemplates. Add cases to `MainViewModel.NavigateTo()`. |
| 11 | **Clone pattern for Customers, Suppliers, Warehouses** | After Products pattern is proven, replicate for remaining 3 entity types. |
| 12 | **Add DTO models for Customers, Suppliers, Warehouses** | Create model folders with record definitions. |
| 13 | **Extend `INavigationService` with parameterized navigation** | `NavigateTo<T>(Action<T> configure)` for edit scenarios (passing entity ID). |
| 14 | **Test at 3 DPI levels + both themes** | Verify all new DataGrid/Form screens at 100/125/150% DPI, Dark/Light. |
| 15 | **Write `docs/UI 2.2 — CLOSEOUT REPORT.md`** | After all screens pass acceptance criteria. |

---

## Appendix A — Full Permission Code Reference (47 codes)

| Code | Used By Phase |
|------|--------------|
| `PRODUCTS_READ`, `PRODUCTS_WRITE` | UI 2.2 |
| `CUSTOMERS_READ`, `CUSTOMERS_WRITE` | UI 2.2 |
| `SUPPLIERS_READ`, `SUPPLIERS_WRITE` | UI 2.2 |
| `WAREHOUSES_READ`, `WAREHOUSES_WRITE` | UI 2.2 |
| `STOCK_READ`, `STOCK_POST` | UI 2.3 |
| `PURCHASES_READ`, `PURCHASES_WRITE`, `PURCHASES_POST` | UI 2.4 |
| `PRODUCTION_READ`, `PRODUCTION_WRITE`, `PRODUCTION_POST` | UI 2.4 |
| `SALES_READ`, `SALES_WRITE`, `SALES_POST` | UI 2.5 |
| `VIEW_REASON_CODES`, `MANAGE_REASON_CODES` | UI 2.6 |
| `VIEW_SALES_RETURNS`, `SALES_RETURN_CREATE`, `SALES_RETURN_POST`, `SALES_RETURN_VOID` | UI 2.6 |
| `VIEW_PURCHASE_RETURNS`, `PURCHASE_RETURN_CREATE`, `PURCHASE_RETURN_POST`, `PURCHASE_RETURN_VOID` | UI 2.6 |
| `VIEW_DISPOSITIONS`, `DISPOSITION_CREATE`, `DISPOSITION_APPROVE`, `DISPOSITION_POST`, `DISPOSITION_VOID` | UI 2.6 |
| `ACCOUNTING_READ` | UI 2.7 |
| `PAYMENTS_READ`, `PAYMENTS_WRITE` | UI 2.7 |
| `DASHBOARD_READ` | UI 2.8 |
| `IMPORT_MASTER_DATA`, `IMPORT_OPENING_BALANCES`, `IMPORT_PAYMENTS` | UI 2.8 |
| `MANAGE_PRINTING_POLICY` | UI 2.8 |
| `USERS_READ`, `USERS_WRITE` | UI 2.8 |
| `ROLES_READ`, `ROLES_WRITE` | UI 2.8 |
| `AUDIT_READ` | Deferred (G1) |

## Appendix B — Error Code Quick Reference (65 codes)

All errors return RFC 7807 ProblemDetails. `ApiClient.TryParseProblemDetails()` extracts the message. Key domain-specific codes per phase:

| Phase | Error Codes |
|-------|-------------|
| UI 2.2 | `NOT_FOUND`, `CONFLICT`, `BARCODE_ALREADY_EXISTS`, `VALIDATION_FAILED` |
| UI 2.3 | `STOCK_NEGATIVE_NOT_ALLOWED`, `MOVEMENT_EMPTY`, `WAREHOUSE_NOT_FOUND`, `VARIANT_NOT_FOUND`, `TRANSFER_UNBALANCED` |
| UI 2.4 | `PURCHASE_RECEIPT_*`, `PRODUCTION_BATCH_*`, `SUPPLIER_NOT_FOUND`, `DOCUMENT_NUMBER_EXISTS`, `BATCH_NUMBER_EXISTS`, `POST_CONCURRENCY_CONFLICT` |
| UI 2.5 | `SALES_INVOICE_*`, `STOCK_NEGATIVE_NOT_ALLOWED`, `CUSTOMER_NOT_FOUND`, barcode 404/410 |
| UI 2.6 | `SALES_RETURN_*`, `PURCHASE_RETURN_*`, `DISPOSITION_*`, `REASON_CODE_*`, `RETURN_QTY_EXCEEDS_SOLD`, `RETURN_QTY_EXCEEDS_RECEIVED` |
| UI 2.7 | `OVERPAYMENT_NOT_ALLOWED`, `WALLET_NAME_REQUIRED`, `INVALID_PAYMENT_METHOD`, `INVALID_PARTY_TYPE`, `PARTY_NOT_FOUND` |
| UI 2.8 | `IMPORT_*`, `PRINT_PROFILE_*`, `PRINT_RULE_*` |

---

*End of UI 2 — EXECUTION PLAN REPORT*
