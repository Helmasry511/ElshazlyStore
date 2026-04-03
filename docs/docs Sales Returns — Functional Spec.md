# Sales Returns — Functional Spec

## 0) Document Status
- Status: Agreed Functional Spec
- Scope Type: UI + Desktop Workflow Spec
- Related Area: Sales / Cashier / Inventory / Printing
- Execution Mode: Phase-by-phase implementation with closeout report + human test after each phase
- UI Reference: شاشة **الكاشير** هي المرجع البصري الحالي من حيث أحجام الخطوط، وضوح الشاشة، ودعم الوضعين الليلي/النهاري

---

## 1) Purpose
هذه الوثيقة هي المرجع الوظيفي الرسمي لشاشة **مرتجعات المبيعات** داخل برنامج الشاذلي.

الهدف منها:
- تثبيت القواعد التجارية والوظيفية قبل التنفيذ
- منع التخمين أثناء التطوير
- تقسيم التنفيذ إلى مراحل واضحة ومغلقة
- جعل كل مرحلة قابلة للاختبار البشري والإغلاق قبل الانتقال للي بعدها

---

## 2) Golden Rules
1. **Backend هو مصدر الحقيقة الوحيد**
   - لا يجوز للـ UI اختراع أي حالة أو صلاحية أو مسار غير مدعوم فعليًا.
2. **Arabic-first / RTL**
   - الشاشة عربية أولًا
   - RTL صحيح
   - الوضع الليلي والنهاري يجب أن يكونا واضحين ومريحين بصريًا
3. **مرجع الـ UI**
   - المرجع البصري للشاشة هو مستوى وضوح وتنظيم شاشة **الكاشير**
   - مع الاستفادة من نفس فلسفة الأحجام، الوضوح، والتنقل
4. **No fake business behavior**
   - لا يتم إظهار أن posted return يمكن إلغاؤه لو backend لا يدعم ذلك فعليًا
   - لا يتم إظهار مسارات استثنائية وكأنها المسار الطبيعي
5. **NotificationBar-first**
   - الإشعارات الأساسية تكون عبر NotificationBar
   - رسائل مفهومة
   - بدون raw server fragments
6. **Cleanup Policy mandatory**
   - كل مرحلة تنفيذية يجب أن تحتوي على Cleanup Audit للملفات الملموسة فقط
7. **Stop after phase**
   - بعد كل مرحلة: closeout report + human test script + توقف كامل
   - لا يتم الانتقال للمرحلة التالية إلا بعد اجتياز الاختبار البشري

---

## 3) Business Goal
شاشة مرتجعات المبيعات هدفها:
- استرجاع أصناف من فاتورة بيع أصلية
- تحديد الكميات المرتجعة بدقة
- تحديد سبب كل سطر
- تحديد مسار الإرجاع الحالي المدعوم
- ترحيل المرتجع إلى المخزون
- إنشاء مستند واضح وقابل للمراجعة والطباعة
- منع الاستخدام الخاطئ أو المرتجعات المفتوحة بلا ضوابط

---

## 4) Grounded Backend Truth
هذه النقاط هي الحقيقة الحالية المعتمدة التي يجب أن يلتزم بها التنفيذ:

### 4.1 Supported endpoints
- `GET /api/v1/sales-returns`
- `GET /api/v1/sales-returns/{id}`
- `POST /api/v1/sales-returns`
- `PUT /api/v1/sales-returns/{id}`
- `DELETE /api/v1/sales-returns/{id}`
- `POST /api/v1/sales-returns/{id}/post`
- `POST /api/v1/sales-returns/{id}/void`

### 4.2 Current backend capabilities
- المرتجع يدعم Draft / Posted / Voided حسب العقد الحالي
- `originalSalesInvoiceId` في backend nullable
- لكل سطر:
  - quantity
  - unitPrice
  - reasonCodeId
  - disposition
- المخزن على مستوى الـ header وليس لكل سطر

### 4.3 Current posting limitations
في التنفيذ الحالي، الـ posting يدعم فقط:
- `ReturnToStock`
- `Quarantine`

ولا يجوز للـ UI في هذه المرحلة أن يعرض كخيارات Posting فعالة:
- `Scrap`
- `Rework`
- `ReturnToVendor`
- `WriteOff`
- أو أي disposition إضافي غير مدعوم حاليًا

### 4.4 Void limitation
- Void عمليًا يجب اعتباره **Draft-only**
- الـ UI لا يجب أن يسمح أو يوحي بإلغاء مرتجع Posted كعكس ترحيل/عكس محاسبي كامل
- أي reversal للـ posted return خارج نطاق المرحلة الحالية

### 4.5 Accounting / stock implications
عند Post:
- يتم إنشاء أثر مخزني مطابق لما يدعمه السيرفر
- وإذا كان هناك عميل، يتم إنشاء الأثر المالي/الائتماني المدعوم حاليًا
- الـ UI لا يختلق أو يوسع هذا السلوك

---

## 5) UI Policy for This Delivery

### 5.1 Main rule
المسار الطبيعي في هذه المرحلة هو:
**مرتجع مرتبط بفاتورة بيع أصلية فقط**

أي أن واجهة الاستخدام الأساسية تعتمد على:
- اختيار فاتورة بيع أصلية
- تحميل بياناتها
- تحديد الأصناف والكميات القابلة للإرجاع منها

### 5.2 No-invoice return policy
رغم أن الـ backend يسمح حاليًا تقنيًا بمرتجع بدون فاتورة أصلية، فإن سياسة الـ UI في هذا الإصدار هي:

- **لا يتم فتح مسار مرتجع بدون فاتورة أصلية في النسخة الأساسية الحالية**
- يؤجل هذا المسار لمرحلة لاحقة إذا لزم
- لا يظهر للمستخدم كخيار عادي الآن

### 5.3 15-day rule
قرار إداري معتمد:
- **لا يتم السماح بعمل مرتجع لفاتورة بيع يتجاوز تاريخها 15 يومًا**
- الاستثناء الإداري بصلاحية مدير قوية **مؤجل لمرحلة لاحقة**
- في النسخة الحالية:
  - إذا كانت الفاتورة أقدم من 15 يومًا → يتم الرفض في الـ UI برسالة واضحة
  - لا يتم فتح override مؤقت الآن
  - لا يتم التحايل عليه من الواجهة

### 5.4 Allowed return states in this phase
لكل سطر، يعرض للمستخدم فقط:
- `ReturnToStock` = صالح للرجوع للمخزون
- `Quarantine` = يحتاج فحص / عزل

ولا يعرض أي حالة أخرى الآن.

---

## 6) Visual / UX Reference
مرجع الشكل العام:
- شاشة **الكاشير** من حيث:
  - حجم الخطوط
  - وضوح العناصر
  - الراحة البصرية
  - التوازن بين الحقول والمحتوى
  - دعم الوضع الليلي والنهاري

### 6.1 Visual expectations
- الخطوط تكون واضحة ومريحة
- الصفوف المهمة أكبر بصريًا من النصوص الثانوية
- الحقول لا تكون مشتتة أو مكتظة
- الـ modal منظم وواضح
- الوضع الليلي والنهاري سليمين تمامًا
- نفس مستوى الاحترافية الحالي في الكاشير والمبيعات بعد إغلاق مراحلها الأخيرة

---

## 7) Screen Scope

### 7.1 Main page: Sales Returns List
الشاشة الرئيسية تحتوي على:
- زر: إنشاء مرتجع مبيعات
- زر: تحديث
- زر/حقل: بحث
- Paging
- Empty state
- Loading state
- Error state
- NotificationBar

### 7.2 Required columns in the list
- رقم المرتجع
- التاريخ
- رقم الفاتورة الأصلية
- العميل
- المخزن
- الحالة
- الإجمالي
- المستخدم / المنشئ إن كان متاحًا
- إجراءات الصف

### 7.3 Row actions
#### Draft
- تفاصيل
- تعديل
- حذف
- ترحيل

#### Posted
- تفاصيل
- طباعة

#### Not allowed on Posted
- تعديل
- حذف
- Void
- أي rollback يوحي بعكس posted return

---

## 8) Create/Edit Modal Scope

### 8.1 Header section
يحتوي على:
- رقم المرتجع
- الحالة
- التاريخ
- المخزن
- الفاتورة الأصلية
- العميل
- ملاحظات

### 8.2 Original invoice section
هذه أهم نقطة في الشاشة:
- المستخدم يحدد فاتورة البيع الأصلية
- بعد اختيارها:
  - يتم تحميل بيانات الفاتورة
  - يتم إظهار العميل
  - يتم إظهار الأصناف المباعة
  - يتم توضيح:
    - الكمية المباعة
    - الكمية المرتجعة سابقًا
    - الكمية المتاحة للمرتجع

### 8.3 Lines grid
كل سطر يحتوي على الأقل على:
- الصنف
- SKU / Barcode إن كان متاحًا
- الكمية المباعة
- المتاح للإرجاع
- الكمية المرتجعة حاليًا
- سعر الوحدة
- سبب المرتجع
- Return state (`ReturnToStock` / `Quarantine`)
- ملاحظات السطر
- إجمالي السطر

---

## 9) User Flow

### 9.1 Main flow
1. المستخدم يفتح شاشة **مرتجعات المبيعات**
2. يضغط **إنشاء مرتجع**
3. تظهر نافذة modal
4. يختار فاتورة البيع الأصلية
5. إذا كانت الفاتورة أقدم من 15 يومًا:
   - يتم الرفض برسالة واضحة
   - ولا يكمل المستخدم العملية
6. إذا كانت الفاتورة صالحة:
   - يتم تحميل السطور
   - يتم إظهار العميل والمخزن والمعلومات المرتبطة
7. المستخدم يحدد الكميات المرجعة
8. المستخدم يختار السبب لكل سطر
9. المستخدم يختار حالة كل سطر:
   - ReturnToStock
   - Quarantine
10. يحفظ Draft
11. عند الترحيل النهائي:
   - يظهر **MessageBox / Confirmation Dialog**
   - يحتوي على الأصناف التي سيتم إرجاعها والكميات
   - وعند الضغط على **موافق** فقط يتم تنفيذ الترحيل
   - وعند الضغط على **إلغاء / رفض** لا يتم اتخاذ أي إجراء
12. بعد نجاح الترحيل:
   - يظهر NotificationBar بالنجاح
   - تتحدث القائمة
   - يمكن الطباعة
   - يصبح المستند غير قابل للتعديل

---

## 10) Confirmation Behavior
قبل الترحيل النهائي للمرتجع يجب أن يحدث الآتي:

### 10.1 Mandatory confirmation
- تظهر رسالة تنبيه/تأكيد
- تحتوي بشكل واضح على:
  - رقم المرتجع
  - رقم الفاتورة الأصلية
  - أسماء الأصناف التي سيتم إرجاعها
  - الكميات التي سيتم إرجاعها
  - ملخص مختصر للحالات المختارة إن أمكن

### 10.2 On confirm
- عند الضغط **موافق**
  - يتم تنفيذ Post
  - ثم NotificationBar success
  - ثم Refresh

### 10.3 On cancel
- عند الضغط **إلغاء / رفض**
  - لا يتم ترحيل المرتجع
  - لا يتم تنفيذ أي قرار
  - تبقى الشاشة كما هي

---

## 11) Validation Rules

### 11.1 General
- لا يجوز حفظ مرتجع بدون سطور
- لا يجوز حفظ/ترحيل مرتجع بدون سبب لكل سطر
- لا يجوز حفظ/ترحيل مرتجع بدون حالة سطر مدعومة
- لا يجوز حفظ كمية <= 0
- لا يجوز إرجاع كمية أكبر من المتاح

### 11.2 Invoice-linked policy
- لا يتم فتح المرتجع العادي بدون فاتورة أصلية
- لا يجوز اختيار فاتورة غير صالحة
- لا يجوز اختيار فاتورة أقدم من 15 يومًا في هذه المرحلة

### 11.3 Post rules
- لا يجوز ترحيل Draft غير مكتمل
- لا يجوز اختيار disposition غير مدعوم حاليًا
- لا يجوز تعديل أو حذف مرتجع بعد Post

---

## 12) Permissions
الـ UI يجب أن تحترم الصلاحيات الموجودة حاليًا فقط.

### 12.1 Required permissions
- `VIEW_SALES_RETURNS`
- `SALES_RETURN_CREATE`
- `SALES_RETURN_POST`
- `SALES_RETURN_VOID` إن استُخدمت لأي سلوك Draft-only داخلي أو إداري لاحقًا
- وصلاحيات المساندة لقراءة:
  - المبيعات
  - العملاء
  - المخازن
  - الأسباب

### 12.2 Current administrative decision
- override على شرط الـ 15 يوم **غير منفذ الآن**
- سيتم ربطه لاحقًا بصلاحية مدير قوية عندما يتم الاتفاق عليها وتنفيذها في الصلاحيات

---

## 13) Printing Expectations
مطلوب مستند **مرتجع مبيعات** مهني عبر نفس shared print system الحالي.

### 13.1 Printed document must include
- عنوان المستند: مرتجع مبيعات
- رقم المرتجع
- التاريخ
- رقم الفاتورة الأصلية
- العميل
- المخزن
- الحالة
- السطور
- السبب
- return state
- الإجمالي
- المستخدم
- مساحة توقيع/مرجع إذا كانت موجودة في النظام الحالي

### 13.2 Printing limits in this phase
- لا يتم إدخال Print Config هنا
- لا يتم اختراع fields محاسبية غير مدعومة
- لا يتم ادعاء reversal لشيء غير موجود

---

## 14) Notifications and Errors
### 14.1 Success messages
- تم حفظ المرتجع كمسودة
- تم ترحيل المرتجع بنجاح
- تم تحديث المرتجع
- تم حذف المسودة

### 14.2 Error messages
- لا يمكن إرجاع فاتورة أقدم من 15 يومًا
- لا يمكن إرجاع كمية أكبر من المتاح
- لا يمكن ترحيل سطر بدون سبب
- لا يمكن ترحيل سطر بحالة غير مدعومة
- لا يمكن تعديل مرتجع مُرحّل
- لا يمكن حذف مرتجع مُرحّل

### 14.3 Message policy
- NotificationBar-first
- بدون أكواد خام من السيرفر
- بدون رسائل غامضة
- الرسائل النهائية يجب أن تكون واضحة للمستخدم الإداري

---

## 15) Non-Scope
هذه الأشياء خارج نطاق هذه الوثيقة/المرحلة الحالية:
- no-invoice return UI
- manager override الحقيقي لشرط الـ 15 يوم
- posted-return reversal
- void-after-post as real reversal
- scrap/rework/writeoff UI options
- per-line destination warehouse
- Print Config
- shared Compo SearchBox rollout
- Customer Payments Page
- تحسين بصري عام شامل لكل البرنامج

---

## 16) Phase Breakdown

# Phase SR-1 — Sales Returns Core List + Invoice-Linked Draft Flow
## Scope
- إضافة التنقل من الشريط الجانبي
- شاشة القائمة
- البحث/الترتيب/التحديث
- Modal إنشاء/تعديل
- اختيار فاتورة البيع الأصلية
- تحميل السطور
- Validation على:
  - 15-day rule
  - available quantity
  - reason
  - disposition
- حفظ Draft
- تعديل Draft
- حذف Draft

## Must Not Include
- Post
- Print
- أي override إداري جديد
- no-invoice route

## Human Review After SR-1
يجب مراجعة:
1. فتح الشاشة من الشريط الجانبي
2. ظهور القائمة بشكل صحيح
3. إنشاء Draft من فاتورة أصلية
4. تحميل السطور والمتاح للإرجاع
5. منع فاتورة أقدم من 15 يومًا
6. منع كمية أكبر من المتاح
7. حفظ Draft
8. تعديل Draft
9. حذف Draft
10. سلامة الوضعين الليلي والنهاري

---

# Phase SR-2 — Final Post + Confirmation + Print + Posted Lockdown
## Scope
- Post action
- Confirmation dialog قبل الترحيل
- NotificationBar success/error
- Refresh after post
- منع التعديل والحذف على Posted
- طباعة مرتجع المبيعات
- قفل سلوك posted document بشكل صحيح

## Must Not Include
- no-invoice route
- manager override
- posted reversal
- Print Config

## Human Review After SR-2
يجب مراجعة:
1. ترحيل Draft مكتملة
2. ظهور dialog تأكيد يحتوي الأصناف والكميات
3. الضغط على موافق ينفذ الترحيل
4. الضغط على إلغاء لا ينفذ أي شيء
5. ظهور NotificationBar صحيح
6. ظهور المرتجع في القائمة كـ Posted
7. عدم إمكانية التعديل أو الحذف بعد الترحيل
8. طباعة المستند بشكل واضح ومهني
9. عدم وجود ادعاء بإمكانية إلغاء posted return

---

# Phase SR-Future — Administrative Extensions (Deferred)
## Deferred only
- no-invoice return route
- manager override على فواتير أقدم من 15 يومًا
- أي صلاحيات خاصة إضافية
- أي توسعة في dispositions غير المدعومة حاليًا

---

## 17) Human Acceptance for Full Closure
تعتبر عائلة Sales Returns مقفولة عندما أستطيع بشريًا:
1. إنشاء Draft مرتبط بفاتورة أصلية
2. رؤية المتاح للإرجاع بدقة
3. حفظ وتعديل وحذف Draft
4. منع الفواتير الأقدم من 15 يومًا
5. منع الإدخالات غير الصحيحة
6. ترحيل المرتجع بعد MessageBox تأكيد واضح
7. رؤية نجاح العملية في NotificationBar
8. طباعة مستند مرتجع واضح
9. التأكد أن المرتجع المُرحّل غير قابل للتعديل/الحذف
10. عدم ظهور أي مسار استثنائي غير متفق عليه

---

## 18) Cleanup Policy
في كل مرحلة تنفيذية من هذه الوثيقة:

### 18.1 Mandatory cleanup rule
- يتم فحص الملفات الملموسة فقط
- يتم حذف dead code الآمن فقط
- لا يتم فتح whole-project cleanup

### 18.2 What cleanup includes
- using/imports غير المستخدمة
- fields/properties/methods غير المستخدمة
- commands غير المرتبطة
- XAML resources/styles/templates غير المستعملة
- converters/helpers غير المستدعاة
- commented-out legacy code

### 18.3 Safety rule
- لا يتم حذف شيء لمجرد أنه “يبدو” غير مستخدم
- يجب التحقق من:
  - source references
  - bindings
  - DI registrations
  - navigation hooks
  - build/test result

### 18.4 Required closeout section
كل closeout يجب أن يحتوي:
- `## Cleanup Audit`
- What was found
- What was removed
- Why safe
- What was intentionally left
- Build/test verification

---

## 19) Prompting Model
بعد اعتماد هذه الوثيقة:
- يرسل للإيجينت برومبت تنفيذي يقرأ هذا الملف أولًا
- ينفذ **Phase SR-1 فقط**
- يخرج closeout
- ثم نراجع بشريًا
- ثم نرسل له **Phase SR-2** فقط بعد النجاح

---

## 20) Final Rule
أي تعارض بين هذه الوثيقة وبين الـ backend الحقيقي:
- يتم توثيقه في التقرير
- ويتبع التنفيذ **backend truth**
- بدون اختراع سلوك UI أو business flow غير مدعوم