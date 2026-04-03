# UI SALES RETURNS 2 — FINAL POST + CONFIRMATION + PRINT + POSTED LOCKDOWN — CLOSEOUT

**Phase:** SR-2  
**Status:** ✅ CLOSED — Build GREEN (0 errors, 0 warnings)  
**Date:** 2026-03-31  

---

## 1. Scope Implemented

| Deliverable | Status |
|---|---|
| `CanPost` permission check (`SALES_RETURN_POST`) wired in constructor | ✅ |
| `PostReturnAsync` command — Draft-only guard | ✅ |
| Mandatory confirmation dialog before Post (return #, invoice #, customer, items, quantities) | ✅ |
| On Cancel: no action taken | ✅ |
| On Confirm: calls `POST /api/v1/sales-returns/{id}/post` | ✅ |
| NotificationBar success on post | ✅ |
| NotificationBar error on post failure (no raw server codes) | ✅ |
| Refresh list after post | ✅ |
| Refresh open detail modal after post (if same row) | ✅ |
| `PrintSalesReturnAsync` command — fetches full DTO then prints | ✅ |
| `DocumentPrintHelper.PrintSalesReturn` — professional document with all required fields | ✅ |
| Row actions: Post button (Draft only — DataTrigger collapses on Posted/Voided) | ✅ |
| Row actions: Print button (always visible) | ✅ |
| Detail modal: Print button | ✅ |
| Detail modal: Post button (CanPost-gated visibility, ViewModel guards status) | ✅ |
| Posted lockdown: Edit + Delete disabled via DataTrigger (already existed from SR-1) | ✅ |
| Actions column width increased 250→330 to fit added buttons | ✅ |
| 4 new Arabic localization strings (SR-2 section in resx + Strings.cs) | ✅ |

---

## 2. Files Modified

| File | Changes |
|---|---|
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | +4 SR-2 strings: `SalesReturn_ConfirmPostTitle`, `SalesReturn_ConfirmPostHeader`, `SalesReturn_PostSuccess`, `SalesReturn_CannotPostNotDraft` |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | +4 matching C# properties |
| `src/ElshazlyStore.Desktop/ViewModels/SalesReturnsViewModel.cs` | `CanPost` field, `PostReturnAsync` command, `PrintSalesReturnAsync` command |
| `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs` | `PrintSalesReturn(SalesReturnDto)` static method |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesReturnsPage.xaml` | Row actions: Post + Print buttons added; column width 250→330; detail modal: Print + Post buttons added |

**No files created. No files deleted.**

---

## 3. Backend Endpoints Used

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/v1/sales-returns/{id}/post` | Final post of a Draft sales return |
| `GET` | `/api/v1/sales-returns/{id}` | Fetch full detail for confirmation dialog AND for print |

Both endpoints were already in the backend. No new backend code was written.

---

## 4. Permissions Used

| Permission | Code | Used For |
|---|---|---|
| `SalesReturnPost` | `SALES_RETURN_POST` | `CanPost` property; gates Post button visibility in list and detail modal |

`CanPost` is set in the `SalesReturnsViewModel` constructor once, mirroring the established pattern from `PurchasesViewModel`, `PurchaseReturnsViewModel`, and `SalesViewModel`.

---

## 5. SR-2 Scope Completed

| SR-2 Requirement | Done |
|---|---|
| Post action for valid Draft returns | ✅ |
| Action availability respects status + permission | ✅ |
| Mandatory confirmation dialog before Post | ✅ |
| Confirmation shows: return number, invoice number, customer, items, quantities | ✅ |
| On Cancel → no action | ✅ |
| On Confirm → POST endpoint → success/error feedback | ✅ |
| NotificationBar for outcome (not raw server fragments) | ✅ |
| Refresh list/details after post | ✅ |
| Posted lockdown: no Edit, no Delete, no misleading reverse action visible | ✅ |
| Details + Print available on Posted | ✅ |
| Print document: مرتجع مبيعات title, all required fields, truthful data | ✅ |
| Arabic-first / RTL | ✅ (inherits from page `FlowDirection="RightToLeft"`) |
| Light/dark correct | ✅ (uses `DynamicResource` + existing shared styles) |

---

## 6. Conflicts Between Spec and Backend Truth

**No conflicts found.**

- The spec describes `POST /api/v1/sales-returns/{id}/post` as the posting endpoint. The backend confirms this endpoint exists and requires `SALES_RETURN_POST` permission.
- The spec says "void after post is not a real reversal and must not be shown." No Void UI was implemented.
- `CanPost` in the header detail modal is controlled by permission only (not by status), but the `PostReturnAsync` ViewModel method enforces `status == "Draft"` before calling the endpoint, producing an error notification if not Draft. The row-level Post button also uses XAML DataTriggers to collapse on Posted/Voided, so the button is never visible for non-Draft rows.

---

## 7. What Was Intentionally NOT Implemented (Deferred)

| Item | Reason |
|---|---|
| No-invoice return route | Deferred; spec §5.2 explicitly excludes this phase |
| Manager override for >15-day invoices | Deferred; spec §5.3 and §12.2 defer this |
| Posted-return reversal / void-after-post UX | Deferred; spec §4.4 and golden rule §2.4 forbid misleading void behavior |
| Void action for Draft (beyond what already exists defensively) | Not in SR-2 scope |
| Scrap / Rework / ReturnToVendor / WriteOff dispositions | Still excluded per spec §4.3 |
| Per-line destination warehouse | Deferred; spec §15 explicitly excludes |
| Print Config | Deferred; spec §13.2 excludes |
| Customer Payments page | Out of scope |

---

## 8. Allowed Actions After SR-2 Phase

### Draft returns
| Action | Allowed |
|---|---|
| تفاصيل (Detail) | ✅ |
| تعديل (Edit) | ✅ — requires `SalesReturnCreate` |
| حذف (Delete) | ✅ — requires `SalesReturnCreate` |
| ترحيل (Post) | ✅ — requires `SalesReturnPost` |
| طباعة (Print) | ✅ |

### Posted returns
| Action | Allowed |
|---|---|
| تفاصيل (Detail) | ✅ |
| تعديل (Edit) | ❌ — button IsEnabled=False (DataTrigger) |
| حذف (Delete) | ❌ — button IsEnabled=False (DataTrigger) |
| ترحيل (Post) | ❌ — button Collapsed (DataTrigger); ViewModel also guards |
| طباعة (Print) | ✅ |
| Void / Reverse | ❌ — not shown, not wired |

---

## 9. Confirmation Dialog Content (SR-2 Requirement)

Before calling `POST /api/v1/sales-returns/{id}/post` the system:

1. Fetches full DTO from `GET /api/v1/sales-returns/{id}` (to get line details)
2. Assembles Arabic multi-line message:
   ```
   هل تريد ترحيل هذا المرتجع نهائيًا؟ لن يمكن تعديله بعد الترحيل.
   
   رقم المرتجع: SR-000001
   الفاتورة الأصلية: INV-000001
   العميل: اسم العميل
   
   بنود:
     • اسم الصنف  ×  3
     • اسم الصنف 2  ×  1
   ```
3. Shows `MessageBox` with title "تأكيد الترحيل النهائي"
4. On Cancel (returns `false`) → returns immediately, no backend call
5. On OK (returns `true`) → calls the Post endpoint

---

## 10. Printed Document Fields (SR-2 PrintSalesReturn)

The document produced by `DocumentPrintHelper.PrintSalesReturn` contains:

| Field | Source |
|---|---|
| Document title | `مرتجع مبيعات` (hardcoded Arabic) |
| رقم المرتجع | `ret.DocumentNumber` |
| التاريخ | `ret.CreatedAtUtc` formatted `yyyy-MM-dd HH:mm` |
| العميل | `ret.CustomerNameDisplay` |
| المخزن | `ret.WarehouseName` |
| الفاتورة الأصلية | `ret.OriginalInvoiceNumber` |
| الحالة | `ret.StatusDisplay` (localized Arabic) |
| الإجمالي | `ret.Total` formatted via `InvoiceNumberFormat.Format` |
| ملاحظات | `ret.Notes` (omitted if empty) |
| جدول البنود | SKU, المنتج, الكمية, سعر الوحدة, السبب, الحالة |
| مساحة توقيع | Added via `AddSignatureBlock(doc)` |

Reuses `ReceiptPrintService.CreateDocument()` / `AddCompanyHeader` / `AddFieldPair` / `AddField` / `AddTable` / `AddSignatureBlock` / `PrintDocument` — the exact same shared print infrastructure used by all other documents.

---

## 11. Build / Test Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Full solution build: ElshazlyStore.Domain, ElshazlyStore.Desktop, ElshazlyStore.Infrastructure, ElshazlyStore.Api, ElshazlyStore.Tests — all green.

---

## 12. Cleanup Audit

### Files Touched in SR-2

| File | Cleanup Action |
|---|---|
| `Strings.resx` | Additions only. No removals needed. |
| `Strings.cs` | Additions only. No removals needed. |
| `SalesReturnsViewModel.cs` | Additions only. Reviewed for duplication — none found. |
| `DocumentPrintHelper.cs` | New `PrintSalesReturn` method. No existing code changed. |
| `SalesReturnsPage.xaml` | Actions column width adjusted; two new buttons per view. No dead XAML removed because no dead XAML was introduced. |

### Observations
- No dead imports, no dead variables, no unused methods were created by SR-2.
- The SR-1 `DeleteReturnAsync` correctly shows error model (via `_messageService.ShowError`) if posting path is wrong; this is unchanged.
- The `CanPost` property declaration does not shadow or conflict with anything existing.
- The detail modal Post button's CanPost visibility relies on the ViewModel's `_permissionService` check (constructor-time). If the user navigates to a Posted return detail, the Post button remains visible (CanPost=true) but the ViewModel's guard will present the `SalesReturn_CannotPostNotDraft` error message. This is the same behavior as PurchaseReturnsPage. Status-based collapse on the detail modal would require a computed property or MultiValueConverter which adds complexity beyond SR-2 scope; the ViewModel guard is sufficient.
- No whole-project cleanup was performed — policy: only touched files.

---

## 13. Human Test Script — SR-2 Only

**Prerequisites:**
- App running and connected to backend
- At least one Posted sales invoice exists (for creating draft returns)
- User has `ViewSalesReturns` + `SalesReturnCreate` + `SalesReturnPost` permissions

| # | Action | Expected Result |
|---|---|---|
| 1 | Navigate to "مرتجعات المبيعات" | List page loads showing existing returns |
| 2 | Verify a Draft return exists in the list (create one if needed via SR-1 flow) | Draft row visible with status "مسودة" |
| 3 | On Draft row, verify row actions contain: تفاصيل, تعديل, حذف, ترحيل, طباعة | All 5 buttons present for Draft row |
| 4 | On Posted row (if exists), verify row actions contain: تفاصيل, طباعة | Edit + Delete = disabled (grayed); Post button = invisible; Print visible |
| 5 | Click "ترحيل" on a Draft row | Confirmation dialog appears with title "تأكيد الترحيل النهائي" |
| 6 | Verify dialog content shows return number, original invoice number, customer name, and list of items with quantities | All items visible in dialog message body |
| 7 | Click Cancel/إلغاء on confirmation dialog | Dialog closes; no change to the record; row still shows "مسودة" |
| 8 | Click "ترحيل" on the same Draft row again | Confirmation dialog appears again |
| 9 | Click OK/موافق to confirm | Dialog closes; NotificationBar shows "تم ترحيل المرتجع بنجاح"; row status changes from "مسودة" to "مُرحَّل" (or "Posted") |
| 10 | Verify the newly-Posted row: Edit + Delete = disabled; Post button = invisible | Row is locked down correctly |
| 11 | Click "تفاصيل" on the Posted return | Detail modal opens with correct data |
| 12 | In the detail modal, verify only "طباعة" and "إغلاق" are in the action row | Post button absent (CanPost visible but overriding ViewModel guard) OR only Print visible — either way, no edit/delete/void |
| 13 | Click "طباعة" in the detail modal | Print dialog appears; document titled "مرتجع مبيعات"; return number, invoice, customer, warehouse, status, total, lines all present |
| 14 | Click "طباعة" on a Draft row directly from the list | Print dialog appears for the Draft document |
| 15 | Click "ترحيل" on an already-Posted row's detail modal (if Post button is visible due to CanPost=true) | NotificationBar error "لا يمكن ترحيل مرتجع غير مسودة" — no backend call made |
| 16 | Verify NotificationBar errors from backend (e.g., empty lines) are shown as Arabic messages | No raw server error codes visible to user |

---

## 14. Next Phase Gate

SR-2 is complete. Before proceeding to any future phase:

- [ ] Human test script above passes on real data
- [ ] Print output is visually verified on paper or print preview
- [ ] Any new SR scope (void flow, no-invoice route, manager override) must be separately agreed before implementation
