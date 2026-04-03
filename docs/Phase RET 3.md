# Phase RET 3 — Pre-sale Dispositions (Damage/Theft/Defects) (STRICT)

## تقرير تفصيلي بما تم تنفيذه

**التاريخ:** 2 مارس 2026  
**الحالة:** ✅ مكتمل بالكامل — 205/205 اختبار ناجح — 0 أخطاء بناء — 0 تحذيرات

---

## 1. نظرة عامة

تم تنفيذ نظام **التصرف في المخزون قبل البيع (Pre-sale Dispositions)** بالكامل. هذا النظام يُعالج المشاكل التي تُكتشف في المخزون **قبل عملية البيع** مثل:

- **التالف (Damage)** — بضاعة تالفة
- **السرقة (Theft)** — سرقة مخزنية
- **انتهاء الصلاحية (Expiry)** — انتهاء صلاحية المنتج
- **عدم مطابقة المواصفات (Spec Mismatch)** — المنتج لا يطابق المواصفات
- **عيب تصنيع (Manufacturing Defect)** — عيب من المصنع

---

## 2. الملفات التي تم إنشاؤها (New Files)

### 2.1 Domain Layer — الكيانات

#### `src/ElshazlyStore.Domain/Entities/InventoryDisposition.cs`
كيان رئيسي (Header Entity) يمثل عملية التصرف في المخزون:

| الحقل | النوع | الوصف |
|-------|------|-------|
| `Id` | `Guid` | المعرف الفريد |
| `DispositionNumber` | `string` | رقم تسلسلي تلقائي بصيغة `DISP-NNNNNN` |
| `DispositionDateUtc` | `DateTime` | تاريخ التصرف (تاريخ العمل) |
| `WarehouseId` | `Guid` | المستودع المصدر الذي اكتُشفت فيه المشكلة |
| `Notes` | `string?` | ملاحظات اختيارية |
| `Status` | `DispositionStatus` | الحالة: Draft / Posted / Voided |
| `StockMovementId` | `Guid?` | ربط بحركة المخزون بعد الترحيل |
| `RowVersion` | `byte[]` | للتحكم في التزامن |
| `ApprovedByUserId` | `Guid?` | المستخدم الذي وافق (إذا مطلوب موافقة مدير) |
| `ApprovedAtUtc` | `DateTime?` | تاريخ الموافقة |
| `CreatedByUserId` | `Guid` | من أنشأ العملية |
| `PostedByUserId` | `Guid?` | من رحّل العملية |
| `VoidedByUserId` | `Guid?` | من ألغى العملية |
| `CreatedAtUtc` | `DateTime` | تاريخ الإنشاء |
| `PostedAtUtc` | `DateTime?` | تاريخ الترحيل |
| `VoidedAtUtc` | `DateTime?` | تاريخ الإلغاء |

تم أيضاً إنشاء `DispositionStatus` enum:
```csharp
public enum DispositionStatus
{
    Draft = 0,   // مسودة — قابلة للتعديل
    Posted = 1,  // مرحّلة — نهائية ودائمة
    Voided = 2,  // ملغاة — إلغاء ناعم بدون أثر مخزني
}
```

#### `src/ElshazlyStore.Domain/Entities/InventoryDispositionLine.cs`
بنود (Lines) عملية التصرف:

| الحقل | النوع | الوصف |
|-------|------|-------|
| `Id` | `Guid` | المعرف الفريد |
| `InventoryDispositionId` | `Guid` | ربط بالعملية الرئيسية |
| `VariantId` | `Guid` | المنتج/الصنف المتأثر |
| `Quantity` | `decimal` | الكمية (يجب أن تكون موجبة) |
| `ReasonCodeId` | `Guid` | سبب التصرف (تالف، سرقة، إلخ) |
| `DispositionType` | `DispositionType` | نوع التصرف (Scrap/Quarantine/WriteOff/Rework) |
| `Notes` | `string?` | ملاحظات اختيارية للبند |

---

### 2.2 Infrastructure Layer — إعدادات EF Core

#### `src/ElshazlyStore.Infrastructure/Persistence/Configurations/InventoryDispositionConfiguration.cs`
- جدول: `inventory_dispositions`
- فهارس (Indexes): `DispositionNumber` (فريد), `WarehouseId`, `Status`, `DispositionDateUtc`, `CreatedAtUtc`, مركب (`Status` + `PostedAtUtc`)
- علاقات (FK): `Warehouse` → Restrict, `CreatedBy` → Restrict, `PostedBy` → Restrict, `ApprovedBy` → Restrict, `StockMovement` → SetNull
- الحالة (Status) مخزنة كنص (string conversion)

#### `src/ElshazlyStore.Infrastructure/Persistence/Configurations/InventoryDispositionLineConfiguration.cs`
- جدول: `inventory_disposition_lines`
- الكمية: `numeric(18,4)`
- `DispositionType` مخزن كنص (string conversion)
- علاقات: Parent → Cascade delete, `Variant` → Restrict, `ReasonCode` → Restrict

---

### 2.3 Infrastructure Layer — خدمة الأعمال

#### `src/ElshazlyStore.Infrastructure/Services/DispositionService.cs` (683 سطر)
الخدمة الرئيسية التي تحتوي على كل منطق الأعمال:

**العمليات المتاحة:**

| العملية | الوصف |
|---------|-------|
| `CreateAsync` | إنشاء مسودة جديدة مع التحقق من صحة البيانات |
| `GetByIdAsync` | جلب عملية بالمعرف مع كل البنود والعلاقات |
| `ListAsync` | بحث وعرض مع تقسيم الصفحات والترتيب |
| `UpdateAsync` | تحديث مسودة — يمسح الموافقة إذا تغيرت البنود |
| `DeleteAsync` | حذف مسودة فقط |
| `ApproveAsync` | موافقة مدير — يسجل من وافق ومتى |
| `PostAsync` | ترحيل ذري (Atomic Post) مع إنشاء حركة مخزون |
| `VoidAsync` | إلغاء مسودة فقط (المرحّلة لا يمكن إلغاؤها) |

**تفاصيل الترحيل (PostAsync):**
1. **القفل الذري**: `ExecuteUpdateAsync WHERE Status = Draft` — يمنع الترحيل المزدوج (TOCTOU Prevention)
2. **التحقق من أكواد السبب**: يجب أن تكون نشطة وقت الترحيل
3. **التحقق من الموافقة**: إذا أي بند يحتاج موافقة مدير، يجب وجود موافقة مسبقة
4. **حل المستودعات الخاصة**: تحميل SCRAP, QUARANTINE, REWORK من قاعدة البيانات
5. **إنشاء حركة المخزون**: لكل بند حسب نوع التصرف (التفاصيل في القسم 5)
6. **منع الرصيد السالب**: إذا لم يكن هناك رصيد كافٍ يتم رفض الترحيل
7. **التراجع (Rollback)**: إذا فشل أي شيء بعد القفل، يتم إرجاع الحالة لـ Draft

**توليد رقم التصرف:**
- PostgreSQL: `SELECT nextval('disposition_number_seq')` → `DISP-000001`
- SQLite (الاختبارات): عداد مع retry loop لتجنب التكرار

---

### 2.4 API Layer — نقاط النهاية

#### `src/ElshazlyStore.Api/Endpoints/DispositionEndpoints.cs` (218 سطر)

| Method | Path | Permission | الوصف |
|--------|------|-----------|-------|
| `GET` | `/api/v1/dispositions` | `VIEW_DISPOSITIONS` | عرض القائمة مع بحث وتقسيم صفحات |
| `GET` | `/api/v1/dispositions/{id}` | `VIEW_DISPOSITIONS` | عرض عملية بالتفصيل مع البنود |
| `POST` | `/api/v1/dispositions` | `DISPOSITION_CREATE` | إنشاء مسودة جديدة |
| `PUT` | `/api/v1/dispositions/{id}` | `DISPOSITION_CREATE` | تحديث مسودة |
| `DELETE` | `/api/v1/dispositions/{id}` | `DISPOSITION_CREATE` | حذف مسودة |
| `POST` | `/api/v1/dispositions/{id}/approve` | `DISPOSITION_APPROVE` | موافقة المدير |
| `POST` | `/api/v1/dispositions/{id}/post` | `DISPOSITION_POST` | ترحيل العملية |
| `POST` | `/api/v1/dispositions/{id}/void` | `DISPOSITION_VOID` | إلغاء المسودة |

---

### 2.5 الاختبارات

#### `tests/ElshazlyStore.Tests/Api/DispositionTests.cs` (740 سطر — 20 اختبار)

| # | اسم الاختبار | ما يختبره |
|---|-------------|-----------|
| 1 | `CreateDisposition_Scrap_ReturnsCreated` | إنشاء مسودة بنوع Scrap |
| 2 | `CreateDisposition_Quarantine_ReturnsCreated` | إنشاء مسودة بنوع Quarantine |
| 3 | `CreateDisposition_InvalidType_ReturnToVendor_Rejected` | رفض النوع ReturnToVendor (غير مسموح قبل البيع) |
| 4 | `PostDisposition_Scrap_MovesStockToScrapWarehouse` | ترحيل Scrap → نقل لمستودع SCRAP |
| 5 | `PostDisposition_Quarantine_MovesStockToQuarantineWarehouse` | ترحيل Quarantine → نقل لمستودع QUARANTINE |
| 6 | `PostDisposition_WriteOff_RemovesStockNoDest` | ترحيل WriteOff → خصم بدون وجهة |
| 7 | `PostDisposition_Rework_MovesStockToReworkWarehouse` | ترحيل Rework → نقل لمستودع REWORK |
| 8 | `PostDisposition_NegativeStockPrevented` | منع الرصيد السالب عند الترحيل |
| 9 | `PostDisposition_RequiresManagerApproval_RejectedWithoutApproval` | رفض الترحيل بدون موافقة المدير |
| 10 | `PostDisposition_WithApproval_SucceedsAfterApprove` | نجاح الترحيل بعد الموافقة |
| 11 | `PostDisposition_MixedLines_ApprovalRequiredIfAnyLineNeedsIt` | بنود مختلطة — الموافقة مطلوبة إذا أي بند يحتاجها |
| 12 | `PostDisposition_InactiveReasonCode_Rejected` | رفض ترحيل بكود سبب معطّل |
| 13 | `VoidDraftDisposition_Succeeds` | إلغاء مسودة بنجاح |
| 14 | `VoidPostedDisposition_Rejected` | رفض إلغاء عملية مرحّلة |
| 15 | `GetDisposition_ReturnsDetails` | جلب تفاصيل عملية |
| 16 | `ListDispositions_ReturnsPaged` | عرض القائمة مع تقسيم صفحات |
| 17 | `UpdateDraftDisposition_UpdatesLines` | تحديث بنود المسودة |
| 18 | `UpdateDraftDisposition_ClearsApproval` | تحديث البنود يمسح الموافقة السابقة |
| 19 | `DeleteDraftDisposition_Succeeds` | حذف مسودة بنجاح |
| 20 | `Dispositions_RequiresAuthentication` | التحقق من أن المصادقة مطلوبة |

---

## 3. الملفات التي تم تعديلها (Modified Files)

### 3.1 `src/ElshazlyStore.Domain/Entities/StockMovement.cs`
إضافة نوع حركة مخزون جديد:
```csharp
Disposition = 9  // حركة مخزون ناتجة عن تصرف في المخزون
```

### 3.2 `src/ElshazlyStore.Domain/Common/Permissions.cs`
إضافة 5 صلاحيات جديدة:
```csharp
public const string DispositionCreate  = "DISPOSITION_CREATE";   // إنشاء وتعديل المسودات
public const string DispositionPost    = "DISPOSITION_POST";     // ترحيل العمليات
public const string DispositionApprove = "DISPOSITION_APPROVE";  // موافقة المدير
public const string DispositionVoid    = "DISPOSITION_VOID";     // إلغاء المسودات
public const string ViewDispositions   = "VIEW_DISPOSITIONS";   // عرض العمليات
```
تمت إضافتها أيضاً لقائمة `All` لتُزرع تلقائياً (Auto-seeded).

### 3.3 `src/ElshazlyStore.Domain/Common/ErrorCodes.cs`
إضافة 9 أكواد خطأ جديدة:

| الكود | الوصف |
|-------|-------|
| `DISPOSITION_NOT_FOUND` | العملية غير موجودة |
| `DISPOSITION_ALREADY_POSTED` | لا يمكن تعديل عملية مرحّلة |
| `DISPOSITION_EMPTY` | العملية بدون بنود |
| `DISPOSITION_ALREADY_VOIDED` | العملية ملغاة بالفعل |
| `DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST` | لا يمكن إلغاء عملية مرحّلة |
| `DISPOSITION_NUMBER_EXISTS` | رقم العملية مكرر |
| `DISPOSITION_REQUIRES_APPROVAL` | مطلوب موافقة مدير قبل الترحيل |
| `DISPOSITION_INVALID_TYPE` | نوع تصرف غير مسموح (ReturnToVendor/ReturnToStock) |
| `DESTINATION_WAREHOUSE_NOT_FOUND` | مستودع الوجهة الخاص غير موجود |

### 3.4 `src/ElshazlyStore.Infrastructure/Persistence/AppDbContext.cs`
إضافة DbSets:
```csharp
// Phase RET 3
public DbSet<InventoryDisposition> InventoryDispositions => Set<InventoryDisposition>();
public DbSet<InventoryDispositionLine> InventoryDispositionLines => Set<InventoryDispositionLine>();
```

### 3.5 `src/ElshazlyStore.Infrastructure/Seeding/AdminSeeder.cs`
إضافة زراعة المستودعات الخاصة تلقائياً عند بدء التطبيق:

| المستودع | الغرض |
|----------|-------|
| `QUARANTINE` | احتجاز البضاعة المعلّقة حتى اتخاذ قرار |
| `SCRAP` | وجهة نهائية للبضاعة المخردة |
| `REWORK` | البضاعة المرسلة لإعادة التصنيع/الإصلاح |

الثلاثة مستودعات:
- غير افتراضية (`IsDefault = false`)
- نشطة (`IsActive = true`)
- تُنشأ فقط إذا لم تكن موجودة (idempotent)

### 3.6 `src/ElshazlyStore.Infrastructure/DependencyInjection.cs`
تسجيل الخدمة في حاوية DI:
```csharp
services.AddScoped<DispositionService>();
```

### 3.7 `src/ElshazlyStore.Api/Endpoints/EndpointMapper.cs`
ربط نقاط النهاية:
```csharp
// Phase RET 3
v1.MapDispositionEndpoints();
```

### 3.8 `docs/api.md`
تحديث التوثيق بإضافة:
- قسم كامل **Inventory Dispositions (Phase RET 3)** مع وصف كل الـ Endpoints
- 9 أكواد خطأ جديدة في جدول Error Codes
- 5 صلاحيات جديدة في جدول Permissions

---

## 4. مسار دورة حياة العملية (Status Flow)

```
                   ┌──────────┐
                   │  Draft   │ ← قابلة للتعديل والحذف
                   └────┬─────┘
                        │
            ┌───────────┼───────────┐
            │                       │
            ▼                       ▼
     ┌──────────┐           ┌──────────┐
     │  Posted  │           │  Voided  │
     └──────────┘           └──────────┘
     دائمة — لا تراجع      إلغاء ناعم — لا أثر
```

**القواعد:**
- **Draft → Posted**: يتطلب وجود بنود + أكواد سبب نشطة + موافقة مدير (إذا مطلوبة)
- **Draft → Voided**: إلغاء ناعم بدون أي أثر مخزني
- **Posted ↛ Voided**: ❌ ممنوع — العمليات المرحّلة نهائية ودائمة
- **Posted ↛ Draft**: ❌ ممنوع — لا تراجع

---

## 5. أنواع التصرف وتأثيرها على المخزون

### النوع 1: Scrap (تخريد)
```
المستودع المصدر  →  -الكمية (خصم)
مستودع SCRAP     →  +الكمية (إضافة)
```
**الاستخدام:** بضاعة تالفة نهائياً لا يمكن إصلاحها.

### النوع 2: Quarantine (حجر/احتجاز)
```
المستودع المصدر     →  -الكمية (خصم)
مستودع QUARANTINE   →  +الكمية (إضافة)
```
**الاستخدام:** بضاعة مشتبه بها تحتاج فحص/قرار.

### النوع 3: Rework (إعادة تصنيع)
```
المستودع المصدر  →  -الكمية (خصم)
مستودع REWORK    →  +الكمية (إضافة)
```
**الاستخدام:** بضاعة بها عيب يمكن إصلاحه.

### النوع 4: WriteOff (شطب)
```
المستودع المصدر  →  -الكمية (خصم فقط — لا وجهة)
```
**الاستخدام:** سرقة أو خسارة كاملة — يتم شطب المخزون نهائياً.

### الأنواع المرفوضة (غير مسموحة في Pre-sale):
- ❌ `ReturnToVendor` — هذا يخص Phase RET 2 (مرتجع مشتريات)
- ❌ `ReturnToStock` — هذا يخص Phase RET 1 (مرتجع مبيعات)

---

## 6. نظام موافقة المدير (Manager Approval)

### كيف يعمل:
1. كل بند (Line) مرتبط بـ **كود سبب (ReasonCode)**
2. كل كود سبب له خاصية `RequiresManagerApproval` (true/false)
3. إذا **أي بند واحد** في العملية يحتوي على كود سبب يتطلب موافقة → **العملية بالكامل تحتاج موافقة**
4. الموافقة تتم عبر `POST /api/v1/dispositions/{id}/approve`
5. عند الموافقة يتم تسجيل:
   - `ApprovedByUserId` — من وافق
   - `ApprovedAtUtc` — متى وافق
6. **تحديث البنود يمسح الموافقة** — إذا تم تعديل بنود عملية تمت الموافقة عليها، تُمسح الموافقة ويجب إعادتها
7. محاولة الترحيل بدون الموافقة المطلوبة تُرجع خطأ `403 DISPOSITION_REQUIRES_APPROVAL`

---

## 7. التحقق والضوابط (Validations)

### عند الإنشاء (CreateAsync):
- ✅ يجب وجود بند واحد على الأقل
- ✅ المستودع المصدر يجب أن يكون موجود ونشط
- ✅ كل variant يجب أن يكون موجود
- ✅ كل كود سبب يجب أن يكون موجود ونشط
- ✅ الكميات يجب أن تكون موجبة
- ✅ أنواع التصرف المسموحة: Scrap, Quarantine, WriteOff, Rework فقط
- ❌ مرفوض: ReturnToVendor, ReturnToStock

### عند الترحيل (PostAsync):
- ✅ العملية يجب أن تكون في حالة Draft
- ✅ أكواد السبب يجب أن لا تزال نشطة
- ✅ الموافقة مطلوبة إذا أي كود سبب يتطلبها
- ✅ المستودعات الخاصة (SCRAP/QUARANTINE/REWORK) يجب أن تكون موجودة
- ✅ الرصيد الكافي في المستودع المصدر (لا رصيد سالب)
- ✅ ترحيل ذري (Atomic) — لا يمكن ترحيل نفس العملية مرتين

### عند التحديث (UpdateAsync):
- ✅ يجب أن تكون في حالة Draft
- ✅ نفس التحققات كالإنشاء
- ✅ تحديث البنود يمسح الموافقة السابقة

### عند الإلغاء (VoidAsync):
- ✅ يجب أن تكون في حالة Draft
- ❌ لا يمكن إلغاء عملية مرحّلة (Posted)

### عند الحذف (DeleteAsync):
- ✅ يجب أن تكون في حالة Draft

---

## 8. التحكم في التزامن (Concurrency Control)

### نمط الترحيل الذري:
```csharp
// خطوة 1: قفل ذري — يحدّث الحالة فقط إذا كانت Draft
var claimed = await _db.InventoryDispositions
    .Where(d => d.Id == dispositionId && d.Status == DispositionStatus.Draft)
    .ExecuteUpdateAsync(s => s
        .SetProperty(d => d.Status, DispositionStatus.Posted)
        .SetProperty(d => d.PostedAtUtc, DateTime.UtcNow)
        .SetProperty(d => d.PostedByUserId, userId), ct);

// إذا claimed == 0: شخص آخر رحّل أو العملية غير موجودة
```

### السيناريوهات:
- **طلبان متزامنان**: الأول يفوز والثاني يحصل على `409 POST_CONCURRENCY_CONFLICT`
- **ترحيل مكرر**: لعملية مرحّلة بالفعل يُرجع `200 OK` مع نفس `StockMovementId` (Idempotent)
- **فشل بعد القفل**: إذا فشل إنشاء حركة المخزون، يتم التراجع (Rollback) وإرجاع الحالة لـ Draft

---

## 9. نتائج البناء والاختبار

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 205, Skipped: 0, Total: 205
```

| المقياس | القيمة |
|---------|--------|
| إجمالي الاختبارات | 205 |
| ناجح | 205 ✅ |
| فاشل | 0 |
| متخطى | 0 |
| أخطاء بناء | 0 |
| تحذيرات | 0 |
| اختبارات RET 3 الجديدة | 20 |
| اختبارات سابقة لم تتأثر | 185 |

---

## 10. مخطط قاعدة البيانات الجديد

### جدول `inventory_dispositions`
```sql
CREATE TABLE inventory_dispositions (
    id                  UUID PRIMARY KEY,
    disposition_number  VARCHAR(100) NOT NULL UNIQUE,
    disposition_date_utc TIMESTAMP NOT NULL,
    warehouse_id        UUID NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
    notes               VARCHAR(2000),
    status              VARCHAR(20) NOT NULL,  -- 'Draft' | 'Posted' | 'Voided'
    stock_movement_id   UUID REFERENCES stock_movements(id) ON DELETE SET NULL,
    row_version         BYTEA,
    approved_by_user_id UUID REFERENCES users(id) ON DELETE RESTRICT,
    approved_at_utc     TIMESTAMP,
    created_at_utc      TIMESTAMP NOT NULL,
    created_by_user_id  UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    posted_at_utc       TIMESTAMP,
    posted_by_user_id   UUID REFERENCES users(id) ON DELETE RESTRICT,
    voided_at_utc       TIMESTAMP,
    voided_by_user_id   UUID REFERENCES users(id) ON DELETE RESTRICT
);

-- Indexes
CREATE UNIQUE INDEX IX_inventory_dispositions_disposition_number ON inventory_dispositions(disposition_number);
CREATE INDEX IX_inventory_dispositions_warehouse_id ON inventory_dispositions(warehouse_id);
CREATE INDEX IX_inventory_dispositions_status ON inventory_dispositions(status);
CREATE INDEX IX_inventory_dispositions_disposition_date_utc ON inventory_dispositions(disposition_date_utc);
CREATE INDEX IX_inventory_dispositions_created_at_utc ON inventory_dispositions(created_at_utc);
CREATE INDEX IX_inventory_dispositions_status_posted ON inventory_dispositions(status, posted_at_utc);
```

### جدول `inventory_disposition_lines`
```sql
CREATE TABLE inventory_disposition_lines (
    id                        UUID PRIMARY KEY,
    inventory_disposition_id  UUID NOT NULL REFERENCES inventory_dispositions(id) ON DELETE CASCADE,
    variant_id                UUID NOT NULL REFERENCES product_variants(id) ON DELETE RESTRICT,
    quantity                  NUMERIC(18,4) NOT NULL,
    reason_code_id            UUID NOT NULL REFERENCES reason_codes(id) ON DELETE RESTRICT,
    disposition_type          VARCHAR(30) NOT NULL,  -- 'Scrap' | 'Quarantine' | 'WriteOff' | 'Rework'
    notes                     VARCHAR(2000)
);

-- Indexes
CREATE INDEX IX_inventory_disposition_lines_inventory_disposition_id ON inventory_disposition_lines(inventory_disposition_id);
CREATE INDEX IX_inventory_disposition_lines_variant_id ON inventory_disposition_lines(variant_id);
CREATE INDEX IX_inventory_disposition_lines_reason_code_id ON inventory_disposition_lines(reason_code_id);
```

---

## 11. أمثلة API

### إنشاء مسودة تصرف
```http
POST /api/v1/dispositions
Authorization: Bearer {token}
Content-Type: application/json

{
  "warehouseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "dispositionDateUtc": "2026-03-01T12:00:00Z",
  "notes": "بضاعة تالفة تم اكتشافها أثناء الجرد",
  "lines": [
    {
      "variantId": "a1b2c3d4-...",
      "quantity": 5,
      "reasonCodeId": "e5f6g7h8-...",
      "dispositionType": "Scrap",
      "notes": "غلاف مكسور"
    },
    {
      "variantId": "i9j0k1l2-...",
      "quantity": 3,
      "reasonCodeId": "m3n4o5p6-...",
      "dispositionType": "WriteOff",
      "notes": "مفقود — سرقة"
    }
  ]
}
```

### الاستجابة
```json
{
  "id": "uuid",
  "dispositionNumber": "DISP-000001",
  "dispositionDateUtc": "2026-03-01T12:00:00Z",
  "warehouseId": "uuid",
  "warehouseName": "المستودع الرئيسي",
  "createdByUserId": "uuid",
  "createdByUsername": "admin",
  "notes": "بضاعة تالفة تم اكتشافها أثناء الجرد",
  "status": "Draft",
  "stockMovementId": null,
  "approvedByUserId": null,
  "approvedAtUtc": null,
  "lines": [
    {
      "id": "uuid",
      "variantId": "uuid",
      "sku": "SKU-001",
      "productName": "منتج 1",
      "quantity": 5,
      "reasonCodeId": "uuid",
      "reasonCodeCode": "DAMAGED",
      "reasonCodeNameAr": "تالف",
      "requiresManagerApproval": false,
      "dispositionType": "Scrap",
      "notes": "غلاف مكسور"
    }
  ]
}
```

### موافقة المدير
```http
POST /api/v1/dispositions/{id}/approve
Authorization: Bearer {token}
```

### ترحيل العملية
```http
POST /api/v1/dispositions/{id}/post
Authorization: Bearer {token}
```

### إلغاء المسودة
```http
POST /api/v1/dispositions/{id}/void
Authorization: Bearer {token}
```

---

## 12. ملخص التنفيذ

| البند | الحالة |
|-------|--------|
| كيان `InventoryDisposition` | ✅ مكتمل |
| كيان `InventoryDispositionLine` | ✅ مكتمل |
| حالة `DispositionStatus` enum | ✅ مكتمل |
| نوع حركة `MovementType.Disposition = 9` | ✅ مكتمل |
| 5 صلاحيات جديدة | ✅ مكتمل |
| 9 أكواد خطأ جديدة | ✅ مكتمل |
| إعدادات EF Core (جدولين) | ✅ مكتمل |
| DbSets في AppDbContext | ✅ مكتمل |
| زراعة مستودعات خاصة (QUARANTINE, SCRAP, REWORK) | ✅ مكتمل |
| خدمة DispositionService (683 سطر) | ✅ مكتمل |
| 8 نقاط نهاية API | ✅ مكتمل |
| تسجيل DI + Endpoint Mapping | ✅ مكتمل |
| 20 اختبار تكامل Integration Tests | ✅ مكتمل (20/20 ناجح) |
| توثيق docs/api.md | ✅ مكتمل |
| البناء بدون أخطاء | ✅ 0 errors, 0 warnings |
| كل الاختبارات ناجحة | ✅ 205/205 passed |
