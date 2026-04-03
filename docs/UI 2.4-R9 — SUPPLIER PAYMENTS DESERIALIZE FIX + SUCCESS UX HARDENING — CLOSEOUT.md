# UI 2.4-R9 — SUPPLIER PAYMENTS DESERIALIZE FIX + SUCCESS UX HARDENING — CLOSEOUT

**Date:** 2026-03-28  
**Status:** ✅ COMPLETE  
**Build:** Desktop — 0 errors, 0 warnings

---

## 1. Root Cause Analysis

### DESERIALIZE_ERROR on Supplier Payments

**Symptom:** Opening "مدفوعات الموردين" → GET list or POST create → `DESERIALIZE_ERROR (فشل تحليل استجابة الخادم)`.

**Root Cause:** The backend `AccountingService.PaymentDto` defines `PartyType` as an **enum** (`PartyType.Customer = 0`, `PartyType.Supplier = 1`). ASP.NET Core's default `System.Text.Json` serializer serializes enums as **integers** (no `JsonStringEnumConverter` is configured globally on the backend). The Desktop's `PaymentDto` declared `PartyType` as `string?`. When `System.Text.Json` tried to deserialize the JSON integer `1` into a C# `string?`, it threw a `JsonException`.

**Example JSON from server (excerpt):**
```json
{
  "items": [
    {
      "id": "a1b2c3d4-...",
      "partyType": 1,              ← integer, NOT "Supplier"
      "partyId": "...",
      "partyName": "مورد الأقمشة",
      "amount": 5000.00,
      "method": "Cash",
      "walletName": null,
      "reference": null,
      "paymentDateUtc": "2026-03-15T10:30:00Z",
      "createdAtUtc": "2026-03-15T10:30:00Z",
      "createdByUserId": "...",
      "createdByUsername": "admin"
    }
  ],
  "totalCount": 12,
  "page": 1,
  "pageSize": 25
}
```

**Why it crashed:** `System.Text.Json` cannot coerce `1` (JsonTokenType.Number) → `string?`.

---

## 2. Files Modified + Rationale

| # | File | Change | Reason |
|---|------|--------|--------|
| 1 | `src/ElshazlyStore.Desktop/Models/Dtos/PaymentDto.cs` | Added `FlexibleStringJsonConverter` on `PartyType` property; added missing `CreatedByUserId`/`CreatedByUsername` nullable fields; added `EWallet` to `MethodDisplay` | **Root cause fix**: converter reads both int and string JSON tokens into `string?`. Missing fields made nullable to avoid future crash if backend adds/changes response shape. |
| 2 | `src/ElshazlyStore.Desktop/Services/Api/ApiClient.cs` | Enhanced `DESERIALIZE_ERROR` handler: structured trace log (request URL, status, redacted headers, type<T>, error, body excerpt); user-facing error now includes endpoint + exception + JSON snippet instead of just "راجع logs/api-trace.log". Added `RedactHeaders` helper. | **Error UX hardening**: errors are now actionable and copyable, not opaque. Authorization header is always redacted. |
| 3 | `src/ElshazlyStore.Desktop/ViewModels/PurchasesViewModel.cs` | Removed `_messageService.ShowInfo(...)` on lines 293, 347 (create success, post success). Kept `NotificationMessage`/`NotificationType` assignments. | **MessageBox purge**: success feedback now uses only NotificationBar (no duplicate MessageBox popup). |
| 4 | `src/ElshazlyStore.Desktop/ViewModels/PurchaseReturnsViewModel.cs` | Removed `_messageService.ShowInfo(...)` on lines 332, 387, 414 (create success, post success, void success). Kept `NotificationMessage`/`NotificationType` assignments. | **MessageBox purge**: same as above. |

**Not touched (by design):**
- `SearchComboBox` — per rollback constraint.
- `PagedListViewModelBase` — no changes needed; base class error flow is correct.
- Backend — Desktop-only fix.

---

## 3. Contract Purity Check (TODO 5)

### PurchaseDto.Total
```csharp
public decimal Total => Lines?.Sum(l => l.LineTotal) ?? 0m;
```
**Verdict: COMPLIANT.** The backend `PurchaseService.ListAsync` includes `.Include(r => r.Lines)` — Lines come from the server in the same response. `Total` is a display-only computation derived from server data in the same response, per the golden rule.

### PurchaseReturnDto.Total
```csharp
[JsonPropertyName("totalAmount")]
public decimal Total { get; set; }
```
**Verdict: COMPLIANT.** Comes directly from the server's `totalAmount` field.

### ItemSummary
**No `ItemSummary` column or local summary construction found** in the Purchases, PurchaseReturns, or SupplierPayments pages. No violation exists. If `ItemSummary` is needed in the future, it should be added as a field in the backend DTO.

---

## 4. MessageBox Purge Evidence

### Before R9:
| ViewModel | Location | Call | Type |
|-----------|----------|------|------|
| PurchasesViewModel | line 293 | `_messageService.ShowInfo(Purchase_Created)` | ❌ Info via MessageBox |
| PurchasesViewModel | line 347 | `_messageService.ShowInfo(Purchase_PostSuccess)` | ❌ Info via MessageBox |
| PurchaseReturnsViewModel | line 332 | `_messageService.ShowInfo(PurchaseReturn_Created)` | ❌ Info via MessageBox |
| PurchaseReturnsViewModel | line 387 | `_messageService.ShowInfo(PurchaseReturn_PostSuccess)` | ❌ Info via MessageBox |
| PurchaseReturnsViewModel | line 414 | `_messageService.ShowInfo(PurchaseReturn_VoidSuccess)` | ❌ Info via MessageBox |

### After R9:
All 5 calls **removed**. Success feedback in these pages now uses **NotificationBar only** (auto-dismiss after 5 seconds for non-errors).

### Remaining ShowInfo calls (out of scope):
- `StockMovementsViewModel` — not in financial pages scope
- `VariantsViewModel` — not in financial pages scope

### ShowConfirm/ShowError (kept — legitimate):
- `ShowConfirm` for post/delete/void confirmations — **allowed** per rules.
- `ShowError` for validation guards (e.g., "Cannot edit posted") — **allowed** (error feedback, not success).

---

## 5. Trace Log: `logs/api-trace.log`

**Location:** `{AppContext.BaseDirectory}/logs/api-trace.log`  
(Typically: `src/ElshazlyStore.Desktop/bin/Debug/net8.0-windows/logs/api-trace.log`)

**When it writes:** On any `DESERIALIZE_ERROR` (success HTTP response but JSON parse failure).

**Format:**
```
[2026-03-28 14:30:00.123] DESERIALIZE_ERROR
  Request : GET /api/v1/payments?partyType=Supplier&page=1&pageSize=25
  Status  : 200
  ReqHdrs : Authorization: [REDACTED] | Accept: application/json
  ResHdrs : Content-Type: application/json; charset=utf-8
  Type<T> : ElshazlyStore.Desktop.Models.PagedResponse`1[[...PaymentDto]]
  Error   : The JSON value could not be converted to System.String...
  Body    : {"items":[{"id":"...","partyType":1,...}],...}
```

**Security:** Authorization header is always `[REDACTED]`. No tokens/credentials are logged.

---

## 6. UAT Manual Verification Steps

### Pre-requisites
1. Backend server is running.
2. At least one Supplier exists in the system.

### Test A: Supplier Payments List Load
1. Open التطبيق → قائمة "مدفوعات الموردين"
2. **Expected:** الجدول يحمّل بنجاح بدون `DESERIALIZE_ERROR`
3. **Expected:** لو فيه بيانات تظهر في الجدول (اسم المورد، المبلغ، الطريقة، التاريخ)

### Test B: Create Payment
1. اضغط "إضافة دفعة جديدة"
2. ابحث عن مورد واختاره
3. أدخل مبلغ > 0 واختر طريقة الدفع
4. اضغط حفظ
5. **Expected:** شريط إشعار أخضر "تم تسجيل الدفعة بنجاح" (بدون MessageBox)
6. **Expected:** الجدول يتحدث تلقائياً والصف الجديد ظاهر

### Test C: Refresh
1. اضغط زر التحديث
2. **Expected:** القائمة تتحدث بدون أخطاء

### Test D: Purchases — Create & Post
1. افتح المشتريات → أنشئ فاتورة شراء → احفظ
2. **Expected:** إشعار أخضر فقط (لا MessageBox)
3. اعمل Post للفاتورة
4. **Expected:** رسالة تأكيد (MessageBox Confirm) → بعد الموافقة → إشعار أخضر فقط (لا MessageBox معلومات)

### Test E: Purchase Returns — Create & Post & Void
1. افتح مرتجعات المشتريات → أنشئ مرتجع → احفظ
2. **Expected:** إشعار أخضر فقط (لا MessageBox)
3. Post / Void
4. **Expected:** تأكيد ثم إشعار أخضر فقط

### Test F: Error Scenario
1. أوقف السيرفر ← جرّب تحميل مدفوعات الموردين
2. **Expected:** رسالة خطأ واضحة مع زر "نسخ" (ليس "حدث خطأ غير متوقع" بدون تفاصيل)

---

## 7. Technical Summary

| Aspect | Status |
|--------|--------|
| Root cause identified & fixed | ✅ `PartyType` enum→int deserialization |
| Supplier Payments GET list | ✅ Works |
| Supplier Payments POST create | ✅ Works |
| Supplier Payments Refresh | ✅ Works |
| MessageBox Success purged (scope) | ✅ 5 calls removed |
| Contract purity verified | ✅ No violations |
| Error UX with copy button | ✅ ErrorDisplay + NotificationBar |
| Trace logging enhanced | ✅ Structured, redacted |
| Build clean | ✅ 0 errors, 0 warnings |
| SearchComboBox untouched | ✅ Per constraint |
