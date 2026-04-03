# PHASE: RET 4 — Returns/Dispositions Performance + Dashboard Correctness

## تقرير التنفيذ | Implementation Report

**التاريخ:** 2 مارس 2026  
**الحالة:** ✅ مكتمل — 210/210 اختبار ناجح

---

## 1. الفهارس (Indexes)

### ملف الترحيل
- **`20260302100000_0016_ret4_perf_indexes.cs`**

### الفهارس الموجودة مسبقاً (B-tree — من تكوينات EF Fluent)
| الجدول | الفهرس | النوع |
|---|---|---|
| `sales_returns` | `(Status, PostedAtUtc)` | Composite B-tree |
| `sales_returns` | `(ReturnNumber)` | Unique |
| `sales_returns` | `(CustomerId)` | B-tree |
| `purchase_returns` | `(Status, PostedAtUtc)` | Composite B-tree |
| `purchase_returns` | `(ReturnNumber)` | Unique |
| `purchase_returns` | `(SupplierId)` | B-tree |
| `inventory_dispositions` | `(Status, PostedAtUtc)` | Composite B-tree |
| `inventory_dispositions` | `(DispositionNumber)` | Unique |
| `inventory_dispositions` | `(WarehouseId)` | B-tree |
| `stock_movements` | `(Type, PostedAtUtc)` | Composite B-tree |
| `stock_movements` | `(Type)` | B-tree |
| `stock_movements` | `(Reference)` | B-tree |

### الفهارس المُضافة في هذه المرحلة (GIN trgm — PostgreSQL)
| الجدول | العمود | اسم الفهرس |
|---|---|---|
| `sales_returns` | `ReturnNumber` | `IX_sales_returns_return_number_trgm` |
| `purchase_returns` | `ReturnNumber` | `IX_purchase_returns_return_number_trgm` |
| `inventory_dispositions` | `DispositionNumber` | `IX_inventory_dispositions_disposition_number_trgm` |

### التسلسلات (Sequences — idempotent)
- `sales_return_number_seq`
- `purchase_return_number_seq`
- `disposition_number_seq`

---

## 2. البحث (Search)

### التحديث
تم توسيع حقول البحث في نقاط النهاية الثلاث لتشمل **كود العميل/المورد/المستودع**:

| الخدمة | حقول البحث (قبل) | حقول البحث (بعد) |
|---|---|---|
| `SalesReturnService` | ReturnNumber, Customer.Name, CreatedBy.Username, Notes | + **Customer.Code** |
| `PurchaseReturnService` | ReturnNumber, Supplier.Name, CreatedBy.Username, Notes | + **Supplier.Code** |
| `DispositionService` | DispositionNumber, Warehouse.Name, CreatedBy.Username, Notes | + **Warehouse.Code** |

### آلية البحث
- يستخدم `SearchExtensions.ApplySearch()` (provider-aware)
- **PostgreSQL:** `ILIKE '%term%'` مع دعم فهارس `gin_trgm_ops`
- **SQLite (اختبارات):** `LOWER(col) LIKE '%term%'`

### تحديد حجم الصفحة (Paging caps)
- جميع نقاط النهاية: `pageSize` محدود بين 1 و 100
- `includeTotal` اختياري (افتراضي: `true`)

---

## 3. لوحة المعلومات (Dashboard) — مؤشرات الأداء الصافية

### تعريفات KPI

| المؤشر | التعريف |
|---|---|
| **TotalSales** | مجموع مبالغ فواتير المبيعات المرحّلة في النطاق الزمني |
| **TotalSalesReturns** | مجموع مبالغ مرتجعات المبيعات المرحّلة في النطاق الزمني |
| **NetSales** | `TotalSales − TotalSalesReturns` |
| **InvoiceCount** | عدد فواتير المبيعات المرحّلة |
| **ReturnCount** | عدد مرتجعات المبيعات المرحّلة |
| **AverageTicket** | `TotalSales / InvoiceCount` (إجمالي) |
| **NetAverageTicket** | `NetSales / InvoiceCount` |
| **TotalQuantity** | كمية المبيعات الإجمالية لكل صنف |
| **ReturnedQuantity** | كمية المرتجع لكل صنف |
| **NetQuantity** | `TotalQuantity − ReturnedQuantity` |
| **TotalRevenue** | إيراد إجمالي لكل صنف |
| **ReturnedRevenue** | إيراد مرتجع لكل صنف |
| **NetRevenue** | `TotalRevenue − ReturnedRevenue` |
| **DispositionLoss** | مجموع الكميات المُتلفة من عمليات الإتلاف المرحّلة |

### التغييرات في DTOs

#### `SalesSummaryDto` (محدّث)
```
TotalSales, InvoiceCount, AverageTicket,
TotalSalesReturns, ReturnCount, NetSales, NetAverageTicket
```

#### `TopProductDto` (محدّث)
```
ProductId, ProductName, VariantId, Sku, Color, Size,
TotalQuantity, TotalRevenue,
ReturnedQuantity, ReturnedRevenue,
NetQuantity, NetRevenue
```

#### `DashboardSummaryDto` (محدّث)
```
Sales, TopProductsByQuantity, TopProductsByRevenue,
LowStockAlerts, CashierPerformance, DispositionLoss
```

### ترتيب المنتجات الأعلى
- **بالكمية:** يُرتّب حسب `NetQuantity` تنازلياً
- **بالإيراد:** يُرتّب حسب `NetRevenue` تنازلياً

---

## 4. الاختبارات

### اختبارات جديدة (5 اختبارات)

| الاختبار | الوصف |
|---|---|
| `SalesSummary_NetSales_SubtractsReturns` | يتحقق أن صافي المبيعات = إجمالي − مرتجعات |
| `TopProducts_NetQuantity_SubtractsReturned` | يتحقق من صحة الكميات والإيرادات الصافية لكل صنف |
| `TopProducts_ByRevenue_SortsByNetRevenue` | يتحقق أن الترتيب يعتمد على صافي الإيراد |
| `SalesSummary_NoReturns_NetEqualsGross` | يتحقق أن الصافي = الإجمالي عند عدم وجود مرتجعات |
| `Summary_IncludesDispositionLoss` | يتحقق من وجود حقل خسائر الإتلاف |

### نتيجة الاختبارات
```
Test Run Successful.
Total tests: 210
     Passed: 210
 Total time: ~18 Seconds
```

---

## 5. الملفات المُعدّلة

| الملف | نوع التعديل |
|---|---|
| `Migrations/20260302100000_0016_ret4_perf_indexes.cs` | **جديد** — فهارس GIN trgm + تسلسلات |
| `Services/DashboardService.cs` | **محدّث** — حسابات صافية + خسائر إتلاف + توثيق KPI |
| `Services/SalesReturnService.cs` | **محدّث** — إضافة Customer.Code للبحث |
| `Services/PurchaseReturnService.cs` | **محدّث** — إضافة Supplier.Code للبحث |
| `Services/DispositionService.cs` | **محدّث** — إضافة Warehouse.Code للبحث |
| `Tests/Api/DashboardTests.cs` | **محدّث** — DTOs + 5 اختبارات صافية جديدة |

---

## 6. ملاحظات التوافق

- جميع تغييرات DTOs **إضافية** (حقول جديدة فقط) — لا يوجد كسر للتوافق مع العملاء الحاليين
- فهارس GIN trgm ملفوفة في `DO $$ ... END $$` لتكون **no-op** على SQLite
- التسلسلات تُنشأ بشكل **idempotent** (`CREATE ... IF NOT EXISTS`)
