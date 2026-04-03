using System.Globalization;
using System.Resources;

namespace ElshazlyStore.Desktop.Localization;

/// <summary>
/// Strongly-typed accessor for localized strings backed by RESX resource files.
/// Uses the current UI culture (set to ar-EG at startup).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("ElshazlyStore.Desktop.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Gets a localized string by key. Falls back to the key name if not found.
    /// </summary>
    public static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // ═══ App ═══
    public static string AppName => Get(nameof(AppName));
    public static string AppTitle => Get(nameof(AppTitle));
    public static string AppSubtitle => Get(nameof(AppSubtitle));

    // ═══ Navigation Sections ═══
    public static string Section_Main => Get(nameof(Section_Main));
    public static string Section_Commerce => Get(nameof(Section_Commerce));
    public static string Section_Inventory => Get(nameof(Section_Inventory));
    public static string Section_Sales => Get(nameof(Section_Sales));
    public static string Section_Accounting => Get(nameof(Section_Accounting));
    public static string Section_Admin => Get(nameof(Section_Admin));

    // ═══ Navigation Items ═══
    public static string Nav_Home => Get(nameof(Nav_Home));
    public static string Nav_Dashboard => Get(nameof(Nav_Dashboard));
    public static string Nav_Products => Get(nameof(Nav_Products));
    public static string Nav_Customers => Get(nameof(Nav_Customers));
    public static string Nav_Suppliers => Get(nameof(Nav_Suppliers));
    public static string Nav_Warehouses => Get(nameof(Nav_Warehouses));
    public static string Nav_Stock => Get(nameof(Nav_Stock));
    public static string Nav_Purchases => Get(nameof(Nav_Purchases));
    public static string Nav_Production => Get(nameof(Nav_Production));
    public static string Nav_Sales => Get(nameof(Nav_Sales));
    public static string Nav_SalesPos => Get(nameof(Nav_SalesPos));
    public static string Nav_SalesReturns => Get(nameof(Nav_SalesReturns));
    public static string Nav_PurchaseReturns => Get(nameof(Nav_PurchaseReturns));
    public static string Nav_Balances => Get(nameof(Nav_Balances));
    public static string Nav_Payments => Get(nameof(Nav_Payments));
    public static string Nav_Users => Get(nameof(Nav_Users));
    public static string Nav_Roles => Get(nameof(Nav_Roles));
    public static string Nav_Import => Get(nameof(Nav_Import));
    public static string Nav_ReasonCodes => Get(nameof(Nav_ReasonCodes));
    public static string Nav_PrintConfig => Get(nameof(Nav_PrintConfig));
    public static string Nav_Settings => Get(nameof(Nav_Settings));

    // ═══ Login ═══
    public static string Login_Title => Get(nameof(Login_Title));
    public static string Login_Subtitle => Get(nameof(Login_Subtitle));
    public static string Login_Username => Get(nameof(Login_Username));
    public static string Login_Password => Get(nameof(Login_Password));
    public static string Login_SignIn => Get(nameof(Login_SignIn));
    public static string Login_SigningIn => Get(nameof(Login_SigningIn));
    public static string Login_PasswordRequired => Get(nameof(Login_PasswordRequired));

    // ═══ Home ═══
    public static string Home_Welcome => Get(nameof(Home_Welcome));
    public static string Home_Hint => Get(nameof(Home_Hint));

    // ═══ Settings ═══
    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string Settings_Appearance => Get(nameof(Settings_Appearance));
    public static string Settings_DarkMode => Get(nameof(Settings_DarkMode));
    public static string Settings_About => Get(nameof(Settings_About));
    public static string Settings_AboutLine1 => Get(nameof(Settings_AboutLine1));
    public static string Settings_AboutLine2 => Get(nameof(Settings_AboutLine2));

    // ═══ Common Actions ═══
    public static string Action_SignOut => Get(nameof(Action_SignOut));
    public static string Action_ThemeToggle => Get(nameof(Action_ThemeToggle));
    public static string Action_Retry => Get(nameof(Action_Retry));
    public static string Action_Save => Get(nameof(Action_Save));
    public static string Action_Cancel => Get(nameof(Action_Cancel));
    public static string Action_Delete => Get(nameof(Action_Delete));
    public static string Action_Edit => Get(nameof(Action_Edit));
    public static string Action_Create => Get(nameof(Action_Create));
    public static string Action_Search => Get(nameof(Action_Search));
    public static string Action_Confirm => Get(nameof(Action_Confirm));
    public static string Action_Close => Get(nameof(Action_Close));
    public static string Action_Yes => Get(nameof(Action_Yes));
    public static string Action_No => Get(nameof(Action_No));

    // ═══ States ═══
    public static string State_Loading => Get(nameof(State_Loading));
    public static string State_Empty => Get(nameof(State_Empty));
    public static string State_Error => Get(nameof(State_Error));
    public static string State_UnexpectedError => Get(nameof(State_UnexpectedError));
    public static string State_ConnectionError => Get(nameof(State_ConnectionError));
    public static string State_TimeoutError => Get(nameof(State_TimeoutError));

    // ═══ Paging ═══
    public static string Paging_Page => Get(nameof(Paging_Page));
    public static string Paging_Of => Get(nameof(Paging_Of));
    public static string Paging_Items => Get(nameof(Paging_Items));
    public static string Paging_First => Get(nameof(Paging_First));
    public static string Paging_Previous => Get(nameof(Paging_Previous));
    public static string Paging_Next => Get(nameof(Paging_Next));
    public static string Paging_Last => Get(nameof(Paging_Last));

    // ═══ Dialogs ═══
    public static string Dialog_ConfirmDelete => Get(nameof(Dialog_ConfirmDelete));
    public static string Dialog_ConfirmTitle => Get(nameof(Dialog_ConfirmTitle));
    public static string Dialog_ErrorTitle => Get(nameof(Dialog_ErrorTitle));
    public static string Dialog_InfoTitle => Get(nameof(Dialog_InfoTitle));

    // ═══ Status Badges ═══
    public static string Status_Draft => Get(nameof(Status_Draft));
    public static string Status_Posted => Get(nameof(Status_Posted));
    public static string Status_Voided => Get(nameof(Status_Voided));
    public static string Status_Active => Get(nameof(Status_Active));
    public static string Status_Inactive => Get(nameof(Status_Inactive));
    public static string Status_Approved => Get(nameof(Status_Approved));

    // ═══ Navigation — new ═══
    public static string Nav_Variants => Get(nameof(Nav_Variants));

    // ═══ Field Labels ═══
    public static string Field_Name => Get(nameof(Field_Name));
    public static string Field_Description => Get(nameof(Field_Description));
    public static string Field_Category => Get(nameof(Field_Category));
    public static string Field_VariantCount => Get(nameof(Field_VariantCount));
    public static string Field_Status => Get(nameof(Field_Status));
    public static string Field_Actions => Get(nameof(Field_Actions));
    public static string Field_Code => Get(nameof(Field_Code));
    public static string Field_Phone => Get(nameof(Field_Phone));
    public static string Field_Phone2 => Get(nameof(Field_Phone2));
    public static string Field_Notes => Get(nameof(Field_Notes));
    public static string Field_Color => Get(nameof(Field_Color));
    public static string Field_Size => Get(nameof(Field_Size));
    public static string Field_RetailPrice => Get(nameof(Field_RetailPrice));
    public static string Field_WholesalePrice => Get(nameof(Field_WholesalePrice));
    public static string Field_Barcode => Get(nameof(Field_Barcode));
    public static string Field_ProductName => Get(nameof(Field_ProductName));
    public static string Field_ProductId => Get(nameof(Field_ProductId));
    public static string Field_Sku => Get(nameof(Field_Sku));
    public static string Field_Address => Get(nameof(Field_Address));
    public static string Field_IsDefault => Get(nameof(Field_IsDefault));

    // ═══ Additional Actions ═══
    public static string Action_Details => Get(nameof(Action_Details));
    public static string Action_Back => Get(nameof(Action_Back));
    public static string Action_Deactivate => Get(nameof(Action_Deactivate));
    public static string Action_Reactivate => Get(nameof(Action_Reactivate));
    public static string Action_Clear => Get(nameof(Action_Clear));

    // ═══ Barcode Lookup ═══
    public static string Barcode_LookupTitle => Get(nameof(Barcode_LookupTitle));
    public static string Barcode_NotFound => Get(nameof(Barcode_NotFound));

    // ═══ Screen Titles ═══
    public static string Products_FormTitle => Get(nameof(Products_FormTitle));
    public static string Products_Variants => Get(nameof(Products_Variants));
    public static string Variants_FormTitle => Get(nameof(Variants_FormTitle));
    public static string Customers_FormTitle => Get(nameof(Customers_FormTitle));
    public static string Suppliers_FormTitle => Get(nameof(Suppliers_FormTitle));
    public static string Warehouses_FormTitle => Get(nameof(Warehouses_FormTitle));

    // ═══ Validation ═══
    public static string Validation_NameRequired => Get(nameof(Validation_NameRequired));
    public static string Validation_SkuRequired => Get(nameof(Validation_SkuRequired));
    public static string Validation_ProductRequired => Get(nameof(Validation_ProductRequired));
    public static string Validation_CodeRequired => Get(nameof(Validation_CodeRequired));

    // ═══ Additional Dialogs ═══
    public static string Dialog_ConfirmDeactivate => Get(nameof(Dialog_ConfirmDeactivate));
    public static string Dialog_ConfirmReactivate => Get(nameof(Dialog_ConfirmReactivate));

    // ═══ UI 2.2b-R1: New keys ═══
    public static string Field_IsActive => Get(nameof(Field_IsActive));
    public static string Variants_SelectProduct => Get(nameof(Variants_SelectProduct));
    public static string Variants_SearchProducts => Get(nameof(Variants_SearchProducts));
    public static string Variants_NoProductsFound => Get(nameof(Variants_NoProductsFound));
    public static string Field_CreatedAt => Get(nameof(Field_CreatedAt));

    // ═══ UI 2.2b-R2: New keys ═══
    public static string Warehouse_CreateActiveNote => Get(nameof(Warehouse_CreateActiveNote));

    // ═══ UI 2.2b-R3: New keys ═══
    public static string Action_Refresh => Get(nameof(Action_Refresh));
    public static string Toast_SaveSuccess => Get(nameof(Toast_SaveSuccess));

    // ═══ UI 2.2b-R4: New keys ═══
    public static string SearchMode_All => Get(nameof(SearchMode_All));
    public static string SearchMode_Barcode => Get(nameof(SearchMode_Barcode));
    public static string SearchMode_Sku => Get(nameof(SearchMode_Sku));
    public static string SearchMode_Name => Get(nameof(SearchMode_Name));
    public static string SearchMode_Label => Get(nameof(SearchMode_Label));

    // ═══ UI 2.2b-R5: New keys ═══
    public static string Sku_HelperText => Get(nameof(Sku_HelperText));
    public static string Barcode_HelperText => Get(nameof(Barcode_HelperText));
    public static string Variant_CreatedSuccess => Get(nameof(Variant_CreatedSuccess));
    public static string Search_NotFound => Get(nameof(Search_NotFound));

    // ═══ UI 2.2b-R6: Default Warehouse ═══
    public static string Field_DefaultWarehouse => Get(nameof(Field_DefaultWarehouse));
    public static string Field_DefaultWarehouseNotSet => Get(nameof(Field_DefaultWarehouseNotSet));
    public static string Variant_DefaultWarehouseLabel => Get(nameof(Variant_DefaultWarehouseLabel));

    // ═══ UI 2.2b-R7: Warehouse Refresh ═══
    public static string Action_RefreshWarehouses => Get(nameof(Action_RefreshWarehouses));

    // ═══ UI 2.3: Stock Screens ═══
    public static string Nav_StockBalances => Get(nameof(Nav_StockBalances));
    public static string Nav_StockLedger => Get(nameof(Nav_StockLedger));
    public static string Nav_StockMovements => Get(nameof(Nav_StockMovements));
    public static string Field_Quantity => Get(nameof(Field_Quantity));
    public static string Field_Warehouse => Get(nameof(Field_Warehouse));
    public static string Field_WarehouseCode => Get(nameof(Field_WarehouseCode));
    public static string Field_Date => Get(nameof(Field_Date));
    public static string Field_MovementType => Get(nameof(Field_MovementType));
    public static string Field_Reference => Get(nameof(Field_Reference));
    public static string Field_InQty => Get(nameof(Field_InQty));
    public static string Field_OutQty => Get(nameof(Field_OutQty));
    public static string Field_PostedBy => Get(nameof(Field_PostedBy));
    public static string Field_UnitCost => Get(nameof(Field_UnitCost));
    public static string Field_Reason => Get(nameof(Field_Reason));
    public static string Field_DateFrom => Get(nameof(Field_DateFrom));
    public static string Field_DateTo => Get(nameof(Field_DateTo));
    public static string Field_Variant => Get(nameof(Field_Variant));
    public static string Field_QuantityDelta => Get(nameof(Field_QuantityDelta));
    public static string Stock_NoDefaultWarehouse => Get(nameof(Stock_NoDefaultWarehouse));
    public static string Stock_BalancesTitle => Get(nameof(Stock_BalancesTitle));
    public static string Stock_LedgerTitle => Get(nameof(Stock_LedgerTitle));
    public static string Stock_MovementsTitle => Get(nameof(Stock_MovementsTitle));
    public static string Stock_MovementType_OpeningBalance => Get(nameof(Stock_MovementType_OpeningBalance));
    public static string Stock_MovementType_Adjustment => Get(nameof(Stock_MovementType_Adjustment));
    public static string Stock_MovementType_Transfer => Get(nameof(Stock_MovementType_Transfer));
    public static string Stock_PostSuccess => Get(nameof(Stock_PostSuccess));
    public static string Stock_SelectVariant => Get(nameof(Stock_SelectVariant));
    public static string Stock_SelectWarehouse => Get(nameof(Stock_SelectWarehouse));
    public static string Validation_VariantRequired => Get(nameof(Validation_VariantRequired));
    public static string Validation_WarehouseRequired => Get(nameof(Validation_WarehouseRequired));
    public static string Validation_QuantityRequired => Get(nameof(Validation_QuantityRequired));
    public static string Validation_LinesRequired => Get(nameof(Validation_LinesRequired));
    public static string Action_AddLine => Get(nameof(Action_AddLine));
    public static string Action_RemoveLine => Get(nameof(Action_RemoveLine));
    public static string Action_Post => Get(nameof(Action_Post));
    public static string Field_AllWarehouses => Get(nameof(Field_AllWarehouses));

    // ═══ UI 2.3-R1: Empty State & Typeahead ═══
    public static string Stock_BalancesEmpty => Get(nameof(Stock_BalancesEmpty));
    public static string Stock_OpenMovements => Get(nameof(Stock_OpenMovements));
    public static string Stock_LedgerSelectHint => Get(nameof(Stock_LedgerSelectHint));
    public static string Stock_VariantSearchHint => Get(nameof(Stock_VariantSearchHint));

    // ═══ UI 2.3-R2: Warehouse "All" + Ledger scope ═══
    public static string Stock_AllVariants => Get(nameof(Stock_AllVariants));

    // ═══ UI 2.3-R3: Warehouse Refresh + Transfer Fix + Display Format ═══
    public static string Validation_TransferSameWarehouse => Get(nameof(Validation_TransferSameWarehouse));
    public static string Stock_VariantDefaultWarehouse => Get(nameof(Stock_VariantDefaultWarehouse));
    public static string Stock_WarehouseInactiveOrMissing => Get(nameof(Stock_WarehouseInactiveOrMissing));

    // ═══ UI 2.3-R4: Transfer From/To + Dark Theme + Table Sizing ═══
    public static string Field_FromWarehouse => Get(nameof(Field_FromWarehouse));
    public static string Field_ToWarehouse => Get(nameof(Field_ToWarehouse));
    public static string Validation_TransferFromToRequired => Get(nameof(Validation_TransferFromToRequired));
    public static string Stock_LedgerHintAllFilters => Get(nameof(Stock_LedgerHintAllFilters));

    // ═══ UI 2.3-R6: Ledger columns + Transfer route ═══
    public static string Field_WarehouseName => Get(nameof(Field_WarehouseName));
    public static string Field_ProductVariantName => Get(nameof(Field_ProductVariantName));
    public static string Stock_TransferRoute => Get(nameof(Stock_TransferRoute));
    public static string Stock_TransferPostSuccess => Get(nameof(Stock_TransferPostSuccess));

    // ═══ UI 2.4: Purchases ═══
    public static string Purchase_CreateNew => Get(nameof(Purchase_CreateNew));
    public static string Purchase_FormTitle => Get(nameof(Purchase_FormTitle));
    public static string Purchase_DetailTitle => Get(nameof(Purchase_DetailTitle));
    public static string Purchase_DocNumber => Get(nameof(Purchase_DocNumber));
    public static string Purchase_Supplier => Get(nameof(Purchase_Supplier));
    public static string Purchase_Warehouse => Get(nameof(Purchase_Warehouse));
    public static string Purchase_Total => Get(nameof(Purchase_Total));
    public static string Purchase_Lines => Get(nameof(Purchase_Lines));
    public static string Purchase_LineTotal => Get(nameof(Purchase_LineTotal));
    public static string Purchase_SupplierRequired => Get(nameof(Purchase_SupplierRequired));
    public static string Purchase_Created => Get(nameof(Purchase_Created));
    public static string Purchase_PostSuccess => Get(nameof(Purchase_PostSuccess));
    public static string Purchase_ConfirmPost => Get(nameof(Purchase_ConfirmPost));
    public static string Purchase_AlreadyPosted => Get(nameof(Purchase_AlreadyPosted));
    public static string Purchase_CannotEditPosted => Get(nameof(Purchase_CannotEditPosted));
    public static string Purchase_CannotDeletePosted => Get(nameof(Purchase_CannotDeletePosted));
    public static string Purchase_SupplierSearchHint => Get(nameof(Purchase_SupplierSearchHint));

    // ═══ UI SALES 1: Sales Admin ═══
    public static string Sales_CreateNew => Get(nameof(Sales_CreateNew));
    public static string Sales_FormTitle => Get(nameof(Sales_FormTitle));
    public static string Sales_DetailTitle => Get(nameof(Sales_DetailTitle));
    public static string Sales_InvoiceNumber => Get(nameof(Sales_InvoiceNumber));
    public static string Sales_InvoiceNumberPending => Get(nameof(Sales_InvoiceNumberPending));
    public static string Sales_Customer => Get(nameof(Sales_Customer));
    public static string Sales_AnonymousCustomer => Get(nameof(Sales_AnonymousCustomer));
    public static string Sales_AnonymousAction => Get(nameof(Sales_AnonymousAction));
    public static string Sales_Cashier => Get(nameof(Sales_Cashier));
    public static string Sales_Total => Get(nameof(Sales_Total));
    public static string Sales_UnitPrice => Get(nameof(Sales_UnitPrice));
    public static string Sales_Discount => Get(nameof(Sales_Discount));
    public static string Sales_QuickAddCustomer => Get(nameof(Sales_QuickAddCustomer));
    public static string Sales_CustomerCreatedSelected => Get(nameof(Sales_CustomerCreatedSelected));
    public static string Sales_CustomerCreatedPendingInvoiceSave => Get(nameof(Sales_CustomerCreatedPendingInvoiceSave));
    public static string Sales_CustomerCreatedInvoiceSaveFailed => Get(nameof(Sales_CustomerCreatedInvoiceSaveFailed));
    public static string Sales_SaveFailed => Get(nameof(Sales_SaveFailed));
    public static string Sales_ExistingCustomerHint => Get(nameof(Sales_ExistingCustomerHint));
    public static string Sales_UseExistingCustomer => Get(nameof(Sales_UseExistingCustomer));
    public static string Sales_WarehousePolicyNote => Get(nameof(Sales_WarehousePolicyNote));
    public static string Sales_PricingHelperNote => Get(nameof(Sales_PricingHelperNote));
    public static string Sales_RetailPriceButton => Get(nameof(Sales_RetailPriceButton));
    public static string Sales_WholesalePriceButton => Get(nameof(Sales_WholesalePriceButton));
    public static string Sales_SortByDate => Get(nameof(Sales_SortByDate));
    public static string Sales_SortByNumber => Get(nameof(Sales_SortByNumber));
    public static string Sales_SortByTotal => Get(nameof(Sales_SortByTotal));
    public static string Sales_SortDirectionAscending => Get(nameof(Sales_SortDirectionAscending));
    public static string Sales_SortDirectionDescending => Get(nameof(Sales_SortDirectionDescending));
    public static string Sales_Created => Get(nameof(Sales_Created));
    public static string Sales_Updated => Get(nameof(Sales_Updated));
    public static string Sales_DeleteSuccess => Get(nameof(Sales_DeleteSuccess));
    public static string Sales_PostSuccess => Get(nameof(Sales_PostSuccess));
    public static string Sales_ConfirmPost => Get(nameof(Sales_ConfirmPost));
    public static string Sales_AlreadyPosted => Get(nameof(Sales_AlreadyPosted));
    public static string Sales_CannotEditPosted => Get(nameof(Sales_CannotEditPosted));
    public static string Sales_CannotDeletePosted => Get(nameof(Sales_CannotDeletePosted));
    public static string Sales_DiscountInvalid => Get(nameof(Sales_DiscountInvalid));
    public static string Sales_DiscountExceedsLine => Get(nameof(Sales_DiscountExceedsLine));
    public static string Sales_UnitPriceInvalid => Get(nameof(Sales_UnitPriceInvalid));
    public static string Sales_InvoiceDateEditLocked => Get(nameof(Sales_InvoiceDateEditLocked));

    // ═══ UI SALES 2: POS ═══
    public static string POS_BarcodeInputLabel => Get(nameof(POS_BarcodeInputLabel));
    public static string POS_BarcodeHelp => Get(nameof(POS_BarcodeHelp));
    public static string POS_ScanAction => Get(nameof(POS_ScanAction));
    public static string POS_BarcodeAdded => Get(nameof(POS_BarcodeAdded));
    public static string POS_BarcodeMerged => Get(nameof(POS_BarcodeMerged));
    public static string POS_BarcodeInactiveVariant => Get(nameof(POS_BarcodeInactiveVariant));
    public static string POS_BarcodePermissionMissing => Get(nameof(POS_BarcodePermissionMissing));
    public static string POS_SaleModeLabel => Get(nameof(POS_SaleModeLabel));
    public static string POS_SaleModeAnonymous => Get(nameof(POS_SaleModeAnonymous));
    public static string POS_SaleModeNamed => Get(nameof(POS_SaleModeNamed));
    public static string POS_SplitLineLabel => Get(nameof(POS_SplitLineLabel));
    public static string POS_SplitOneAction => Get(nameof(POS_SplitOneAction));
    public static string POS_SplitAllAction => Get(nameof(POS_SplitAllAction));
    public static string POS_SplitSingleDone => Get(nameof(POS_SplitSingleDone));
    public static string POS_SplitAllDone => Get(nameof(POS_SplitAllDone));
    public static string POS_SplitRequiresWholeQuantity => Get(nameof(POS_SplitRequiresWholeQuantity));
    public static string POS_ClearBasket => Get(nameof(POS_ClearBasket));
    public static string POS_BasketCleared => Get(nameof(POS_BasketCleared));
    public static string POS_CompleteSaleAction => Get(nameof(POS_CompleteSaleAction));
    public static string POS_AnonymousPaymentHint => Get(nameof(POS_AnonymousPaymentHint));
    public static string POS_PaymentPermissionMissing => Get(nameof(POS_PaymentPermissionMissing));
    public static string POS_PaymentMethodRequired => Get(nameof(POS_PaymentMethodRequired));
    public static string POS_WalletNameRequired => Get(nameof(POS_WalletNameRequired));
    public static string POS_ManualMethodLabel => Get(nameof(POS_ManualMethodLabel));
    public static string POS_ManualMethodRequired => Get(nameof(POS_ManualMethodRequired));
    public static string POS_CheckoutPosted => Get(nameof(POS_CheckoutPosted));
    public static string POS_CheckoutPostedPaymentSaved => Get(nameof(POS_CheckoutPostedPaymentSaved));
    public static string POS_CheckoutPostedAnonymousNoPayment => Get(nameof(POS_CheckoutPostedAnonymousNoPayment));
    public static string POS_CheckoutPostedPaymentFailed => Get(nameof(POS_CheckoutPostedPaymentFailed));
    public static string POS_PrintFetchFailed => Get(nameof(POS_PrintFetchFailed));
    public static string POS_TenderedAmountLabel => Get(nameof(POS_TenderedAmountLabel));
    public static string POS_TenderPendingLabel => Get(nameof(POS_TenderPendingLabel));
    public static string POS_ChangeDueLabel => Get(nameof(POS_ChangeDueLabel));
    public static string POS_RemainingAmountLabel => Get(nameof(POS_RemainingAmountLabel));
    public static string POS_ExactTenderLabel => Get(nameof(POS_ExactTenderLabel));
    public static string POS_CheckoutShortcutHint => Get(nameof(POS_CheckoutShortcutHint));
    public static string POS_PersistenceHintAnonymous => Get(nameof(POS_PersistenceHintAnonymous));
    public static string POS_PersistenceHintNamedExact => Get(nameof(POS_PersistenceHintNamedExact));
    public static string POS_PersistenceHintNamedPartial => Get(nameof(POS_PersistenceHintNamedPartial));
    public static string POS_PersistenceHintNamedCashChange => Get(nameof(POS_PersistenceHintNamedCashChange));
    public static string POS_TenderedAmountInvalid => Get(nameof(POS_TenderedAmountInvalid));
    public static string POS_AnonymousRemainingNotAllowed => Get(nameof(POS_AnonymousRemainingNotAllowed));
    public static string POS_CheckoutPostedAnonymousChangeDue => Get(nameof(POS_CheckoutPostedAnonymousChangeDue));
    public static string POS_CheckoutPostedPaymentPartial => Get(nameof(POS_CheckoutPostedPaymentPartial));
    public static string POS_CheckoutPostedPaymentSavedWithChange => Get(nameof(POS_CheckoutPostedPaymentSavedWithChange));
    public static string POS_ReferenceOptional => Get(nameof(POS_ReferenceOptional));
    public static string POS_NotesOptional => Get(nameof(POS_NotesOptional));
    public static string POS_PaymentStateLabel => Get(nameof(POS_PaymentStateLabel));
    public static string POS_CashModeSelectorLabel => Get(nameof(POS_CashModeSelectorLabel));
    public static string POS_FullCashModeOption => Get(nameof(POS_FullCashModeOption));
    public static string POS_PartialCreditModeOption => Get(nameof(POS_PartialCreditModeOption));
    public static string POS_PaidNowAmountLabel => Get(nameof(POS_PaidNowAmountLabel));
    public static string POS_PaidNowPendingLabel => Get(nameof(POS_PaidNowPendingLabel));
    public static string POS_PersistenceHintNamedNonCash => Get(nameof(POS_PersistenceHintNamedNonCash));
    public static string POS_CreditModePermissionHint => Get(nameof(POS_CreditModePermissionHint));
    public static string POS_CreditModePermissionMissing => Get(nameof(POS_CreditModePermissionMissing));
    public static string POS_PaidAmountRequired => Get(nameof(POS_PaidAmountRequired));
    public static string POS_PartialModeRequiresRemaining => Get(nameof(POS_PartialModeRequiresRemaining));
    public static string POS_SwitchToPartialForRemaining => Get(nameof(POS_SwitchToPartialForRemaining));
    public static string POS_FullCashRequiresCompleteTender => Get(nameof(POS_FullCashRequiresCompleteTender));
    public static string POS_ModeAnonymousTitle => Get(nameof(POS_ModeAnonymousTitle));
    public static string POS_ModeAnonymousDescription => Get(nameof(POS_ModeAnonymousDescription));
    public static string POS_ModeNamedNonCashTitle => Get(nameof(POS_ModeNamedNonCashTitle));
    public static string POS_ModeNamedNonCashDescription => Get(nameof(POS_ModeNamedNonCashDescription));
    public static string POS_ModePartialCreditTitle => Get(nameof(POS_ModePartialCreditTitle));
    public static string POS_ModePartialCreditDescription => Get(nameof(POS_ModePartialCreditDescription));
    public static string POS_ModeFullCashTitle => Get(nameof(POS_ModeFullCashTitle));
    public static string POS_ModeFullCashDescription => Get(nameof(POS_ModeFullCashDescription));
    public static string POS_PrintAnonymousOperationalNote => Get(nameof(POS_PrintAnonymousOperationalNote));

    // ═══ UI 2.4: Purchase Returns ═══
    public static string PurchaseReturn_CreateNew => Get(nameof(PurchaseReturn_CreateNew));
    public static string PurchaseReturn_FormTitle => Get(nameof(PurchaseReturn_FormTitle));
    public static string PurchaseReturn_DetailTitle => Get(nameof(PurchaseReturn_DetailTitle));
    public static string PurchaseReturn_Reason => Get(nameof(PurchaseReturn_Reason));
    public static string PurchaseReturn_ReasonRequired => Get(nameof(PurchaseReturn_ReasonRequired));
    public static string PurchaseReturn_Created => Get(nameof(PurchaseReturn_Created));
    public static string PurchaseReturn_PostSuccess => Get(nameof(PurchaseReturn_PostSuccess));
    public static string PurchaseReturn_VoidSuccess => Get(nameof(PurchaseReturn_VoidSuccess));
    public static string PurchaseReturn_ConfirmPost => Get(nameof(PurchaseReturn_ConfirmPost));
    public static string PurchaseReturn_ConfirmVoid => Get(nameof(PurchaseReturn_ConfirmVoid));
    public static string PurchaseReturn_AlreadyPosted => Get(nameof(PurchaseReturn_AlreadyPosted));
    public static string PurchaseReturn_CannotEditPosted => Get(nameof(PurchaseReturn_CannotEditPosted));
    public static string PurchaseReturn_CannotDeletePosted => Get(nameof(PurchaseReturn_CannotDeletePosted));
    public static string PurchaseReturn_Void => Get(nameof(PurchaseReturn_Void));

    // ═══ UI 2.4: Supplier Purchases Link ═══
    public static string Supplier_ViewPurchases => Get(nameof(Supplier_ViewPurchases));

    // ═══ UI 2.4-R1: Variant Picker Fix ═══
    public static string Variant_SearchMinChars => Get(nameof(Variant_SearchMinChars));
    public static string Variant_NoResults => Get(nameof(Variant_NoResults));

    // ═══ UI 2.4-R2: Error Banner + Warehouse Guard ═══
    public static string Action_CopyError => Get(nameof(Action_CopyError));
    public static string Validation_WarehouseInactive => Get(nameof(Validation_WarehouseInactive));

    // ═══ UI 2.4-R4a: Reason Code Loading + Empty State + Inline Add ═══
    public static string ReasonCode_EmptyState => Get(nameof(ReasonCode_EmptyState));
    public static string ReasonCode_OpenPage => Get(nameof(ReasonCode_OpenPage));
    public static string ReasonCode_AddNew => Get(nameof(ReasonCode_AddNew));
    public static string ReasonCode_CodeRequired => Get(nameof(ReasonCode_CodeRequired));
    public static string ReasonCode_NameRequired => Get(nameof(ReasonCode_NameRequired));
    public static string ReasonCode_Code => Get(nameof(ReasonCode_Code));
    public static string ReasonCode_NameAr => Get(nameof(ReasonCode_NameAr));
    public static string ReasonCode_Notes => Get(nameof(ReasonCode_Notes));

    // ═══ UI 2.3-R4: Stock Change Refresh ═══
    public static string Action_RefreshQuantities => Get(nameof(Action_RefreshQuantities));
    public static string Stock_QuantityOtherWarehouse => Get(nameof(Stock_QuantityOtherWarehouse));

    // ═══ UI 2.3-R6: Variants Warehouse Filter + Balance Details ═══
    public static string Stock_ViewTotal => Get(nameof(Stock_ViewTotal));
    public static string Stock_ViewWarehouse => Get(nameof(Stock_ViewWarehouse));
    public static string Stock_BalanceDetailsTitle => Get(nameof(Stock_BalanceDetailsTitle));
    public static string Stock_OpenStockLedger => Get(nameof(Stock_OpenStockLedger));

    // ═══ UI 2.4-R8: Supplier Payments ═══
    public static string Nav_SupplierPayments => Get(nameof(Nav_SupplierPayments));
    public static string Payment_CreateNew => Get(nameof(Payment_CreateNew));
    public static string Payment_FormTitle => Get(nameof(Payment_FormTitle));
    public static string Payment_Number => Get(nameof(Payment_Number));
    public static string Payment_Amount => Get(nameof(Payment_Amount));
    public static string Payment_Method => Get(nameof(Payment_Method));
    public static string Payment_WalletName => Get(nameof(Payment_WalletName));
    public static string Payment_Reference => Get(nameof(Payment_Reference));
    public static string Payment_AmountRequired => Get(nameof(Payment_AmountRequired));
    public static string Payment_Created => Get(nameof(Payment_Created));
    public static string Payment_PrintReceipt => Get(nameof(Payment_PrintReceipt));
    public static string Supplier_ViewPayments => Get(nameof(Supplier_ViewPayments));

    // ═══ UI 2.4-R8: Print ═══
    public static string Action_Print => Get(nameof(Action_Print));

    // ═══ UI 2.4-R8: Line Type Column ═══
    public static string Field_LineType => Get(nameof(Field_LineType));
    public static string LineType_Stock => Get(nameof(LineType_Stock));

    // ═══ UI SR-1: Sales Returns ═══
    public static string SalesReturn_CreateNew => Get(nameof(SalesReturn_CreateNew));
    public static string SalesReturn_FormTitle => Get(nameof(SalesReturn_FormTitle));
    public static string SalesReturn_DetailTitle => Get(nameof(SalesReturn_DetailTitle));
    public static string SalesReturn_OriginalInvoice => Get(nameof(SalesReturn_OriginalInvoice));
    public static string SalesReturn_SelectInvoice => Get(nameof(SalesReturn_SelectInvoice));
    public static string SalesReturn_InvoiceSearchHint => Get(nameof(SalesReturn_InvoiceSearchHint));
    public static string SalesReturn_NoInvoiceFound => Get(nameof(SalesReturn_NoInvoiceFound));
    public static string SalesReturn_InvoiceRequired => Get(nameof(SalesReturn_InvoiceRequired));
    public static string SalesReturn_InvoiceTooOld => Get(nameof(SalesReturn_InvoiceTooOld));
    public static string SalesReturn_QuantityExceedsAvailable => Get(nameof(SalesReturn_QuantityExceedsAvailable));
    public static string SalesReturn_DispositionNotAllowed => Get(nameof(SalesReturn_DispositionNotAllowed));
    public static string SalesReturn_DispositionReturnToStock => Get(nameof(SalesReturn_DispositionReturnToStock));
    public static string SalesReturn_DispositionQuarantine => Get(nameof(SalesReturn_DispositionQuarantine));
    public static string SalesReturn_Created => Get(nameof(SalesReturn_Created));
    public static string SalesReturn_Updated => Get(nameof(SalesReturn_Updated));
    public static string SalesReturn_DeleteSuccess => Get(nameof(SalesReturn_DeleteSuccess));
    public static string SalesReturn_CannotEditPosted => Get(nameof(SalesReturn_CannotEditPosted));
    public static string SalesReturn_CannotDeletePosted => Get(nameof(SalesReturn_CannotDeletePosted));
    public static string SalesReturn_LinesGridHint => Get(nameof(SalesReturn_LinesGridHint));
    public static string SalesReturn_SoldQty => Get(nameof(SalesReturn_SoldQty));
    public static string SalesReturn_AvailableQty => Get(nameof(SalesReturn_AvailableQty));
    public static string SalesReturn_ReturnQty => Get(nameof(SalesReturn_ReturnQty));
    public static string SalesReturn_Disposition => Get(nameof(SalesReturn_Disposition));
    public static string SalesReturn_ReturnNumber => Get(nameof(SalesReturn_ReturnNumber));
    // SR-2: Post + Print
    public static string SalesReturn_ConfirmPostTitle => Get(nameof(SalesReturn_ConfirmPostTitle));
    public static string SalesReturn_ConfirmPostHeader => Get(nameof(SalesReturn_ConfirmPostHeader));
    public static string SalesReturn_PostSuccess => Get(nameof(SalesReturn_PostSuccess));
    public static string SalesReturn_CannotPostNotDraft => Get(nameof(SalesReturn_CannotPostNotDraft));

    // ═══ CP-1: Customers Page Upgrade ═══
    public static string Customers_FormTitleCreate => Get(nameof(Customers_FormTitleCreate));
    public static string Customers_FormTitleEdit => Get(nameof(Customers_FormTitleEdit));
    public static string Customers_DetailTitle => Get(nameof(Customers_DetailTitle));
    public static string Customers_BasicInfo => Get(nameof(Customers_BasicInfo));
    public static string Customers_ContactInfo => Get(nameof(Customers_ContactInfo));
    public static string Customers_SaveSuccess => Get(nameof(Customers_SaveSuccess));
    public static string Customers_DeactivateSuccess => Get(nameof(Customers_DeactivateSuccess));
    public static string Customers_ReactivateSuccess => Get(nameof(Customers_ReactivateSuccess));

    // ═══ CP-2: Customer Payments Core + Receipt ═══
    public static string Nav_CustomerPayments => Get(nameof(Nav_CustomerPayments));
    public static string Customer_ViewPayments => Get(nameof(Customer_ViewPayments));
    public static string CustomerPayment_Created => Get(nameof(CustomerPayment_Created));
    public static string CustomerPayment_CustomerRequired => Get(nameof(CustomerPayment_CustomerRequired));
    public static string CustomerPayments_ContextSubtitle => Get(nameof(CustomerPayments_ContextSubtitle));
    public static string CustomerPayments_AllSubtitle => Get(nameof(CustomerPayments_AllSubtitle));
    public static string CustomerPayments_BackToCustomers => Get(nameof(CustomerPayments_BackToCustomers));
    public static string CustomerPayments_CreateFormTitle => Get(nameof(CustomerPayments_CreateFormTitle));
    public static string CustomerPayments_Party => Get(nameof(CustomerPayments_Party));

    // ═══ CP-2-R1: Customer Context Search + Outstanding Visibility + Header Clarity ═══
    public static string CustomerPayments_SelectCustomer => Get(nameof(CustomerPayments_SelectCustomer));
    public static string CustomerPayments_ChangeCustomer => Get(nameof(CustomerPayments_ChangeCustomer));
    public static string CustomerPayments_ClearFilter => Get(nameof(CustomerPayments_ClearFilter));
    public static string CustomerPayments_AllCustomers => Get(nameof(CustomerPayments_AllCustomers));
    public static string CustomerPayments_OutstandingLabel => Get(nameof(CustomerPayments_OutstandingLabel));
    public static string CustomerPayments_OutstandingLoading => Get(nameof(CustomerPayments_OutstandingLoading));
    public static string CustomerPayments_SearchCustomer => Get(nameof(CustomerPayments_SearchCustomer));

    // ═══ CP-3A: Customer Credit Profile + Attachments ═══
    public static string Field_WhatsApp => Get(nameof(Field_WhatsApp));
    public static string Field_WalletNumber => Get(nameof(Field_WalletNumber));
    public static string Field_InstaPayId => Get(nameof(Field_InstaPayId));
    public static string Field_CommercialName => Get(nameof(Field_CommercialName));
    public static string Field_CommercialAddress => Get(nameof(Field_CommercialAddress));
    public static string Field_NationalIdNumber => Get(nameof(Field_NationalIdNumber));
    public static string Customers_DigitalPayment => Get(nameof(Customers_DigitalPayment));
    public static string Customers_CommercialInfo => Get(nameof(Customers_CommercialInfo));
    public static string Customers_Attachments => Get(nameof(Customers_Attachments));
    public static string Customers_UploadAttachment => Get(nameof(Customers_UploadAttachment));
    public static string Customers_DownloadAttachment => Get(nameof(Customers_DownloadAttachment));
    public static string Customers_OpenAttachment => Get(nameof(Customers_OpenAttachment));
    public static string Customers_AttachmentUploaded => Get(nameof(Customers_AttachmentUploaded));
    public static string Customers_AttachmentDownloaded => Get(nameof(Customers_AttachmentDownloaded));
    public static string Customers_AttachmentDeleted => Get(nameof(Customers_AttachmentDeleted));
    public static string Customers_AttachmentTooLarge => Get(nameof(Customers_AttachmentTooLarge));
    public static string Customers_ConfirmDeleteAttachment => Get(nameof(Customers_ConfirmDeleteAttachment));
    public static string Customers_UploadNationalId => Get(nameof(Customers_UploadNationalId));
    public static string Customers_UploadContract => Get(nameof(Customers_UploadContract));
    public static string Customers_UploadOther => Get(nameof(Customers_UploadOther));
    public static string Customers_NoAttachments => Get(nameof(Customers_NoAttachments));

    // ═══ CAFS-1-R1: Customer Code + Open Folder ═══
    public static string Field_CustomerCode => Get(nameof(Field_CustomerCode));
    public static string Customers_OpenFolder => Get(nameof(Customers_OpenFolder));
    public static string Customers_OpenFolderTooltip => Get(nameof(Customers_OpenFolderTooltip));
    public static string Customers_FolderNotAccessible => Get(nameof(Customers_FolderNotAccessible));
    public static string Customers_FolderNotCreatedYet => Get(nameof(Customers_FolderNotCreatedYet));
}
