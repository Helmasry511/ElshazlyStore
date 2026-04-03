namespace ElshazlyStore.Desktop.Models;

/// <summary>
/// Maps backend ErrorCodes (from ProblemDetails.Title) to Arabic user-friendly messages.
/// All 77 error codes from the backend are mapped.
/// </summary>
public static class ErrorCodeMapper
{
    private static readonly Dictionary<string, string> ArabicMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── General ──
        ["INTERNAL_ERROR"] = "حدث خطأ غير متوقع في الخادم",
        ["VALIDATION_FAILED"] = "بيانات غير صالحة",
        ["NOT_FOUND"] = "العنصر غير موجود",
        ["CONFLICT"] = "تعذر إتمام العملية بسبب تعارض في البيانات",
        ["UNAUTHORIZED"] = "غير مصرح — يرجى تسجيل الدخول",
        ["FORBIDDEN"] = "ليس لديك صلاحية لهذا الإجراء",

        // ── Auth ──
        ["INVALID_CREDENTIALS"] = "اسم المستخدم أو كلمة المرور غير صحيحة",
        ["ACCOUNT_INACTIVE"] = "الحساب معطل",
        ["TOKEN_EXPIRED"] = "انتهت صلاحية الجلسة",
        ["TOKEN_INVALID"] = "رمز الجلسة غير صالح",

        // ── Barcodes ──
        ["BARCODE_ALREADY_EXISTS"] = "الباركود مستخدم بالفعل",
        ["BARCODE_RETIRED"] = "الباركود تم إيقافه نهائياً",

        // ── Import ──
        ["IMPORT_PREVIEW_FAILED"] = "فشل في معاينة الملف",
        ["IMPORT_COMMIT_FAILED"] = "فشل في تنفيذ الاستيراد",
        ["IMPORT_JOB_NOT_FOUND"] = "عملية الاستيراد غير موجودة",
        ["IMPORT_JOB_ALREADY_COMMITTED"] = "تم تنفيذ هذا الاستيراد مسبقاً",

        // ── Stock ──
        ["STOCK_NEGATIVE_NOT_ALLOWED"] = "الرصيد غير كافٍ — لا يُسمح برصيد سالب",
        ["MOVEMENT_EMPTY"] = "الحركة لا تحتوي على أصناف",
        ["WAREHOUSE_NOT_FOUND"] = "المخزن غير موجود أو غير نشط. افحص حالة المخزن من شاشة المخازن.",
        ["WAREHOUSE_INACTIVE"] = "المخزن غير نشط. افحص حالة المخزن من شاشة المخازن.",
        ["VARIANT_NOT_FOUND"] = "الصنف غير موجود",
        ["TRANSFER_UNBALANCED"] = "عملية التحويل غير متوازنة",

        // ── Purchases ──
        ["PURCHASE_RECEIPT_NOT_FOUND"] = "إذن الشراء غير موجود",
        ["PURCHASE_RECEIPT_ALREADY_POSTED"] = "لا يمكن تعديل إذن شراء مُرحَّل",
        ["PURCHASE_RECEIPT_EMPTY"] = "إذن الشراء لا يحتوي على أصناف",
        ["SUPPLIER_NOT_FOUND"] = "المورد غير موجود",
        ["DOCUMENT_NUMBER_EXISTS"] = "رقم المستند مستخدم بالفعل",

        // ── Production ──
        ["PRODUCTION_BATCH_NOT_FOUND"] = "أمر الإنتاج غير موجود",
        ["PRODUCTION_BATCH_ALREADY_POSTED"] = "لا يمكن تعديل أمر إنتاج مُرحَّل",
        ["PRODUCTION_BATCH_NO_INPUTS"] = "أمر الإنتاج لا يحتوي على مدخلات",
        ["PRODUCTION_BATCH_NO_OUTPUTS"] = "أمر الإنتاج لا يحتوي على مخرجات",
        ["BATCH_NUMBER_EXISTS"] = "رقم الدفعة مستخدم بالفعل",

        // ── Sales ──
        ["SALES_INVOICE_NOT_FOUND"] = "فاتورة البيع غير موجودة",
        ["SALES_INVOICE_ALREADY_POSTED"] = "لا يمكن تعديل فاتورة مُرحَّلة",
        ["SALES_INVOICE_EMPTY"] = "الفاتورة لا تحتوي على أصناف",
        ["INVOICE_NUMBER_EXISTS"] = "رقم الفاتورة مستخدم بالفعل",
        ["CUSTOMER_NOT_FOUND"] = "العميل غير موجود",

        // ── Posting ──
        ["POST_ALREADY_POSTED"] = "تم الترحيل مسبقاً",
        ["POST_CONCURRENCY_CONFLICT"] = "يتم الترحيل حالياً — أعد المحاولة",

        // ── Payments ──
        ["PAYMENT_NOT_FOUND"] = "الدفعة غير موجودة",
        ["OVERPAYMENT_NOT_ALLOWED"] = "المبلغ يتجاوز الرصيد المستحق",
        ["WALLET_NAME_REQUIRED"] = "اسم المحفظة مطلوب",
        ["INVALID_PAYMENT_METHOD"] = "طريقة دفع غير صالحة",
        ["INVALID_PARTY_TYPE"] = "نوع الطرف غير صالح",
        ["PARTY_NOT_FOUND"] = "الطرف غير موجود",

        // ── Print ──
        ["PRINT_PROFILE_NOT_FOUND"] = "ملف الطباعة غير موجود",
        ["PRINT_RULE_NOT_FOUND"] = "قاعدة الطباعة غير موجودة",
        ["PRINT_PROFILE_NAME_EXISTS"] = "اسم ملف الطباعة مستخدم بالفعل",
        ["PRINT_RULE_SCREEN_EXISTS"] = "يوجد قاعدة لهذه الشاشة بالفعل",

        // ── Reason Codes ──
        ["REASON_CODE_NOT_FOUND"] = "كود السبب غير موجود",
        ["REASON_CODE_ALREADY_EXISTS"] = "كود السبب مستخدم بالفعل",
        ["REASON_CODE_IN_USE"] = "لا يمكن حذف كود سبب مستخدم",
        ["REASON_CODE_INACTIVE"] = "كود السبب غير نشط",

        // ── Sales Returns ──
        ["SALES_RETURN_NOT_FOUND"] = "مرتجع البيع غير موجود",
        ["SALES_RETURN_ALREADY_POSTED"] = "لا يمكن تعديل مرتجع مُرحَّل",
        ["SALES_RETURN_EMPTY"] = "المرتجع لا يحتوي على أصناف",
        ["RETURN_NUMBER_EXISTS"] = "رقم المرتجع مستخدم بالفعل",
        ["RETURN_QTY_EXCEEDS_SOLD"] = "كمية الإرجاع تتجاوز الكمية المباعة",
        ["SALES_RETURN_ALREADY_VOIDED"] = "المرتجع ملغي بالفعل",
        ["SALES_RETURN_NOT_POSTED"] = "لم يتم ترحيل المرتجع بعد",
        ["SALES_RETURN_DISPOSITION_NOT_ALLOWED"] = "نوع التصرف غير مسموح (استخدم التصرفات المخزنية)",
        ["SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST"] = "لا يمكن إلغاء مرتجع مُرحَّل",

        // ── Purchase Returns ──
        ["PURCHASE_RETURN_NOT_FOUND"] = "مرتجع الشراء غير موجود",
        ["PURCHASE_RETURN_ALREADY_POSTED"] = "لا يمكن تعديل مرتجع شراء مُرحَّل",
        ["PURCHASE_RETURN_EMPTY"] = "مرتجع الشراء لا يحتوي على أصناف",
        ["PURCHASE_RETURN_ALREADY_VOIDED"] = "مرتجع الشراء ملغي بالفعل",
        ["PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST"] = "لا يمكن إلغاء مرتجع شراء مُرحَّل",
        ["RETURN_QTY_EXCEEDS_RECEIVED"] = "كمية الإرجاع تتجاوز الكمية المستلمة",
        ["PURCHASE_RETURN_NUMBER_EXISTS"] = "رقم مرتجع الشراء مستخدم بالفعل",

        // ── Dispositions ──
        ["DISPOSITION_NOT_FOUND"] = "التصرف المخزني غير موجود",
        ["DISPOSITION_ALREADY_POSTED"] = "لا يمكن تعديل تصرف مُرحَّل",
        ["DISPOSITION_EMPTY"] = "التصرف لا يحتوي على أصناف",
        ["DISPOSITION_ALREADY_VOIDED"] = "التصرف ملغي بالفعل",
        ["DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST"] = "لا يمكن إلغاء تصرف مُرحَّل",
        ["DISPOSITION_NUMBER_EXISTS"] = "رقم التصرف مستخدم بالفعل",
        ["DISPOSITION_REQUIRES_APPROVAL"] = "يتطلب موافقة المدير قبل الترحيل",
        ["DISPOSITION_INVALID_TYPE"] = "نوع التصرف غير مسموح",
        ["DESTINATION_WAREHOUSE_NOT_FOUND"] = "مخزن الوجهة غير موجود",
    };

    /// <summary>
    /// Gets an Arabic user-friendly message for the given error code.
    /// Falls back to "code: detail" format if unmapped.
    /// </summary>
    public static string ToArabicMessage(string? errorCode, string? detail = null)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && ArabicMessages.TryGetValue(errorCode, out var arabicMsg))
            return arabicMsg;

        return Localization.Strings.State_UnexpectedError;
    }
}
