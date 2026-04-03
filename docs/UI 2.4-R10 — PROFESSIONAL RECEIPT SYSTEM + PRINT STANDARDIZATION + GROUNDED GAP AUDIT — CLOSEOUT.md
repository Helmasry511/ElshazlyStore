# UI 2.4-R10 — PROFESSIONAL RECEIPT SYSTEM + PRINT STANDARDIZATION + GROUNDED GAP AUDIT — CLOSEOUT

**Date:** 2026-03-28  
**Status:** ✅ COMPLETE  
**Desktop Build:** 0 errors, 0 warnings  
**Tests:** BLOCKED by pre-existing EF Core version mismatch (Infrastructure 8.0.25 vs Tests 8.0.24) — unrelated to this phase; Desktop-only changes.

---

## 1. Grounded Audit Summary

### 1A. Printed Documents/Receipts in the Desktop App Today

| Document | Method | File | Shared Helper? |
|----------|--------|------|----------------|
| إذن شراء (Purchase Order) | `DocumentPrintHelper.PrintPurchase()` | `Helpers/DocumentPrintHelper.cs` | ✅ Yes — via `ReceiptPrintService` (after R10) |
| مرتجع شراء (Purchase Return) | `DocumentPrintHelper.PrintPurchaseReturn()` | `Helpers/DocumentPrintHelper.cs` | ✅ Yes — via `ReceiptPrintService` (after R10) |
| إيصال دفع (Payment Receipt) | `DocumentPrintHelper.PrintPaymentReceipt()` | `Helpers/DocumentPrintHelper.cs` | ✅ Yes — via `ReceiptPrintService` (after R10) |

All three printing surfaces now route through the shared `ReceiptPrintService`.

### 1B. Print Profiles Endpoint Consumption

`/api/v1/print-profiles` exists in `openapi.json` (GET list, GET by id, POST, PUT, DELETE) along with `/api/v1/print-profiles/{profileId}/rules` (CRUD for rules).

**Desktop consumption: NONE.** No `PrintProfileDto`, no `PrintRuleDto`, no API call to `/api/v1/print-profiles` exists in the desktop codebase. Error codes (`PRINT_PROFILE_NOT_FOUND`, `PRINT_RULE_NOT_FOUND`, etc.) and the navigation stub `PrintConfig` are defined but the page/ViewModel are not implemented.

### 1C. Permission Readiness

`ManagePrintingPolicy` permission code is defined in `PermissionCodes.cs`. The sidebar item `Nav_PrintConfig` is gated behind `CanViewPrintConfig`. Navigation falls through to default (Home) since no `PrintConfigViewModel` exists.

### 1D. Major UI Modules — Status Map

| # | Module/Screen | Nav Param | ViewModel Exists | Page Exists | Backend Ready | DTO Ready | Permission Ready | Status |
|---|---------------|-----------|------------------|-------------|---------------|-----------|------------------|--------|
| 1 | Home | `Home` | ✅ | ✅ | — | — | — | Stub/welcome |
| 2 | Settings | `Settings` | ✅ | ✅ | — | — | — | ✅ Implemented |
| 3 | Products | `Products` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 4 | Variants | `Variants` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 5 | Customers | `Customers` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 6 | Suppliers | `Suppliers` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 7 | Warehouses | `Warehouses` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 8 | Stock Balances | `StockBalances` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 9 | Stock Ledger | `StockLedger` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 10 | Stock Movements | `StockMovements` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 11 | Purchases | `Purchases` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 12 | Purchase Returns | `PurchaseReturns` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 13 | Reason Codes | `ReasonCodes` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 14 | Supplier Payments | `SupplierPayments` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ Fully implemented |
| 15 | Dashboard | `Dashboard` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `DashboardRead` | ❌ Not started |
| 16 | Production | `Production` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `ProductionRead/Write/Post` | ❌ Not started |
| 17 | Sales | `Sales` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `SalesRead/Write/Post` | ❌ Not started |
| 18 | Sales Returns | `SalesReturns` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `ViewSalesReturns` | ❌ Not started |
| 19 | Users | `Users` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `UsersRead/Write` | ❌ Not started |
| 20 | Roles | `Roles` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `RolesRead/Write` | ❌ Not started |
| 21 | Import | `Import` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `ImportMasterData` | ❌ Not started |
| 22 | Print Config | `PrintConfig` | ❌ | ❌ | ✅ (openapi) | ❌ | ✅ `ManagePrintingPolicy` | ❌ Not started |

**14 modules fully implemented. 8 modules (15–22) are sidebar stubs with no ViewModel/page — navigation falls through to Home.**

---

## 2. Files Changed + Rationale

| # | File | Change | Rationale |
|---|------|--------|-----------|
| 1 | `Services/Printing/ReceiptPrintService.cs` | **NEW** — Shared receipt/document printing service with reusable layout primitives | Central shared system: company header (with logo placeholder), field rows, field pairs, data tables, notes, signature blocks (أمين الخزينة + المدير), copy labels (أصل/صورة), tear guides, dual-copy mode, and print driver integration. All future receipts/documents reuse this. |
| 2 | `Helpers/DocumentPrintHelper.cs` | **REFACTORED** — All three print methods now delegate to `ReceiptPrintService` | Eliminated all one-off layout code. Payment receipt upgraded to dual-copy professional A5. Purchase + Purchase Return upgraded with professional headers, field pairs, signature blocks. No callers changed — public API preserved. |

**Not changed:**
- `SupplierPaymentsViewModel.cs` — `PrintPayment` command unchanged; already calls `DocumentPrintHelper.PrintPaymentReceipt(payment)`.
- `PurchasesViewModel.cs` — `PrintPurchaseDocCommand` unchanged; already fetches fresh data then calls `DocumentPrintHelper.PrintPurchase(result.Data)`.
- `PurchaseReturnsViewModel.cs` — `PrintReturnDocCommand` unchanged; same pattern.
- No XAML files changed — print buttons bind to the same commands.
- No DTOs changed — all receipt data comes from existing server-sourced fields.

---

## 3. Print Surfaces Standardized

### 3A. Supplier Payment Receipt (إيصال دفع مورد) — PROFESSIONALIZED

**Layout:** Dual-copy (أصل + صورة) on a single sheet, separated by ✂ tear guide.

Each copy contains:
- Company header: logo placeholder (◻) + "الشاذلي" + horizontal rule
- Document title: "إيصال دفع مورد"
- Field pair: رقم الإيصال + التاريخ (date+time)
- Field: المورد (supplier name)
- Field pair: المبلغ + طريقة الدفع
- Conditional: المرجع (reference, if present)
- Conditional: بواسطة (created by username, if present)
- Conditional: ملاحظات (notes, if present)
- Signature block: توقيع الخزينة | توقيع المدير (compact spacing)
- Copy label: — أصل — or — صورة —

**Data source:** `PaymentDto` from list response (includes `CreatedByUsername` since R9 fix).

### 3B. Purchase Order (إذن شراء) — PROFESSIONALIZED

**Layout:** Single-page professional document.

Contains:
- Company header: logo placeholder + "الشاذلي" + subtitle + horizontal rule
- Document title: "إذن شراء"
- Field pairs: رقم المستند + التاريخ, المورد + المخزن, الحالة + الإجمالي
- Conditional: ملاحظات
- Conditional: line items table (SKU, المنتج, الكمية, تكلفة الوحدة, الإجمالي)
- Signature block: توقيع الخزينة | توقيع المدير

**Data source:** `PurchaseDto` from `GET /api/v1/purchases/{id}` (fresh fetch before print).

### 3C. Purchase Return (مرتجع شراء) — PROFESSIONALIZED

**Layout:** Single-page professional document (same pattern as Purchase).

Contains:
- Company header + "مرتجع شراء"
- Field pairs: رقم المرتجع + التاريخ, المورد + المخزن, الحالة + الإجمالي
- Conditional: ملاحظات
- Conditional: line items table (SKU, المنتج, الكمية, تكلفة الوحدة, السبب)
- Signature block: توقيع الخزينة | توقيع المدير

**Data source:** `PurchaseReturnDto` from `GET /api/v1/purchase-returns/{id}` (fresh fetch before print).

---

## 4. What Remains Non-Standardized and Why

| Item | Reason |
|------|--------|
| Print Config page (إعدادات الطباعة) | ViewModel/page not yet built; `/api/v1/print-profiles` endpoints exist in openapi but no Desktop DTOs or consumption yet. Out of scope for this phase — purely presentation changes were made. |
| Sales / Sales Returns receipts | Sales module not yet implemented (no ViewModel/page). When built, they should use `ReceiptPrintService` for printing. |
| Production documents | Production module not yet implemented. Same recommendation. |
| Customer payment receipts | No separate customer payments page exists yet. When added, `DocumentPrintHelper` should add a `PrintCustomerPaymentReceipt()` using `ReceiptPrintService`. |
| Final company logo | No logo asset provided yet. A placeholder (◻) is rendered in the header. See remarks in `ReceiptPrintService.cs` for exact integration steps. |

---

## 5. Logo / Branding Integration Guide

The logo placeholder is in `ReceiptPrintService.AddCompanyHeader()`.

**To replace with actual logo:**
1. Add logo file to: `src/ElshazlyStore.Desktop/Resources/company-logo.png`
2. Add to `ElshazlyStore.Desktop.csproj`:
   ```xml
   <Resource Include="Resources\company-logo.png" />
   ```
3. In `ReceiptPrintService.cs`, replace the placeholder `Run("◻ ")` with:
   ```csharp
   var logo = new System.Windows.Controls.Image
   {
       Source = new System.Windows.Media.Imaging.BitmapImage(
           new Uri("pack://application:,,,/Resources/company-logo.png")),
       Width = 40, Height = 40
   };
   namePara.Inlines.Add(new InlineUIContainer(logo));
   ```
4. Adjust `Width`/`Height` to match the logo's aspect ratio.

**Company name/subtitle** can be changed in the constants at the top of `ReceiptPrintService`:
```csharp
public const string CompanyName = "الشاذلي";
public const string CompanySubtitle = "للتجارة والتوزيع";
```

---

## 6. Build / Test Result

```
Desktop Build:
  Build succeeded.
  0 Warning(s)
  0 Error(s)

API Build:
  Build succeeded.
  0 Warning(s)
  0 Error(s)

Tests:
  BLOCKED — Pre-existing EF Core version mismatch:
  Infrastructure references Microsoft.EntityFrameworkCore 8.0.25
  Tests project references Microsoft.EntityFrameworkCore 8.0.24
  Error: CS1705 assembly version conflict
  This is NOT caused by R10 changes (Desktop-only phase).
```

---

## 7. Human Test Script

### Prerequisites
1. Backend API server is running.
2. At least one supplier exists.
3. At least one supplier payment exists (or create one first).

### Test A: Supplier Payment Receipt (Dual-Copy)
1. افتح "مدفوعات الموردين"
2. اضغط "طباعة إيصال" على أي دفعة
3. **تأكد:**
   - نافذة الطباعة تظهر
   - اختر "Microsoft Print to PDF" أو طابعة حقيقية
   - الإيصال يحتوي على:
     - ◻ الشاذلي (عنوان الشركة) في الأعلى
     - عنوان المستند: "إيصال دفع مورد"
     - رقم الإيصال + التاريخ (في نفس السطر)
     - اسم المورد
     - المبلغ + طريقة الدفع (في نفس السطر)
     - المرجع (لو موجود)
     - بواسطة: (اسم المستخدم — لو موجود)
     - الملاحظات (لو موجودة)
     - توقيع الخزينة | توقيع المدير (خطوط توقيع)
   - ✂ خط قص يفصل بين النسختين
   - نسختين متطابقتين: — أصل — و — صورة —
   - لا يوجد مساحات فارغة كبيرة
   - التصميم مهني واحترافي

### Test B: Purchase Order Print
1. افتح "المشتريات"
2. اختر فاتورة شراء واضغط "طباعة"
3. **تأكد:**
   - عنوان الشركة (◻ الشاذلي + للتجارة والتوزيع)
   - عنوان المستند: "إذن شراء"
   - الحقول منظمة في أزواج (رقم + تاريخ، مورد + مخزن، حالة + إجمالي)
   - جدول البنود (لو موجودة)
   - توقيع الخزينة + توقيع المدير
   - التصميم مهني

### Test C: Purchase Return Print
1. افتح "مرتجعات المشتريات"
2. اختر مرتجع واضغط "طباعة"
3. **تأكد:**
   - نفس التصميم المهني
   - عنوان "مرتجع شراء"
   - جدول البنود يشمل عمود "السبب"
   - توقيعات في الأسفل

### Test D: Regression — Supplier Payment Flow
1. أنشئ دفعة جديدة لمورد → **تأكد:** تنجح بدون أخطاء + إشعار أخضر
2. حدّث القائمة → **تأكد:** القائمة تتحدث
3. اطبع الدفعة الجديدة → **تأكد:** الإيصال يظهر البيانات الصحيحة

### Test E: Regression — Purchase + Return Flow
1. أنشئ فاتورة شراء جديدة → احفظ → اطبع → **تأكد:** يعمل
2. أنشئ مرتجع شراء → احفظ → اطبع → **تأكد:** يعمل

---

## 8. Next Recommended Phases (Priority Order)

| Priority | Phase Name | Scope | Blocked? |
|----------|-----------|-------|----------|
| 1 | **UI 2.4-R11 — Test Project NuGet Fix** | Fix EF Core version mismatch (8.0.24→8.0.25) in Tests.csproj; restore green test suite | Unblocked |
| 2 | **UI 3.0 — Dashboard** | Build DashboardViewModel + DashboardPage consuming dashboard endpoints from openapi | Unblocked |
| 3 | **UI 3.1 — Sales + Customer Payments** | Sales CRUD + posting + customer payment receipts using shared ReceiptPrintService | Unblocked |
| 4 | **UI 3.2 — Sales Returns** | Sales return CRUD, reason codes integration, print using ReceiptPrintService | Blocked by UI 3.1 |
| 5 | **UI 3.3 — Admin (Users / Roles / Import)** | User/Role CRUD, CSV import for master data | Unblocked |
| 6 | **UI 3.4 — Print Config** | Consume `/api/v1/print-profiles` + `/api/v1/print-profiles/{id}/rules` for printer policy management | Unblocked |
| 7 | **UI 3.5 — Production** | Production module CRUD + posting | Unblocked |

---

## 9. Remaining Blind Spots

| Blind Spot | Impact | Mitigation |
|------------|--------|------------|
| Tests not runnable | Cannot verify backend integration regressions | Fix NuGet version in next phase (UI 2.4-R11) |
| No actual A5 printer test | Dual-copy layout tested via PDF only; physical A5 output untested | User should test on physical printer and report |
| Logo placeholder only | No final branding asset | Documented exact integration steps in ReceiptPrintService.cs + this closeout |
| `CreatedByUsername` availability | If a payment was created before R9, it may not have CreatedByUsername populated in the server DB → field will be skipped (conditional display) | Graceful fallback — field is only shown when present |
| Company name/subtitle hardcoded | Currently "الشاذلي" + "للتجارة والتوزيع" as constants | Easy to change — constants at top of ReceiptPrintService; could be moved to config/settings later |
| Print Config page not built | Users cannot configure print profiles via desktop UI yet | Backend endpoints exist; Desktop consumption is a separate phase |
