# UI 2.4-R8 — CLOSE CRITICAL UI GAPS — CLOSEOUT REPORT

**Phase**: UI 2.4-R8  
**Status**: ✅ COMPLETE (7 of 8 TODOs implemented, 1 BLOCKER documented)  
**Build**: Desktop project — 0 errors, 0 warnings  
**Tests**: Pre-existing EF Core version mismatch in test project (unrelated)

---

## TODO Status Summary

| # | TODO | Status | Notes |
|---|------|--------|-------|
| 1 | Professional lines table | ✅ Done | Header row + LineTotal column + consistent grid layout |
| 2 | Purchase Returns modal UX | ✅ Done | NotificationBar, Print, header row, LineTotal column |
| 3 | Non-Stock Lines UI | ❌ BLOCKER | Backend PurchaseLineRequest has no LineType/Description fields |
| 4 | Supplier Payments screen | ✅ Done | Full CRUD + nav + DI + sidebar |
| 5 | Printing via FlowDocument | ✅ Done | Print for Purchases, Returns, Payments |
| 6 | Global Notification Bar | ✅ Done | Reusable control on 3 pages |
| 7 | Refresh pattern consistency | ✅ Done | NotificationBar replaces MessageBox for success feedback |
| 8 | UI polish | ✅ Done | LTR on numerics, column widths, margins standardized |

---

## TODO 1 — Professional Lines Table

**Approach**: Enhanced existing ItemsControl with dedicated header row and computed LineTotal column rather than risky DataGrid swap (variant search popup depends on ItemsControl layout).

### Files Changed
- `ViewModels/PurchasesViewModel.cs` — Added `LineTotal` computed property + change notifications to `PurchaseLineVm`
- `ViewModels/PurchaseReturnsViewModel.cs` — Added `LineTotal` computed property + change notifications to `PurchaseReturnLineVm`
- `Views/Pages/PurchasesPage.xaml` — Added header row (الصنف / الكمية / تكلفة الوحدة / الإجمالي), added LineTotal column, removed per-row labels (moved to header)
- `Views/Pages/PurchaseReturnsPage.xaml` — Same pattern + added LineTotal column to detail DataGrid

---

## TODO 2 — Purchase Returns Modal UX

### Files Changed
- `Views/Pages/PurchaseReturnsPage.xaml` — NotificationBar added, Print button in row actions + detail modal, lines header row, LineTotal column in detail DataGrid
- `ViewModels/PurchaseReturnsViewModel.cs` — Notification properties (`NotificationMessage`, `NotificationType`), success messages on create/post/void, `PrintReturnDocCommand`

---

## TODO 3 — Non-Stock Lines UI — BLOCKER

**Root Cause**: The OpenAPI contract's `PurchaseLineRequest` schema contains only:
- `variantId` (uuid, required)
- `quantity` (double, required)
- `unitCost` (double, required)

There are **no fields** for `lineType`, `description`, `unitName`, or `category`. The backend exclusively supports stock (variant-based) purchase lines. Non-stock lines cannot be implemented without backend changes.

**Action Required**: Backend must add non-stock line support to `PurchaseLineRequest` before UI can implement this feature.

---

## TODO 4 — Supplier Payments Screen

### New Files
- `Models/Dtos/PaymentDto.cs` — `PaymentDto` (read), `CreatePaymentRequest` (write)
- `ViewModels/SupplierPaymentsViewModel.cs` — Full CRUD VM extending `PagedListViewModelBase<PaymentDto>`, supplier typeahead, payment method selection, create modal, notification, print
- `Views/Pages/SupplierPaymentsPage.xaml` + `.xaml.cs` — RTL page with DataGrid, create modal, NotificationBar

### Registration
- `App.xaml.cs` — `services.AddTransient<SupplierPaymentsViewModel>()`
- `ViewModels/MainViewModel.cs` — `case "SupplierPayments"` in NavigateTo switch
- `Views/MainWindow.xaml` — DataTemplate + sidebar nav button (under Accounting section, gated by `CanViewPayments`)

### API Integration
- GET `/api/v1/payments?partyType=Supplier` — list with paging/search
- POST `/api/v1/payments` — create with `partyType: "Supplier"`

---

## TODO 5 — Printing via FlowDocument

### New Files
- `Helpers/DocumentPrintHelper.cs` — Static helper with `PrintPurchase()`, `PrintPurchaseReturn()`, `PrintPaymentReceipt()` methods using FlowDocument + PrintDialog

### Integration
- `PurchasesViewModel.cs` — `PrintPurchaseDocCommand` (RelayCommand)
- `PurchaseReturnsViewModel.cs` — `PrintReturnDocCommand` (RelayCommand)
- `SupplierPaymentsViewModel.cs` — `PrintPaymentCommand` (RelayCommand)
- XAML: Print buttons added to row actions and detail modals on all 3 pages

---

## TODO 6 — Global Notification Bar

### New Files
- `Views/Controls/NotificationBar.xaml` + `.xaml.cs` — Reusable UserControl with:
  - DependencyProperties: `Message`, `NotificationType` (Success/Warning/Error/Info), `AutoDismissSeconds`
  - Color-coded borders and backgrounds
  - Auto-dismiss timer for non-error messages
  - Copy button for error messages
  - Dismiss button

### Integration
- PurchasesPage.xaml — NotificationBar at Grid.Row="1"
- PurchaseReturnsPage.xaml — NotificationBar at Grid.Row="1"
- SupplierPaymentsPage.xaml — NotificationBar at Grid.Row="1"
- All 3 ViewModels have `NotificationMessage` and `NotificationType` observable properties

---

## TODO 7 — Refresh Pattern Consistency

All 3 pages (Purchases, PurchaseReturns, SupplierPayments) now use NotificationBar for success feedback instead of MessageBox. F5 refresh and تحديث buttons remain functional. Success messages auto-dismiss after 5 seconds.

---

## TODO 8 — UI Polish

- Standardized column widths: SKU/DocNumber columns use fixed widths with LTR, Name columns use star-sizing
- Actions column widths increased to accommodate Print button (280→320 for Purchases, 320→360 for Returns)
- Consistent margins on NotificationBar (24,0,24,4) across all pages
- LineTotal columns added with LTR + N2 formatting in both list and detail DataGrids
- Grid row definitions updated for 5-row layout (title/notification/toolbar/content/paging)

---

## Localization Keys Added

| Key | Arabic Value |
|-----|-------------|
| Nav_SupplierPayments | مدفوعات الموردين |
| Payment_CreateNew | إنشاء دفعة |
| Payment_FormTitle | بيانات الدفعة |
| Payment_Number | رقم الإيصال |
| Payment_Amount | المبلغ |
| Payment_Method | طريقة الدفع |
| Payment_WalletName | اسم المحفظة |
| Payment_Reference | المرجع |
| Payment_AmountRequired | يجب إدخال مبلغ صحيح أكبر من صفر |
| Payment_Created | تم إنشاء الدفعة بنجاح |
| Payment_PrintReceipt | طباعة إيصال |
| Supplier_ViewPayments | عرض مدفوعات المورد |
| Action_Print | طباعة |
| Field_LineType | نوع البند |
| LineType_Stock | صنف مخزني |

---

## Files Changed (Complete List)

### New Files (6)
1. `Views/Controls/NotificationBar.xaml`
2. `Views/Controls/NotificationBar.xaml.cs`
3. `Models/Dtos/PaymentDto.cs`
4. `ViewModels/SupplierPaymentsViewModel.cs`
5. `Views/Pages/SupplierPaymentsPage.xaml`
6. `Views/Pages/SupplierPaymentsPage.xaml.cs`
7. `Helpers/DocumentPrintHelper.cs`

### Modified Files (9)
1. `ViewModels/PurchasesViewModel.cs` — notification props, print command, LineTotal on PurchaseLineVm
2. `ViewModels/PurchaseReturnsViewModel.cs` — notification props, print command, LineTotal on PurchaseReturnLineVm
3. `Views/Pages/PurchasesPage.xaml` — NotificationBar, print buttons, lines header row, LineTotal column, grid row updates
4. `Views/Pages/PurchaseReturnsPage.xaml` — NotificationBar, print buttons, lines header row, LineTotal column, grid row updates
5. `App.xaml.cs` — DI registration for SupplierPaymentsViewModel
6. `ViewModels/MainViewModel.cs` — NavigateTo case for SupplierPayments
7. `Views/MainWindow.xaml` — DataTemplate + sidebar nav button for SupplierPayments
8. `Localization/Strings.resx` — 15 new keys
9. `Localization/Strings.cs` — 15 new accessor properties

---

## Manual UAT Steps

1. **Purchases — Lines Table**: Open Purchases → Create → Add lines → Verify header row visible, LineTotal auto-calculates as Qty × UnitCost
2. **Purchases — Print**: Open Purchases → Click طباعة on any row → Verify PrintDialog opens with document preview
3. **Purchases — Notification**: Create/Post a purchase → Verify green notification bar appears and auto-dismisses
4. **Purchase Returns — Same Tests**: Repeat 1–3 for Purchase Returns page
5. **Supplier Payments**: Navigate to مدفوعات الموردين → Create payment → Verify list populates, print works
6. **Sidebar**: Verify مدفوعات الموردين appears under المحاسبة section

---

## Build Output

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Test project has pre-existing EF Core version mismatch (8.0.24 vs 8.0.25) — unrelated to this phase.
