# SALES PREP AUDIT 1 — CONTRACT + POLICY + POS FIT REPORT

**Phase:** SALES PREP AUDIT 1  
**Date:** 2026-03-28  
**Status:** READ-ONLY REPORT — NO CODE CHANGES  
**Author:** GitHub Copilot (Claude Sonnet 4.6)  
**Canonical contract source:** `docs/openapi.json` (452 KB, generated 2026-03-06 per BACKEND 4 closeout)  
**Source read:** SalesEndpoints.cs, SalesReturnEndpoints.cs, SalesService.cs, SalesReturnService.cs, AccountingService.cs, PaymentEndpoints.cs, BarcodeEndpoints.cs, CustomerEndpoints.cs, WarehouseEndpoints.cs, VariantEndpoints.cs, MainViewModel.cs, PermissionCodes.cs, Permissions.cs, ProductVariant.cs, SalesInvoice.cs, SalesReturn.cs, Customer.cs, Warehouse.cs, UI 2.4-R10 closeout (status map), UI 2.4-R9 closeout.

---

## 1) Executive Verdict

| Question | Verdict |
|---|---|
| Can we start Sales UI immediately? | **YES** — backend is complete and contract is proven. No backend work needed before Sales Admin. |
| Do we need a backend micro-phase first? | **NO** for core flow. Conditional YES only if tax or invoice-level discount or customer-type auto-pricing enforcement is required before launch. |
| Can POS be built on the same Sales endpoints? | **PARTIAL YES** — same `POST /api/v1/sales` and `POST /api/v1/sales/{id}/post` endpoints serve both. POS uses the barcode endpoint (`GET /api/v1/barcodes/{barcode}`) for fast lookup. Named-customer immediate payment is supported via a second call to `POST /api/v1/payments`. Anonymous walk-in payment is NOT recorded in the backend — it is a UI-only cash drawer convention. |
| Top blockers | (1) No invoice-level tax field in contract. (2) No customer type (retail/wholesale) field — price selection is a pure-UI decision based on `retailPrice`/`wholesalePrice` from variant data. (3) Anonymous POS immediate payment cannot be persisted to the backend (no partyId). (4) No-invoice return has no manager-approval gate at backend level — that enforcement is UI-only. (5) Voiding a Posted sales return: may be blocked per error code `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST` (NOT FULLY PROVEN — source not completely read). |

**Summary:** Sales Admin screen can be built NOW. POS screen can be built NOW with well-understood limitations. Sales Returns screen can be built NOW. No backend micro-phase is required before these three screens unless tax or retail/wholesale customer classification must be enforced server-side.

---

## 2) Sales Contract Audit

### Endpoints (from `docs/openapi.json` and `SalesEndpoints.cs`)

| Method | Path | Permission | Purpose |
|---|---|---|---|
| `GET` | `/api/v1/sales` | `SALES_READ` | Paged list, search by q, sort, includeTotal |
| `GET` | `/api/v1/sales/{id}` | `SALES_READ` | Single invoice with lines |
| `POST` | `/api/v1/sales` | `SALES_WRITE` | Create draft invoice |
| `PUT` | `/api/v1/sales/{id}` | `SALES_WRITE` | Update draft invoice (draft only) |
| `DELETE` | `/api/v1/sales/{id}` | `SALES_WRITE` | Delete draft invoice (draft only) |
| `POST` | `/api/v1/sales/{id}/post` | `SALES_POST` | Post invoice to inventory (Draft→Posted, terminal) |

**No void endpoint** exists for Sales invoices. Deletion is the only reversal and applies to Draft only.

### Request DTOs (grounded in `SalesEndpoints.cs`, confirmed by `openapi.json`)

**`CreateSalesInvoiceRequest`:**
```
warehouseId       Guid (required, validated active)
customerId        Guid? (nullable — null = anonymous / walk-in)
invoiceDateUtc    DateTime? (nullable — defaults to UtcNow)
notes             string? (nullable)
lines             List<SalesInvoiceLineRequest> (at least 1 required)
```

**`SalesInvoiceLineRequest`:**
```
variantId         Guid (required, validated exists)
quantity          decimal (must be > 0)
unitPrice         decimal (must be >= 0)
discountAmount    decimal? (nullable, must be >= 0 if present) — PER-LINE AMOUNT DISCOUNT ONLY
```

**`UpdateSalesInvoiceRequest`:**
```
warehouseId       Guid? (optional, update warehouse)
customerId        Guid? (optional, set/change customer)
clearCustomer     bool? (explicit flag to set customer to null)
notes             string? (optional)
lines             List<SalesInvoiceLineRequest>? (optional, replaces all lines if present)
```

### Response DTO (grounded in `SalesService.cs`: `InvoiceDto`)

```
Id               Guid
InvoiceNumber    string (server-generated, format: INV-NNNNNN via PostgreSQL sequence)
InvoiceDateUtc   DateTime
CustomerId       Guid? (nullable)
CustomerName     string? (nullable)
WarehouseId      Guid
WarehouseName    string
CashierUserId    Guid
CashierUsername  string
Notes            string?
Status           string ("Draft" | "Posted")
StockMovementId  Guid? (null until posted)
TotalAmount      decimal
CreatedAtUtc     DateTime
PostedAtUtc      DateTime?
Lines            List<InvoiceLineDto>
  └ Id, VariantId, Sku, ProductName?, Quantity, UnitPrice, DiscountAmount, LineTotal
```

### Posting Flow (grounded in `SalesService.PostAsync`)

1. Atomic `UPDATE … WHERE Status = Draft` → sets `Status = Posted` (TOCTOU-safe)
2. Creates `SaleIssue` stock movement (negative quantityDelta per line) via `StockService.PostAsync`
3. If customer present: creates `CustomerReceivable` record + AR ledger entry (debit via `AccountingService.CreateInvoiceEntryAsync`)
4. Rollback to Draft if stock posting fails

### Delete / Void Behavior

- **Delete (Draft):** `DELETE /api/v1/sales/{id}` — hard delete. Requires `Status = Draft`.
- **Void:** NOT SUPPORTED. No void endpoint for sales invoices. Error code `SalesInvoiceAlreadyPosted` (409) on any mutation attempt after posting.

### Print-Related Implications

- `ReceiptPrintService` is already implemented and shared. A sales receipt method (`PrintSalesReceipt()`) needs to be added to `DocumentPrintHelper.cs` following the same pattern as `PrintPurchase()` or `PrintPaymentReceipt()`. This is a Desktop-only addition. No backend change needed.
- `GET /api/v1/print-profiles` exists in contract but the Desktop print config page is NOT yet implemented — prints will use hardcoded layout (same as current pattern).

### Verdict: Can Sales Admin screen be built now?

**YES.** All required endpoints, request/response shapes, and posting semantics are proven from source. No schema gaps for core create/edit/delete/post/print flow.

---

## 3) Sales Point / POS Fit Audit

**Verdict: PARTIAL YES**

### What works on the same endpoints:

| POS Requirement | Endpoint | Status |
|---|---|---|
| Create sale with barcode-resolved variant | `POST /api/v1/sales` — same DTO | ✅ YES |
| Post sale immediately | `POST /api/v1/sales/{id}/post` | ✅ YES |
| Barcode lookup → VariantId, retails price, wholesale price | `GET /api/v1/barcodes/{barcode}` | ✅ YES — cached 60s, returns `RetailPrice`, `WholesalePrice`, `VariantId`, `Sku`, `IsActive` |
| Anonymous walk-in (no customer) | `customerId: null` in `CreateSalesInvoiceRequest` | ✅ SUPPORTED BY CONTRACT — entity comment explicitly states "null = walk-in retail" |
| Named customer selection | `customerId` in request | ✅ YES |
| Per-line discount | `discountAmount` on line | ✅ YES (amount only, not %) |
| Cash / Instapay / EWallet / Visa payment method recording for named customer | `POST /api/v1/payments` with `partyType="customer"` | ✅ SUPPORTED (see §8) |

### What is missing or constrained:

| POS Requirement | Status | Notes |
|---|---|---|
| Anonymous walk-in immediate payment recording | ❌ NOT SUPPORTED | No partyId → cannot POST to `/api/v1/payments`. Cash drawer shortfall is UI/operational convention only. |
| Draft-and-post in one step (POS doesn't need draft state) | PARTIAL — two calls required (POST create, then POST /{id}/post) | The backend requires Draft first. UI can make the pair of calls transparently, making it feel like one action. No backend change needed for UX. |
| Print receipt immediately after post | ⚠️ UI-only | Desktop must `GET /api/v1/sales/{id}` to fetch fresh DTO for printing. Same pattern as current `PrintPurchaseDocCommand`. |
| Barcode scan → price auto-fill by customer type | ⚠️ UI-ONLY | Barcode response includes both `retailPrice` and `wholesalePrice`. UI decides which to pre-fill based on whether a customer is selected and what the override should be. No backend enforcement. |

### Immediate Payment Supported?

**For named customers: YES (indirectly — two calls)**
1. `POST /api/v1/sales` → create draft
2. `POST /api/v1/sales/{id}/post` → posts, creates `CustomerReceivable` + AR ledger entry
3. `POST /api/v1/payments` with `partyType="customer"`, `partyId=customerId.Value`, `amount=totalAmount`, `method=Cash|...` → clears AR

Note: payment amount cannot exceed outstanding balance (`allowOverpay=false` is hardcoded in `AccountingService.CreatePaymentAsync` when called from the public `PaymentEndpoints.cs`). In the POS flow, immediate payment after post should always be exactly equal to the invoice total, so this constraint is not a problem.

**For anonymous walk-in: SUPPORTED but no backend payment record.**
- Sale is posted. No AR entry is created (no customer). Cash is collected physically. No API call for payment. This is by design (entity doc: "null = walk-in retail").

**Evidence:**
- `SalesInvoice.CustomerId` is `Guid?` (proven from `SalesInvoice.cs`)
- `SalesService.PostAsync` only creates `CustomerReceivable` if `invoice.CustomerId.HasValue` (proven from `SalesService.cs`)
- `POST /api/v1/payments` accepts `partyType=customer` and `partyId=customerId` (proven from `PaymentEndpoints.cs`)

---

## 4) Sales Returns Contract Audit

### Endpoints (from `docs/openapi.json` and `SalesReturnEndpoints.cs`)

| Method | Path | Permission | Purpose |
|---|---|---|---|
| `GET` | `/api/v1/sales-returns` | `VIEW_SALES_RETURNS` | Paged list, search, sort |
| `GET` | `/api/v1/sales-returns/{id}` | `VIEW_SALES_RETURNS` | Single return with lines |
| `POST` | `/api/v1/sales-returns` | `SALES_RETURN_CREATE` | Create draft return |
| `PUT` | `/api/v1/sales-returns/{id}` | `SALES_RETURN_CREATE` | Update draft return |
| `DELETE` | `/api/v1/sales-returns/{id}` | `SALES_RETURN_CREATE` | Delete draft return |
| `POST` | `/api/v1/sales-returns/{id}/post` | `SALES_RETURN_POST` | Post return to inventory |
| `POST` | `/api/v1/sales-returns/{id}/void` | `SALES_RETURN_VOID` | Void return |

### Request DTOs (grounded in `SalesReturnEndpoints.cs` and `SalesReturnService.cs`)

**`CreateSalesReturnRequest`:**
```
warehouseId              Guid (required, validated active)
customerId               Guid? (nullable — null = walk-in)
originalSalesInvoiceId   Guid? (nullable — optional invoice link)
returnDateUtc            DateTime? (nullable)
notes                    string? (nullable)
lines                    List<SalesReturnLineRequest> (at least 1 required)
```

**`SalesReturnLineRequest`:**
```
variantId         Guid (required)
quantity          decimal (must be > 0)
unitPrice         decimal (must be >= 0)
reasonCodeId      Guid (REQUIRED — validated active reason code)
dispositionType   DispositionType (REQUIRED — integer enum 0–5)
notes             string? (nullable)
```

**`UpdateSalesReturnRequest`:** Adds `clearCustomer bool?` and `clearOriginalInvoice bool?` explicit-clear flags.

### Response DTO (`SalesReturnService.ReturnDto`)

```
Id, ReturnNumber (server-generated: RET-NNNNNN)
ReturnDateUtc
CustomerId?, CustomerName?
OriginalSalesInvoiceId?, OriginalInvoiceNumber?
WarehouseId, WarehouseName
CreatedByUserId, CreatedByUsername
Notes?, Status ("Draft"|"Posted"|"Voided")
StockMovementId? (null until posted)
TotalAmount
CreatedAtUtc, PostedAtUtc?
Lines[]: Id, VariantId, Sku, ProductName?, Quantity, UnitPrice, LineTotal,
          ReasonCodeId, ReasonCodeCode, ReasonCodeNameAr, DispositionType (string), Notes?
```

### Original Invoice Reference: Required or Optional?

**NULLABLE in contract.** `originalSalesInvoiceId` is `Guid?` in both Create and Update DTOs.

- If **provided**, the original invoice MUST exist and MUST be `Posted` (enforced in `SalesReturnService.CreateAsync`).
- If **provided**, return quantity per variant is validated not to exceed sold quantity (`ReturnQtyExceedsSold` → HTTP 422).
- If **null**, none of these validations run — return is accepted with no invoice link.

**No-invoice return is fully supported by the current contract.** There is no manager-approval gate enforced at the backend level for no-invoice returns. The `requiresManagerApproval` field exists on `ReasonCode` entity but is not enforced during sales return posting in the current service implementation (NOT proven it's enforced — source read did not reveal the PostAsync disposition validation code path for this flag). This is a **UI-only rule opportunity**.

### Reason / Condition / Destination Logic

- **Reason:** Mandatory per line (`reasonCodeId` required, must be active). Proven from source.
- **Condition / DispositionType:** Mandatory per line. Controls what kind of stock movement is created upon posting. 6 possible values (0–5). Exact enum names not read from source (only integer values in openapi.json schema — NEEDS VERIFICATION of enum member names).
- **Destination Warehouse:** A single warehouse per return header (`warehouseId`). This is the destination for all lines. Per-line warehouse is NOT supported in the current contract.

### Post / Void Behavior

**Post:** Creates `SaleReturnReceipt` stock movement (positive delta = stock in) based on disposition. Creates `CreditNote` ledger entry for AR if customer is present (source comment in `SalesReturnService.cs` class doc).

**Void:** Endpoint exists (`POST /{id}/void`, `SALES_RETURN_VOID`). Error code `SalesReturnVoidNotAllowedAfterPost` (409) is defined and mapped. The status flow comment on `SalesReturn` entity reads: `Draft → Posted → Voided (optional)`. This implies voiding of Posted returns is intended design. However, the specific condition guarding this error code was NOT fully read. **NEEDS VERIFICATION** — whether voiding a Posted return reverses the stock movement.

**Summary:** Returns contract is comprehensive and ready. Built the same pattern as `PurchaseReturnsViewModel`.

---

## 5) Permissions Audit

### Present in `Permissions.cs` and `PermissionCodes.cs`

| Domain | Permission Code | Backend Constant | Description |
|---|---|---|---|
| Sales | `SALES_READ` | `Permissions.SalesRead` | View sales invoices |
| Sales | `SALES_WRITE` | `Permissions.SalesWrite` | Create, update, delete draft invoices |
| Sales | `SALES_POST` | `Permissions.SalesPost` | Post invoices to inventory |
| Sales Returns (read) | `VIEW_SALES_RETURNS` | `Permissions.ViewSalesReturns` | View sales returns |
| Sales Returns (write) | `SALES_RETURN_CREATE` | `Permissions.SalesReturnCreate` | Create and update draft returns |
| Sales Returns (post) | `SALES_RETURN_POST` | `Permissions.SalesReturnPost` | Post returns |
| Sales Returns (void) | `SALES_RETURN_VOID` | `Permissions.SalesReturnVoid` | Void returns (manager) |
| Payments (read) | `PAYMENTS_READ` | `Permissions.PaymentsRead` | View payments |
| Payments (write) | `PAYMENTS_WRITE` | `Permissions.PaymentsWrite` | Create payments |
| Customers (read) | `CUSTOMERS_READ` | `Permissions.CustomersRead` | View customers |
| Customers (write) | `CUSTOMERS_WRITE` | `Permissions.CustomersWrite` | Create/update/delete customers |
| Warehouses (read) | `WAREHOUSES_READ` | `Permissions.WarehousesRead` | View warehouses |
| Warehouses (write) | `WAREHOUSES_WRITE` | `Permissions.WarehousesWrite` | Create/update/delete warehouses |
| Reasons (view) | `VIEW_REASON_CODES` | `Permissions.ViewReasonCodes` | View reason codes |
| Reasons (manage) | `MANAGE_REASON_CODES` | `Permissions.ManageReasonCodes` | Create/update/disable reason codes |
| Accounting | `ACCOUNTING_READ` | `Permissions.AccountingRead` | View AR/AP ledger and balances |

### MainViewModel `CanView*` flags already wired:

| Flag | Permission Checked | Status |
|---|---|---|
| `CanViewSalesRead` | `SALES_READ` | ✅ Already in `RefreshUserState()` |
| `CanViewSalesReturns` | `VIEW_SALES_RETURNS` | ✅ Already wired |
| `CanViewSales` | `CanViewSalesRead || CanViewSalesReturns || CanViewPurchaseReturns` | ✅ Sidebar section computed |
| `CanViewPayments` | `PAYMENTS_READ` | ✅ Already wired |
| `CanViewCustomers` | `CUSTOMERS_READ` | ✅ Already wired |
| `CanViewWarehouses` | `WAREHOUSES_READ` | ✅ Already wired |

### Missing / Gaps

| Gap | Assessment |
|---|---|
| No POS-specific permission code | **By design** — POS uses `SALES_WRITE` + `SALES_POST` same as Sales Admin. A future `POS_ACCESS` permission could gate the POS screen but is not needed for the first implementation. |
| No `CanViewSalesPost` flag in MainViewModel | The `MainViewModel.cs` does NOT have a `CanViewSalesPost` flag. It has `CanViewSalesRead`. The post action is gated per-button on the sales screen (same pattern as `PurchasesViewModel` where post is a command gated by service response, not a separate nav flag). **Not a blocker — follow existing pattern.** |
| No manager-only gate for no-invoice sales returns | Backend has no enforcement. `SALES_RETURN_VOID` is the closest "manager" permission. UI must enforce this convention itself (e.g., disable "no invoice" option unless user has `SALES_RETURN_VOID` or a manager-specific role). |

---

## 6) Warehouse Policy Readiness

### Current Warehouse Entity Fields (grounded in `Warehouse.cs` and `WarehouseEndpoints.cs`)

```
Id          Guid
Code        string (unique, required)
Name        string (required)
Address     string?
IsDefault   bool
IsActive    bool
CreatedAtUtc DateTime
UpdatedAtUtc DateTime?
```

**NOT PRESENT:** `WarehouseType`, `IsSaleable`, `IsQuarantine`, `IsDamaged`, `AllowSales`, or any other semantic classification field.

### Warehouse-Type Semantics Anywhere in Source?

**None found.** The `SalesService.CreateAsync` checks only `w.IsActive`. The `StockService` routes movements by `warehouseId` but has no concept of warehouse category. No validation prevents a "quarantine" warehouse from being selected in a sales transaction.

### Does the Disposition System Already Handle Non-Saleable Stock?

**PARTIAL YES.** The `DispositionType` enum (6 values, 0–5) on `SalesReturnLine` and `DispositionLine` controls what type of stock movement is generated upon posting. This allows directing returned/inspected goods to a specific stock movement type without requiring a separate warehouse schema change. However:

- DispositionType drives the *movement type* — not the *destination warehouse*. The destination warehouse is always the return's header `warehouseId`.
- A "quarantine" or "damaged" warehouse can be created manually in the current UI (Warehouses screen is fully implemented). The user can create "مخزن العزل" and "مخزن الإتلاف" as named warehouses through the existing UI. No schema change needed.

### Recommended Minimal-Change Warehouse Policy

| Stock Category | Recommended Approach | Classification |
|---|---|---|
| **Saleable stock** | Use the active default warehouse (e.g., "المخزن الرئيسي"). No schema change. | **supported now** |
| **Inspection / quarantine** | Create a dedicated warehouse (e.g., "مخزن العزل"). When doing a sales return with disposition "hold" or inspection type, cashier selects this warehouse as the destination. | **desktop-only rule** (naming convention + cashier training) |
| **Damaged / scrap** | Create a dedicated warehouse (e.g., "مخزن الإتلاف"). Use DispositionType = Scrap/Damaged on return lines. Cashier selects this warehouse. | **desktop-only rule** |
| **Rework / re-manufacture** | Create a dedicated warehouse (e.g., "مخزن الإصلاح"). Route via DispositionType = Rework. | **desktop-only rule** |
| **Preventing sales from non-saleable warehouses** | Currently, no server-side enforcement. Any active warehouse can be used in a sales invoice. | **backend-required** if hard enforcement is needed — otherwise UI-only (warehouse combo-box filtering by name/code convention) |

**SAFEST RECOMMENDATION:** Do NOT add a `WarehouseType` column for now. Create the named warehouses ("مخزن العزل", "مخزن الإتلاف") in data, and in the Sales/POS UI expose only warehouses with specific codes/names in the sales invoice warehouse selector. This is a **UI-level filter** that requires no backend change and preserves current architecture.

If hard server-side enforcement is required later (e.g., prevent API misuse), a single `WarehouseType` enum column (`Saleable=0, Quarantine=1, Damaged=2, Rework=3`) can be added as a backend micro-phase with a migration. Until then, **do not add the column**.

---

## 7) Customer Type + Pricing Readiness

### Does Customer Type (Retail/Wholesale) Exist Today?

**NO.** The `Customer` entity has: `Id, Code, Name, Phone, Phone2, Notes, IsActive, CreatedAtUtc`. There is **no** `CustomerType`, `PricingTier`, `IsWholesale`, or any classification field.

**Source evidence:** `Customer.cs` read in full — no such field exists.

### Are RetailPrice / WholesalePrice Already Present on Variants?

**YES.** Both are present:
- `ProductVariant.RetailPrice decimal?` — `Warehouse.cs` analog confirmed in `ProductVariant.cs`
- `ProductVariant.WholesalePrice decimal?`
- Both are returned in `VariantListDto` (from `VariantEndpoints.ListAsync` — proven from source)
- Both are returned in `GET /api/v1/barcodes/{barcode}` (`BarcodeLookupResult` record — proven from `BarcodeEndpoints.cs`)

### Does the Sales Contract Support Unit Price Override?

**YES.** `SalesInvoiceLineRequest.unitPrice` is a plain `decimal` with no server-side enforcement. The caller (UI) sets any price. The backend validates only that `unitPrice >= 0`. There is no backend mechanism that auto-populates `unitPrice` from `retailPrice` or `wholesalePrice` — this is **always a UI decision**.

### Does the Sales Contract Support Line Discount / Invoice Discount / Tax?

| Feature | Support Level | Evidence |
|---|---|---|
| Per-line discount (amount) | **SUPPORTED** — `discountAmount decimal?` in `SalesInvoiceLineRequest` | Proven from `SalesEndpoints.cs` and `openapi.json` schema |
| Per-line discount (percentage) | **NOT SUPPORTED** — no `discountPercent` field | Not in contract |
| Invoice-level discount | **NOT SUPPORTED** — no `invoiceDiscount` field in request or response | Not in contract |
| Invoice-level tax | **NOT SUPPORTED** — no `taxRate`, `taxAmount`, or `taxIncluded` field | Not in contract |
| Tax per line | **NOT SUPPORTED** | Not in contract |

### What is Missing for the User's Desired Pricing Policy?

| Requirement | Status | Backend Change? |
|---|---|---|
| RetailPrice auto-populate on barcode scan for walk-in | UI-only: fetch barcode → `retailPrice` → pre-fill `unitPrice` | No |
| WholesalePrice auto-populate when named customer selected | UI-only: if customer selected, UI applies `wholesalePrice` | No |
| Customer explicitly marked as "wholesale" | **NOT SUPPORTED** — no `CustomerType` field on `Customer` entity | **Backend-required** if server-side enforcement needed; otherwise UI-only per session convention |
| Percentage discount per line | **NOT SUPPORTED** | **Backend-required** — new field `discountPercent decimal?` on line |
| Invoice-level discount | **NOT SUPPORTED** | **Backend-required** — new header fields |
| Tax (VAT / GST) | **NOT SUPPORTED** | **Backend-required** — significant new fields |

**For first Sales/POS launch without tax or invoice-discount:** No backend change is needed. UI can auto-select `retailPrice` for anonymous and `wholesalePrice` for named customers as a soft convention. The cashier can override `unitPrice` freely.

---

## 8) Payment Flow Readiness

### Available Payment Endpoints (grounded in `PaymentEndpoints.cs` and `AccountingService.cs`)

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/api/v1/payments` | `PAYMENTS_READ` | List payments filtered by `partyType`, `partyId`, `q` |
| `GET` | `/api/v1/payments/{id}` | `PAYMENTS_READ` | Get single payment |
| `POST` | `/api/v1/payments` | `PAYMENTS_WRITE` | Create customer or supplier payment |

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/api/v1/accounting/balances/customers` | (AccountingRead implied) | Paged customer outstanding balances |
| `GET` | `/api/v1/accounting/balances/suppliers` | (AccountingRead implied) | Paged supplier outstanding balances |

**`CreatePaymentRequest`:**
```
partyType     string ("customer" | "supplier") — parsed case-insensitive
partyId       Guid (required, validated exists)
amount        decimal (must be > 0)
method        string ("Cash" | "InstaPay" | "EWallet" | "Visa")
walletName    string? (required only when method = "EWallet")
reference     string?
paymentDateUtc DateTime?
```

### Payment Flow Architectures

#### A) POS Paid-Now Sale (Named Customer)

```
1. POST /api/v1/sales               → creates InvoiceDto (status=Draft)
2. POST /api/v1/sales/{id}/post     → status=Posted, creates CustomerReceivable, creates AR ledger debit
3. POST /api/v1/payments            → partyType="customer", partyId=customerId, amount=totalAmount, method="Cash"
                                      → creates Payment record, AR ledger credit (negative)
                                      → outstanding balance returns to zero
```

This is **fully supported today.** The `allowOverpay=false` constraint in the payment endpoint means the payment amount must not exceed the outstanding. Since the payment happens immediately after posting, outstanding = invoice total = payment amount → no problem.

#### B) POS Paid-Now Sale (Anonymous Walk-In)

```
1. POST /api/v1/sales               → customerId: null
2. POST /api/v1/sales/{id}/post     → stock movement only, NO AR entry, NO CustomerReceivable
3. (UI only) — record cash in drawer — NO API call for payment
```

**LIMITATION:** There is no partyId, so payment cannot be recorded in the ledger for anonymous sales. This is by design (entity comment: "null = walk-in retail"). **Cash-only physical convention for anonymous POS.**

#### C) Credit Sale (Sales Admin Screen — Named Customer)

```
1. POST /api/v1/sales               → draft with customer
2. POST /api/v1/sales/{id}/post     → creates CustomerReceivable + AR debit
3. (no immediate payment)           → customer now appears on /api/v1/accounting/balances/customers with outstanding > 0
4. (later) POST /api/v1/payments   → partyType="customer", partyId=customerId, amount=partial or full
                                      → AR credit, reduces outstanding
```

**FULLY SUPPORTED** — this is the exact pattern already implemented for suppliers.

#### D) Customer Payment Receipt

The `ReceiptPrintService` is already implemented. A `PrintCustomerPaymentReceipt()` method needs to be added to `DocumentPrintHelper.cs` mirroring the existing `PrintPaymentReceipt()` pattern. The payment DTO from `GET /api/v1/payments/{id}` has all required data fields.

**Confirmed: UI 2.4-R10 closeout explicitly noted "Customer payment receipts — No separate customer payments page exists yet. When added, DocumentPrintHelper should add PrintCustomerPaymentReceipt() using ReceiptPrintService."**

### Overpayment Constraint

`AccountingService.CreatePaymentAsync` is called in `PaymentEndpoints.cs` with `allowOverpay: false` (hardcoded). This means payment cannot exceed current outstanding balance. At the endpoint level, there is no query parameter to override this. If the business needs to record a deposit/advance payment (before any sale), this is **NOT SUPPORTED** without backend change.

---

## 9) Reusable UI Patterns Already Available

Sales/POS/Returns should reuse these instead of reinventing:

| Pattern | Location | How to Reuse |
|---|---|---|
| **Server-backed typeahead** | `PurchaseReturnsViewModel.cs` (variant picker), `PurchasesViewModel.cs` (supplier search) | Copy the debounced-search pattern (250ms, min 2 chars, CancellationToken, 5–8 results). Use for customer picker, variant search, reason code search in Sales/POS. |
| **PagedListViewModelBase** | `ViewModels/PagedListViewModelBase.cs` — has `OnPageLoadedAsync()` hook | Subclass for `SalesViewModel` and `SalesReturnsViewModel` for list + paging. |
| **Modal create/edit/detail pattern** | `PurchasesViewModel.cs` (IsCreating / IsEditing state flags), `PurchaseReturnsViewModel.cs` | Same pattern: CurrentSale, IsCreating, IsEditing, IsBusy, ErrorMessage. Open modal → fill → call API → refresh list. |
| **NotificationBar auto-dismiss** | `ViewModels/ViewModelBase.cs` (NotificationMessage, NotificationType) | Set `NotificationMessage` and `NotificationType` for Success/Error/Warning. Auto-dismiss on success (5s). Same as all existing pages. |
| **Print service** | `Services/Printing/ReceiptPrintService.cs` via `Helpers/DocumentPrintHelper.cs` | Add `PrintSalesReceipt(SalesDto)` and `PrintCustomerPaymentReceipt(PaymentDto)` to `DocumentPrintHelper`. Common header/fieldrow/signature block already built. |
| **Permission-gated navigation + sidebar** | `ViewModels/MainViewModel.cs` — `CanViewSales`, `CanViewSalesRead`, `CanViewSalesReturns` flags | Already wired. Sales and SalesReturns nav items just need `NavigateTo("Sales")` and `NavigateTo("SalesReturns")` cases added to the switch. |
| **Active warehouse guard** | `SalesService.CreateAsync` — server-side validates `w.IsActive` | UI should filter warehouse combo-box to `isActive=true` warehouses (same as Purchases/PurchaseReturns). Optionally add a UI-only filter by warehouse naming convention to exclude quarantine/damaged warehouses. |
| **Reason code lazy load + empty state** | `PurchaseReturnsViewModel.cs` — `LoadReasonCodesAsync()`, `HasNoReasonCodes`, `NavigateToReasonCodes` command | Reuse exactly. SalesReturns also need reason codes per line. |
| **DispositionType selector** | `PurchaseReturnsViewModel.cs` — `PurchaseReturnLineRequest.DispositionType` (int enum 0–5) | Reuse DTO pattern. SalesReturnLineRequest takes same `DispositionType` integer enum. |
| **FlexibleStringJsonConverter** | `Models/Dtos/PaymentDto.cs` | Use for any field that the backend may serialize as int enum (like `PartyType`). |
| **ApiClient DESERIALIZE_ERROR trace logging** | `Services/Api/ApiClient.cs` (R9 enhancement) | Auto-applies globally — no extra work needed in new ViewModels. |

---

## 10) Gap Table

| Requirement | Already Supported | UI Only | Backend Change Required | Evidence | Recommendation |
|---|---|---|---|---|---|
| **Sales screen — create/edit/delete draft** | ✅ Yes | — | No | `SalesEndpoints.cs` CRUD proven | Build now |
| **Sales screen — post invoice** | ✅ Yes | — | No | `SalesService.PostAsync` proven | Build now |
| **Sales screen — customer credit flow** | ✅ Yes (via separate payment) | — | No | `AccountingService.CreatePaymentAsync` proven | Build now |
| **Sales screen — invoice-level discount** | ❌ No | — | ✅ Required (new header field) | No `invoiceDiscount` in request/response DTO | Defer to later phase or omit |
| **Sales screen — tax field** | ❌ No | — | ✅ Required (new fields + rate table) | No tax field in contract | Defer to later phase |
| **POS screen — barcode lookup** | ✅ Yes | — | No | `BarcodeEndpoints.cs` proven, `GET /api/v1/barcodes/{barcode}` cached 60s | Build now |
| **POS screen — anonymous walk-in sale** | ✅ Yes | — | No | `customerId: null` in `CreateSalesInvoiceRequest` | Build now |
| **POS screen — named customer immediate payment** | ✅ Yes (2 API calls) | — | No | `POST /api/v1/payments` + `partyType=customer` proven | Build now |
| **POS screen — anonymous immediate payment recording** | ❌ Not supported by API | ⚠️ UI cash-drawer convention only | No (by design) | No partyId → cannot record in ledger | Accept limitation; document for ops |
| **POS screen — barcode duplicate scan merge** | ✅ Yes (contract accepts merged qty on single line) | ✅ Pure UI behavior | No | `SalesInvoiceLineRequest` has `quantity decimal` — merging is UI-side | Build now (UI-only) |
| **POS screen — split merged quantity into individual lines** | ✅ Yes (contract accepts multiple lines for same variantId) | ✅ Pure UI behavior | No | No backend constraint preventing two lines with same variantId | Build now (UI-only) |
| **Sales return — with original invoice reference** | ✅ Yes | — | No | `originalSalesInvoiceId` in contract, qty validation against original | Build now |
| **Sales return — without original invoice (no-invoice return)** | ✅ Yes (contract) | ⚠️ Manager approval = UI-only convention | No | `OriginalSalesInvoiceId nullable`, no backend enforcement | UI-only gate using `SALES_RETURN_VOID` or manager role check |
| **Sales return — mandatory reason code** | ✅ Yes | — | No | `reasonCodeId` required per line, validated active | Build now |
| **Sales return — product condition via DispositionType** | ✅ Yes | — | No | `dispositionType` required per line (6 enum values) | Build now |
| **Sales return — destination warehouse** | ✅ Yes (header-level warehouse) | — | No | `warehouseId` on return header; per-line destination = NOT supported | Accept header-level warehouse as destination |
| **Sales return — ReturnQtyExceedsSold validation** | ✅ Yes (when linked to invoice) | — | No | `ErrorCodes.ReturnQtyExceedsSold` 422 proven from source | Build now |
| **Sales return — void after post** | ⚠️ Unclear | ⚠️ Unclear | ⚠️ NEEDS VERIFICATION | `SalesReturnVoidNotAllowedAfterPost` error code exists but exact condition not proven | READ SalesReturnService.VoidAsync before building |
| **Saleable vs non-saleable warehouse policy** | ⚠️ Partial (naming convention only) | ✅ UI filter on warehouse combo-box can limit by code/name | No (minimal path) | No warehouse type field in schema | Use named warehouses + UI filter |
| **Retail vs wholesale customer type** | ❌ No `CustomerType` field | ✅ UI auto-selects price based on whether customer is named | No for soft convention | No `customerType` on `Customer` entity | UI soft convention (no customer = retail, named customer = prompt for retail/wholesale) |
| **RetailPrice / WholesalePrice on variant** | ✅ Yes | — | No | `ProductVariant.RetailPrice`, `.WholesalePrice` proven; in Barcode response | Prices readable and auto-fillable in UI |
| **Immediate payment (POS named customer)** | ✅ Yes | — | No | `POST /api/v1/payments` with `partyType=customer` proven | Build now |
| **Immediate payment (POS anonymous)** | ❌ Not in API | ⚠️ UI-only cash drawer | No | No partyId → no ledger entry | Accept design limitation |
| **Credit sale** | ✅ Yes | — | No | Invoice posting creates AR entry; payment can be deferred | Build now |
| **Later customer payment settlement** | ✅ Yes | — | No | `POST /api/v1/payments` with `partyType=customer` proven | Build now; mirrors supplier payments screen |
| **Per-line discount (amount)** | ✅ Yes | — | No | `discountAmount decimal?` in request proven | Build now |
| **Per-line discount (percentage)** | ❌ No | — | ✅ Required | No `discountPercent` field | Defer |
| **Invoice-level discount** | ❌ No | — | ✅ Required | No invoice discount header field | Defer |
| **Tax (VAT/GST)** | ❌ No | — | ✅ Required | No tax field anywhere in sales contract | Defer to tax phase |
| **Customer quick-add (name + phone)** | ✅ Yes | — | No | `POST /api/v1/customers` — Name required, Code optional (auto-generated), Phone optional | Build now — name+phone is sufficient |
| **Customer search / phone-based suggestion** | ✅ Yes | — | No | `GET /api/v1/customers?q=...` searches Code, Name, Phone, Phone2 | Use typeahead pattern from existing pages |
| **Print sales receipt** | ⚠️ Not built yet | ✅ UI-only addition needed | No | `ReceiptPrintService` exists; needs `PrintSalesReceipt()` in `DocumentPrintHelper` | Add to DocumentPrintHelper during Sales UI phase |
| **Print customer payment receipt** | ⚠️ Not built yet | ✅ UI-only addition needed | No | UI 2.4-R10 closeout explicitly noted this as next step | Add to DocumentPrintHelper during Customer Payments phase |

---

## 11) Recommended Next Phases

**Strict execution order — no phase starts before its predecessor is closed out.**

### UI SALES 1 — Sales Admin Screen (Invoices)
**Prerequisites:** None — backend ready.  
**Scope:**
- Paged list (`GET /api/v1/sales`)
- Create draft (`POST /api/v1/sales`) with: warehouse selector, nullable customer picker (typeahead), multi-line entry (variant search + SKU, qty, unitPrice, discountAmount)
- Edit draft (`PUT /api/v1/sales/{id}`)
- Delete draft (`DELETE /api/v1/sales/{id}`)
- Post invoice (`POST /api/v1/sales/{id}/post`) — with confirm dialog
- Print invoice via `DocumentPrintHelper.PrintSalesReceipt()` (new method)
- `SalesViewModel` (PagedListViewModelBase subclass)
- `SalesPage.xaml` — RTL, Arabic-first

### UI SALES 2 — Customer Payments Screen
**Prerequisites:** UI SALES 1 (need a posted invoice to demonstrate credit flow).  
**Scope:**
- Paged list of customer payments (`GET /api/v1/payments?partyType=customer`)
- Create customer payment (`POST /api/v1/payments` with `partyType=customer`)
- Customer balance viewer (`GET /api/v1/accounting/balances/customers`)
- Print customer payment receipt via `DocumentPrintHelper.PrintCustomerPaymentReceipt()` (new method)
- `CustomerPaymentsViewModel`, `CustomerPaymentsPage.xaml`
- Navigation wired in `MainViewModel` under `CanViewPayments`

### UI SALES 3 — Sales Returns Screen
**Prerequisites:** UI SALES 1 (sales invoices must exist to link returns).  
**Scope:**
- Paged list (`GET /api/v1/sales-returns`)
- Create draft return (`POST /api/v1/sales-returns`) with: warehouse, optional customer picker, optional invoice link, multi-line entry (variant search, qty, unitPrice, reason code, dispositionType)
- Edit draft (`PUT /api/v1/sales-returns/{id}`)
- Delete draft (`DELETE /api/v1/sales-returns/{id}`)
- Post return (`POST /api/v1/sales-returns/{id}/post`)
- Void (manager-only gate in UI using `SalesReturnVoid` permission check)
- No-invoice return: available but flag with `requiresManagerApproval` UI warning
- Reason code load + empty state + inline add (reuse `PurchaseReturnsViewModel` pattern exactly)
- DispositionType selector (reuse `PurchaseReturnLineRequest` pattern)
- `SalesReturnsViewModel`, `SalesReturnsPage.xaml`
- **Before starting:** READ `SalesReturnService.VoidAsync` to confirm whether voiding a Posted return reverses stock movement

### UI SALES 4 — POS Screen (Point of Sale)
**Prerequisites:** UI SALES 1 + UI SALES 2 (payment infrastructure).  
**Scope:**
- Barcode-first input (`GET /api/v1/barcodes/{barcode}`) — immediate lookup
- Automatic line merge on duplicate scans (UI-only)
- Split merged quantity action (UI-only)
- RetailPrice auto-fill for anonymous; prompt retail/wholesale when customer selected
- Anonymous walk-in (no customer required)
- Quick-add customer (`POST /api/v1/customers`) — name + phone only
- Immediate payment on post:
  - Named customer: auto-call `POST /api/v1/payments` after post
  - Anonymous: UI cash drawer confirmation only
- Print receipt after post
- Separate `SalesPosViewModel` (NOT a PagedList subclass — single session/transaction VM)
- Full RTL, Arabic labels, barcode scanner integration

### BACKEND SALES EXT — Optional Extension Phase (if required)
**Only if stakeholders require before launch:**
- Add `CustomerType` enum (`Retail=0, Wholesale=1`) to `Customer` entity + migration + endpoint
- Add invoice-level `discountAmount` and/or `taxRate` fields to `SalesInvoice` + request DTOs
- Add `WarehouseType` to `Warehouse` entity for hard enforcement of saleable vs non-saleable
- Add `allowOverpay: true` option to payment endpoint if advance deposits needed
- **This phase starts ONLY if any of the above is identified as critical before launch.**

---

## 12) Confidence / Blind Spots

The following items could **not** be fully proven from source during this audit:

| Item | Status | Risk |
|---|---|---|
| `SalesReturnService.VoidAsync` logic — does voiding a Posted return actually reverse the stock movement? | **NOT READ** — VoidAsync body was not reached in the read session | HIGH — affects whether Returns void is a real reversal or just a status flag. Must read before building SalesReturns screen. |
| `DispositionType` enum member names (0–5) | **NOT READ** — openapi.json shows integer enum only; source not read for `DispositionType.cs` | MEDIUM — names are needed for the DispositionType dropdown labels in Arabic |
| `SalesReturnPost` — which DispositionType values create which stock movement types | **NOT READ** — SalesReturnService.PostAsync body not fully read | MEDIUM — needed to design DispositionType selector and warehouse routing |
| `GET /api/v1/accounting/balances/customers` response shape | **NOT READ** — openapi.json shows the endpoint exists but response schema not inspected in detail | LOW — needed for Customer Balance viewer in Customer Payments screen |
| Whether `CustomerReceivable` entity is used for anything beyond a flag | **INFERRED** — seen in `SalesService.PostAsync` but `CustomerReceivable` entity shape not read | LOW — unlikely to block Sales UI |
| Whether partial returns are fully validated (e.g., return 5 of 10 sold, then another 5 of 10) | **INFERRED** — `ValidateReturnQtyAsync` is called but its internals not read | LOW — likely works correctly; test with integration tests |
| Whether reason code `requiresManagerApproval` is enforced during sales return posting | **NOT PROVEN** — field exists on `ReasonCode` entity but SalesReturnService PostAsync internals not fully read | MEDIUM — if enforced, UI must present manager approval UX |
| `ErrorCodes.SalesReturnVoidNotAllowedAfterPost` — exact condition (always blocked post-post, or only in some states?) | **AMBIGUOUS** — error code exists but `VoidAsync` not read | HIGH (see first row) |
| `allowOverpay` parameter reachable from outside CallerSide? | **INFERRED** — `PaymentEndpoints.CreateAsync` passes `allowOverpay: false` hardcoded. No way to pass `true` from client. | LOW — limitation is known and documented |
| Customer quick-add from Sales/POS modal — does the phone uniqueness check block duplicate customers? | **NOT PROVEN** — `CustomerEndpoints.CreateAsync` was not fully read past line 100 | LOW — phone is not a unique constraint based on `Customer` entity fields (Name is required, Code is auto-generated, phone is optional) |

---

*Report generated by audit-only scan. No files in `src/` were modified.*
