# Customer Payments & Customers Page — Functional Spec

## 0) Document Status
- Status: Agreed Functional Spec
- Scope Type: UI + Desktop Workflow Spec
- Related Area: Customers / Payments / Receipts / Visual Reference
- Execution Mode: Phase-by-phase implementation with closeout report + human test after each phase
- Strategic Goal: إذا نُفذت صفحة العملاء بشكل ممتاز في هذه الوثيقة، تُعتمد **مرجع UI** لباقي الشاشات الجديدة والمعدلة

---

## 1) Purpose
هذه الوثيقة تنظم تطوير:
1. **صفحة العملاء الحالية**
2. **ارتباطاتها المالية**
3. **شاشة/قسم دفعات العملاء**
4. **تحسينات إضافة العميل وتفاصيله**
5. **رفع مستوى التصميم الاحترافي للصفحة** بحيث تصبح نموذجًا بصريًا مرجعيًا

الهدف ليس فقط إضافة الدفع، بل أيضًا:
- رفع جودة صفحة العملاء بصريًا ووظيفيًا
- جعلها مريحة للعين
- احترافية
- ديناميكية بهدوء
- واضحة في الوضعين النهاري والليلي
- مع أزرار وانفعالات مرئية حديثة وناعمة دون إزعاج أو وميض حاد

---

## 2) Golden Rules
1. **Backend هو مصدر الحقيقة الوحيد**
   - لا اختراع لأي endpoint أو permission أو financial behavior غير موجود فعليًا
2. **Arabic-first / RTL**
   - العربية هي الأساس
   - RTL صحيح
3. **النهاري والليلي**
   - الوضع الليلي الحالي إذا كان صحيحًا لا يُكسر
   - التحسين البصري الأساسي يكون في تناسق الألوان والوضوح والراحة
4. **Visual upgrade without chaos**
   - الصفحة تصبح أكثر احترافية ومرونة وحيوية
   - لكن بدون مبالغة أو وميض مزعج أو ألوان متصادمة
5. **This page is a visual candidate reference**
   - إذا أُنجزت الصفحة بشكل ممتاز، يمكن اعتمادها لاحقًا كمرجع لشاشات أخرى
6. **NotificationBar-first**
   - الرسائل الأساسية عبر NotificationBar
   - رسائل واضحة، قصيرة، نظيفة
7. **Cleanup Audit mandatory**
   - كل مرحلة تنفيذية يجب أن تحتوي على Cleanup Audit للملفات الملموسة فقط
8. **Phase-by-phase delivery**
   - لا تنفيذ شامل مرة واحدة
   - كل مرحلة تُغلق بعد closeout + human test

---

## 3) Business Goal
الهدف التجاري من هذه العائلة هو:

### 3.1 Customers page
- جعل صفحة العملاء أكثر عملية ووضوحًا واحترافية
- تسهيل:
  - إضافة العميل
  - تعديل العميل
  - مراجعة حالة العميل
  - الاطلاع على ملخصه
  - الوصول إلى حركته المالية لاحقًا

### 3.2 Customer payments
- تمكين تسجيل دفعات العملاء بشكل منظم
- تتبع ما تم دفعه
- عرض المرجع وطريقة الدفع
- طباعة إيصال سداد احترافي
- ربط الدفعة بالعميل الحقيقي
- تمهيد ربطها لاحقًا مع:
  - رصيد العميل
  - حركة البيع الآجل
  - صفحة العملاء
  - الحسابات

---

## 4) Grounded Backend Truth
هذه الوثيقة تفترض الحقائق التالية فقط، وأي تنفيذ يجب أن يتأكد منها أثناء التنفيذ من المصدر الحقيقي:

### 4.1 Generic payments contract
- يوجد مسار generic للمدفوعات
- أي Customer Payments يجب أن تبنى عليه
- ولا يتم اختراع endpoint مخصص جديد للعميل إذا لم يكن موجودًا

### 4.2 Existing customers page
- صفحة العملاء موجودة حاليًا بالفعل
- التعديل سيكون على شاشة موجودة، وليس إنشاء وحدة من الصفر

### 4.3 Scope discipline
- إذا كانت بعض التفاصيل الإضافية للعميل غير مدعومة Backend-side حاليًا:
  - لا يتم اختراعها
  - توضع في deferred/open decisions
  - أو يتم تحسين العرض الحالي للبيانات المدعومة فقط

### 4.4 Existing shared systems
يجب إعادة استخدام:
- NotificationBar
- shared print system
- approved light/dark theme behavior
- current modal/dialog patterns
- current permissions patterns
- current cleanup policy

---

## 5) Product Direction
## 5.1 Main direction
العمل هنا له شقان متوازيان:

### A) Functional / operational
- Customer payments
- ربط مالي واضح
- عرض وتحريك الدفعات
- إيصال دفع

### B) UI / UX / visual
- صفحة العملاء تصبح صفحة احترافية جدًا
- مريحة للعين
- dynamic but calm
- ذات أزرار أكثر انسيابية
- rounded
- hover/press/selection states جميلة لكن غير مزعجة
- صالحة لتكون baseline مرجعية لباقي الشاشات

---

## 6) Visual Design Charter
هذه الصفحة لها **ميثاق تصميم** خاص يجب أن يلتزم به الإيجينت:

### 6.1 Light mode
- ألوان متناسقة ومريحة
- لمعة احترافية خفيفة
- تدرجات subtle وليس flashy
- زرار بحدود ناعمة وrounded
- Hover states سلسة
- بدون ألوان حادة أو طفولية

### 6.2 Dark mode
- يثبت كمرجع صحيح
- لا يتم كسره
- لا يتم التضحية بالوضوح من أجل الشكل
- contrast واضح
- القوائم المنسدلة والحقول والـ selected values كلها مقروءة

### 6.3 Buttons
- Rounded corners
- slightly glossy / polished feel
- smooth hover / press transitions
- لا تكون مربعة جافة
- لا تكون متضخمة أو مبالغًا فيها

### 6.4 Motion / dynamics
- subtle only
- no heavy flicker
- no distracting flash
- no aggressive animation

### 6.5 Typography
- أحجام واضحة ومريحة
- عناوين أقوى بصريًا
- بيانات مهمة أوضح من البيانات الثانوية
- الأرقام المالية والرصيد والدفعات واضحة جدًا

### 6.6 Visual reference intent
إذا خرجت هذه الصفحة ممتازة:
- يتم اعتمادها لاحقًا كـ visual reference
- لباقي صفحات البرنامج التي تحتاج upgrade

---

## 7) Functional Scope

## 7.1 In-scope overall
- Upgrade صفحة العملاء الحالية
- تحسين list/details/create/edit experience
- إضافة القسم/الشاشة الخاصة بمدفوعات العملاء
- إظهار الارتباط المالي للعميل
- طباعة إيصال دفعة عميل
- جعل الصفحة متماسكة بصريًا ومهنيًا

## 7.2 Out of scope for now
- صفحة الموردين في هذه العائلة
- Print Config العامة
- shared Compo SearchBox rollout على مستوى المشروع كله
- redesign شامل لكل البرنامج
- تغيير backend schema بدون ضرورة واضحة ومثبتة
- full accounting dashboard
- whole-app animation system

---

## 8) Page Architecture

## 8.1 Customers page must become the hub
صفحة العملاء تكون هي **المحور الأساسي** لعلاقة العميل بالمعلومات والدفعات.

### الصفحة يجب أن تحتوي مبدئيًا على:
- قائمة العملاء
- إضافة عميل
- تعديل عميل
- تفعيل/تعطيل
- تفاصيل العميل
- قسم/تبويب/منطقة خاصة بمدفوعات العميل
- ملخص مالي واضح للعميل إن كان مدعومًا من الـ backend

## 8.2 Customer Payments should be linked to customer context
دفعات العملاء لا تكون شاشة مفصولة بلا سياق.
بل يجب أن يكون هناك أحد هذين المسارين أو كلاهما إن أمكن:
- من داخل صفحة العميل
- أو شاشة مدفوعات عملاء مرتبطة بسياق العميل الحالي

القرار التنفيذي النهائي يكون حسب أفضل ما يدعمه التصميم والعقد الحالي، لكن:
- لا يجب أن تبدو الدفعات منفصلة تمامًا عن العميل
- ولا يجب أن يضطر المستخدم للبحث عن العميل من الصفر كل مرة بدون داعٍ

---

## 9) Customer Page Upgrade Scope

## 9.1 Customers list
القائمة يجب أن تصبح أكثر وضوحًا واحترافية من الحالية، مع الحفاظ على التشغيل.

### المتطلبات:
- DataGrid واضح
- spacing أفضل
- headers أقوى
- actions أوضح
- حالات:
  - loading
  - empty
  - error
- البحث الحالي يبقى سليمًا
- dark/light mode صحيحان

### الأعمدة الأساسية:
- الاسم
- الهاتف
- الحالة
- أي حقول أساسية مدعومة حاليًا
- summary مالي مختصر إن كان مدعومًا دون تعقيد

## 9.2 Customer create/edit modal
بما أن المستخدم طلب تعديل إضافة العميل وتفاصيله، فالمطلوب:
- تحسين الـ modal بصريًا بقوة
- الحفاظ على نفس النمط العام للتشغيل
- جعل الحقول أوضح
- ترتيب الحقول أفضل
- الأزرار أكثر انسيابية واحترافية
- الوضعين الليلي والنهاري صحيحين

### قاعدة مهمة
- لا يتم اختراع حقول backend جديدة إذا لم تكن مدعومة
- لكن يتم تحسين:
  - العرض
  - التنظيم
  - grouping
  - labels
  - spacing
  - hierarchy
- وإذا كانت هناك حقول مدعومة لكن غير معروضة حاليًا بشكل جيد، يتم استغلالها

## 9.3 Customer details
تفاصيل العميل يجب أن تصبح أوضح وأكثر قيمة:
- بيانات العميل الأساسية
- الحالة
- ملخص المدفوعات
- ملخص أي مديونية/متبقي إذا كان مدعومًا
- آخر الدفعات أو سجل الدفع المختصر إن أمكن
- مدخل واضح للوصول إلى الدفعات

---

## 10) Customer Payments Functional Scope

## 10.1 Main goal
تمكين المستخدم من:
- إنشاء دفعة جديدة للعميل
- مراجعة سجل دفعات العميل
- طباعة إيصال سداد
- تتبع المرجع وطريقة الدفع
- الوصول للدفع من سياق العميل

## 10.2 Payment create flow
يجب أن يدعم:
- اختيار العميل أو استخدام العميل الحالي من سياق الصفحة
- إدخال المبلغ
- اختيار طريقة الدفع
- إدخال المرجع
- ملاحظات داخلية إذا كانت مدعومة/مطلوبة
- حفظ الدفعة
- تحديث القائمة/السجل
- طباعة الإيصال

## 10.3 Payment methods
في هذه الوثيقة، الهدف أن تكون طرق الدفع واضحة وسهلة ومهنية.
لكن التنفيذ الفعلي يجب أن يلتزم بما يدعمه العقد الحالي.

الواجهة يجب أن تكون قابلة لعرض/اختيار طرق مثل:
- نقدي
- فيزا
- إنستاباي
- محفظة إلكترونية
- تحويل/مرجع بنكي
- أخرى  
إذا كان هذا متوافقًا مع العقد الحالي.

وأي provider-specific enrichment:
- يطبق فقط إذا كان مدعومًا أو يمكن تمثيله نصيًا بدون كسر العقد

## 10.4 Receipt
إيصال دفعة العميل يجب أن:
- يعيد استخدام shared print system
- يكون احترافيًا
- واضحًا
- يحتوي على:
  - اسم العميل
  - رقم/مرجع الدفعة
  - طريقة الدفع
  - المرجع
  - المبلغ
  - التاريخ
  - المستخدم
- لا يعتمد على Print Config في هذه المرحلة

---

## 11) UX Rules

## 11.1 Customers page
- الصفحة ليست مجرد CRUD جاف
- يجب أن تشعر أنها شاشة “مهمة”
- فيها hierarchy بصري واضح
- أزرارها مريحة وناعمة
- قابلة لتكون مرجعًا لباقي البرنامج

## 11.2 Payment interactions
- الدفع يجب أن يكون واضحًا ومباشرًا
- لا رسائل كاذبة
- لا raw server errors
- النجاح واضح
- الفشل واضح
- الارتباط بالعميل واضح

## 11.3 Night/Day mode
- أي input/dropdown/details/cards/actions touched in this phase must be correct in both modes
- لا يتم كسر ما تم إصلاحه سابقًا في المبيعات والكاشير

---

## 12) Validation Rules
- لا دفعة بدون عميل
- لا دفعة بدون مبلغ صالح
- لا دفعة بدون طريقة دفع إذا كان العقد/الواجهة يتطلبان ذلك
- المرجع يكون مطلوبًا أو اختياريًا حسب ما يدعمه/يفرضه العقد في المسار الحالي
- لا نجاح UI قبل نجاح API الحقيقي
- لا refresh وهمي
- لا print success قبل وجود عملية ناجحة فعلية

---

## 13) Notifications & Messages
- NotificationBar-first
- رسائل واضحة جدًا
- بدون أكواد أخطاء خام
- النجاح:
  - تم إضافة الدفعة
  - تم تحديث العميل
  - تم حفظ العميل
  - تم طباعة الإيصال
- الفشل:
  - رسالة حقيقية مرتبطة بالفعل الذي فشل
- لا تبقى الرسائل stale عند التنقل

---

## 14) Printing Expectations
في هذه العائلة:
- طباعة إيصال دفعة عميل مطلوبة
- عبر shared print system الحالي
- بدون فتح Print Config الآن
- بدون فتح barcodes print system الآن
- نفس مستوى الاحترافية الحالي في المطبوعات المنجزة

---

## 15) Non-Scope
هذه الأشياء مؤجلة:
- Supplier payments داخل صفحة الموردين
- Print Config العامة
- barcode printing settings
- whole-project Compo SearchBox rollout
- إعادة تصميم كل البرنامج بنفس الشكل الجديد مباشرة
- تغييرات backend كبيرة غير مثبتة
- whole-project animation/polish system

---

## 16) Phase Breakdown

# Phase CP-1 — Customers Page Professional Upgrade + Customer Detail Foundation
## Scope
- تحسين بصري وعملي قوي لصفحة العملاء الحالية
- تحسين القائمة
- تحسين create/edit modal
- تحسين details layout
- الحفاظ على نفس الحقول الحالية المدعومة
- إعادة تنظيم الصفحة لتصبح أوضح وأقوى
- تحسين الأزرار والحواف والحالات والـ spacing
- dark/light mode acceptance
- إبراز الصفحة كمرجع بصري محتمل

## Must Not Include
- تنفيذ Customer Payments بالكامل
- Print receipt
- تغييرات backend غير ثابتة
- shared search rollout العام

## Human Review After CP-1
راجع:
1. هل صفحة العملاء أصبحت أوضح وأجمل بصريًا؟
2. هل الوضع الليلي والنهاري صحيحان؟
3. هل create/edit customer modal أكثر احترافية؟
4. هل details صارت أوضح؟
5. هل الأزرار أكثر انسيابية وراحة؟
6. هل الصفحة تصلح فعلًا كمرجع UI لاحقًا؟

---

# Phase CP-2 — Customer Payments Core + Receipt
## Scope
- إضافة/تفعيل قسم أو شاشة دفعات العملاء
- ربط الدفع بالعميل
- سجل دفعات
- إضافة دفعة
- طباعة إيصال دفعة
- تحديث الصفحة بعد الدفع
- إظهار customer-linked financial flow بوضوح

## Must Not Include
- Supplier mirror
- Print Config
- whole-project financial refactor

## Human Review After CP-2
راجع:
1. هل يمكن إضافة دفعة جديدة للعميل؟
2. هل ترتبط فعليًا بالعميل الصحيح؟
3. هل يظهر سجل الدفعات؟
4. هل الطباعة تعمل؟
5. هل المرجع وطريقة الدفع واضحان؟
6. هل الرسائل واضحة وصادقة؟

---

# Phase CP-Future — Shared Rollout / Suppliers / Global Search / Global Visual Adoption
## Deferred
- Supplier financial linkage
- shared Compo SearchBox rollout
- تعميم visual system على باقي البرنامج
- أي dashboard/accounting enrichment موسع

---

## 17) Human Acceptance for Full Closure
تعتبر هذه العائلة مغلقة عندما أستطيع بشريًا:
1. فتح صفحة العملاء بشكل احترافي واضح
2. إضافة عميل وتعديله ومراجعته بسهولة
3. رؤية الصفحة بشكل مريح في النهاري والليلي
4. إضافة دفعة عميل بشكل سليم
5. ربط الدفعة بالعميل الصحيح
6. رؤية سجل الدفعات
7. طباعة إيصال احترافي
8. عدم وجود رسائل خاطئة أو مظهر مرهق
9. الوصول لصفحة يمكن اعتمادها بصريًا كمرجع لاحقًا

---

## 18) Cleanup Policy
في كل مرحلة تنفيذية من هذه الوثيقة:
- يتم تنظيف الملفات الملموسة فقط
- حذف dead code الآمن فقط
- لا يتم whole-project cleanup
- كل closeout يجب أن يحتوي:
  - `## Cleanup Audit`
  - what was found
  - what was removed
  - why safe
  - what was intentionally left
  - build/test verification

---

## 19) Prompting Model
بعد اعتماد هذه الوثيقة:
- يرسل للإيجينت برومبت تنفيذ **CP-1 فقط**
- بعد نجاحها بشريًا ننتقل إلى **CP-2**
- لا يتم دمج المرحلتين في Prompt واحد

---

## 20) Final Rule
أي تعارض بين هذه الوثيقة وبين backend الحقيقة:
- يتم توثيقه في التقرير
- ويتبع التنفيذ backend truth
- بدون اختراع UI أو behavior غير مدعوم