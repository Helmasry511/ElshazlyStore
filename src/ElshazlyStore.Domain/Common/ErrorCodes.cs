namespace ElshazlyStore.Domain.Common;

/// <summary>
/// Stable error codes returned in ProblemDetails responses.
/// Every server error gets a machine-readable code for the desktop client.
/// </summary>
public static class ErrorCodes
{
    public const string InternalError = "INTERNAL_ERROR";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";

    // Auth-specific
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenInvalid = "TOKEN_INVALID";

    // Barcode
    public const string BarcodeAlreadyExists = "BARCODE_ALREADY_EXISTS";
    public const string BarcodeRetired = "BARCODE_RETIRED";
    public const string BarcodeImmutable = "BARCODE_IMMUTABLE";

    // Import
    public const string ImportPreviewFailed = "IMPORT_PREVIEW_FAILED";
    public const string ImportCommitFailed = "IMPORT_COMMIT_FAILED";
    public const string ImportJobNotFound = "IMPORT_JOB_NOT_FOUND";
    public const string ImportJobAlreadyCommitted = "IMPORT_JOB_ALREADY_COMMITTED";

    // Stock / Inventory
    public const string StockNegativeNotAllowed = "STOCK_NEGATIVE_NOT_ALLOWED";
    public const string MovementEmpty = "MOVEMENT_EMPTY";
    public const string WarehouseNotFound = "WAREHOUSE_NOT_FOUND";
    public const string WarehouseInactive = "WAREHOUSE_INACTIVE";
    public const string VariantNotFound = "VARIANT_NOT_FOUND";
    public const string TransferUnbalanced = "TRANSFER_UNBALANCED";

    // Procurement
    public const string PurchaseReceiptNotFound = "PURCHASE_RECEIPT_NOT_FOUND";
    public const string PurchaseReceiptAlreadyPosted = "PURCHASE_RECEIPT_ALREADY_POSTED";
    public const string PurchaseReceiptEmpty = "PURCHASE_RECEIPT_EMPTY";
    public const string SupplierNotFound = "SUPPLIER_NOT_FOUND";
    public const string DocumentNumberExists = "DOCUMENT_NUMBER_EXISTS";

    // Production
    public const string ProductionBatchNotFound = "PRODUCTION_BATCH_NOT_FOUND";
    public const string ProductionBatchAlreadyPosted = "PRODUCTION_BATCH_ALREADY_POSTED";
    public const string ProductionBatchNoInputs = "PRODUCTION_BATCH_NO_INPUTS";
    public const string ProductionBatchNoOutputs = "PRODUCTION_BATCH_NO_OUTPUTS";
    public const string BatchNumberExists = "BATCH_NUMBER_EXISTS";

    // Sales / POS
    public const string SalesInvoiceNotFound = "SALES_INVOICE_NOT_FOUND";
    public const string SalesInvoiceAlreadyPosted = "SALES_INVOICE_ALREADY_POSTED";
    public const string SalesInvoiceEmpty = "SALES_INVOICE_EMPTY";
    public const string InvoiceNumberExists = "INVOICE_NUMBER_EXISTS";
    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";

    // Posting concurrency
    public const string PostAlreadyPosted = "POST_ALREADY_POSTED";
    public const string PostConcurrencyConflict = "POST_CONCURRENCY_CONFLICT";

    // Accounting / Payments
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string OverpaymentNotAllowed = "OVERPAYMENT_NOT_ALLOWED";
    public const string WalletNameRequired = "WALLET_NAME_REQUIRED";
    public const string InvalidPaymentMethod = "INVALID_PAYMENT_METHOD";
    public const string InvalidPartyType = "INVALID_PARTY_TYPE";
    public const string PartyNotFound = "PARTY_NOT_FOUND";

    // Printing Policy
    public const string PrintProfileNotFound = "PRINT_PROFILE_NOT_FOUND";
    public const string PrintRuleNotFound = "PRINT_RULE_NOT_FOUND";
    public const string PrintProfileNameExists = "PRINT_PROFILE_NAME_EXISTS";
    public const string PrintRuleScreenExists = "PRINT_RULE_SCREEN_EXISTS";

    // Reason Codes
    public const string ReasonCodeNotFound = "REASON_CODE_NOT_FOUND";
    public const string ReasonCodeAlreadyExists = "REASON_CODE_ALREADY_EXISTS";
    public const string ReasonCodeInUse = "REASON_CODE_IN_USE";

    // Sales Returns
    public const string SalesReturnNotFound = "SALES_RETURN_NOT_FOUND";
    public const string SalesReturnAlreadyPosted = "SALES_RETURN_ALREADY_POSTED";
    public const string SalesReturnEmpty = "SALES_RETURN_EMPTY";
    public const string ReturnNumberExists = "RETURN_NUMBER_EXISTS";
    public const string ReturnQtyExceedsSold = "RETURN_QTY_EXCEEDS_SOLD";
    public const string ReasonCodeInactive = "REASON_CODE_INACTIVE";
    public const string SalesReturnAlreadyVoided = "SALES_RETURN_ALREADY_VOIDED";
    public const string SalesReturnNotPosted = "SALES_RETURN_NOT_POSTED";
    public const string SalesReturnDispositionNotAllowed = "SALES_RETURN_DISPOSITION_NOT_ALLOWED";
    public const string SalesReturnVoidNotAllowedAfterPost = "SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST";

    // Purchase Returns
    public const string PurchaseReturnNotFound = "PURCHASE_RETURN_NOT_FOUND";
    public const string PurchaseReturnAlreadyPosted = "PURCHASE_RETURN_ALREADY_POSTED";
    public const string PurchaseReturnEmpty = "PURCHASE_RETURN_EMPTY";
    public const string PurchaseReturnAlreadyVoided = "PURCHASE_RETURN_ALREADY_VOIDED";
    public const string PurchaseReturnVoidNotAllowedAfterPost = "PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST";
    public const string ReturnQtyExceedsReceived = "RETURN_QTY_EXCEEDS_RECEIVED";
    public const string PurchaseReturnNumberExists = "PURCHASE_RETURN_NUMBER_EXISTS";

    // Inventory Dispositions
    public const string DispositionNotFound = "DISPOSITION_NOT_FOUND";
    public const string DispositionAlreadyPosted = "DISPOSITION_ALREADY_POSTED";
    public const string DispositionEmpty = "DISPOSITION_EMPTY";
    public const string DispositionAlreadyVoided = "DISPOSITION_ALREADY_VOIDED";
    public const string DispositionVoidNotAllowedAfterPost = "DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST";
    public const string DispositionNumberExists = "DISPOSITION_NUMBER_EXISTS";
    public const string DispositionRequiresApproval = "DISPOSITION_REQUIRES_APPROVAL";
    public const string DispositionInvalidType = "DISPOSITION_INVALID_TYPE";
    public const string DestinationWarehouseNotFound = "DESTINATION_WAREHOUSE_NOT_FOUND";
}
