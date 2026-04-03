namespace ElshazlyStore.Domain.Common;

/// <summary>
/// All permission code constants used for policy-based authorization.
/// Every protected endpoint requires one of these codes.
/// </summary>
public static class Permissions
{
    public const string UsersRead = "USERS_READ";
    public const string UsersWrite = "USERS_WRITE";
    public const string RolesRead = "ROLES_READ";
    public const string RolesWrite = "ROLES_WRITE";
    public const string AuditRead = "AUDIT_READ";

    // Phase 2 — Master Data
    public const string ProductsRead = "PRODUCTS_READ";
    public const string ProductsWrite = "PRODUCTS_WRITE";
    public const string CustomersRead = "CUSTOMERS_READ";
    public const string CustomersWrite = "CUSTOMERS_WRITE";
    public const string SuppliersRead = "SUPPLIERS_READ";
    public const string SuppliersWrite = "SUPPLIERS_WRITE";
    public const string ImportMasterData = "IMPORT_MASTER_DATA";

    // Phase 3 — Inventory
    public const string StockRead = "STOCK_READ";
    public const string StockPost = "STOCK_POST";
    public const string WarehousesRead = "WAREHOUSES_READ";
    public const string WarehousesWrite = "WAREHOUSES_WRITE";

    // Phase 4 — Procurement
    public const string PurchasesRead = "PURCHASES_READ";
    public const string PurchasesWrite = "PURCHASES_WRITE";
    public const string PurchasesPost = "PURCHASES_POST";

    // Phase 5 — Production
    public const string ProductionRead = "PRODUCTION_READ";
    public const string ProductionWrite = "PRODUCTION_WRITE";
    public const string ProductionPost = "PRODUCTION_POST";

    // Phase 6 — POS / Sales
    public const string SalesRead = "SALES_READ";
    public const string SalesWrite = "SALES_WRITE";
    public const string SalesPost = "SALES_POST";

    // Phase 7 — AR/AP Accounting & Payments
    public const string AccountingRead = "ACCOUNTING_READ";

    // Phase 8 — Dashboard
    public const string DashboardRead = "DASHBOARD_READ";

    // Phase 9 — Printing Policy
    public const string ManagePrintingPolicy = "MANAGE_PRINTING_POLICY";
    public const string PaymentsRead = "PAYMENTS_READ";
    public const string PaymentsWrite = "PAYMENTS_WRITE";
    public const string ImportOpeningBalances = "IMPORT_OPENING_BALANCES";
    public const string ImportPayments = "IMPORT_PAYMENTS";

    // Phase RET 0 — Reason Codes
    public const string ManageReasonCodes = "MANAGE_REASON_CODES";
    public const string ViewReasonCodes = "VIEW_REASON_CODES";

    // Phase RET 1 — Sales Returns
    public const string SalesReturnCreate = "SALES_RETURN_CREATE";
    public const string SalesReturnPost = "SALES_RETURN_POST";
    public const string SalesReturnVoid = "SALES_RETURN_VOID";
    public const string ViewSalesReturns = "VIEW_SALES_RETURNS";

    // Phase RET 2 — Purchase Returns
    public const string PurchaseReturnCreate = "PURCHASE_RETURN_CREATE";
    public const string PurchaseReturnPost = "PURCHASE_RETURN_POST";
    public const string PurchaseReturnVoid = "PURCHASE_RETURN_VOID";
    public const string ViewPurchaseReturns = "VIEW_PURCHASE_RETURNS";

    // Phase RET 3 — Pre-sale Dispositions
    public const string DispositionCreate = "DISPOSITION_CREATE";
    public const string DispositionPost = "DISPOSITION_POST";
    public const string DispositionApprove = "DISPOSITION_APPROVE";
    public const string DispositionVoid = "DISPOSITION_VOID";
    public const string ViewDispositions = "VIEW_DISPOSITIONS";

    /// <summary>All defined permission codes for seeding.</summary>
    public static readonly IReadOnlyList<(string Code, string Description)> All =
    [
        (UsersRead,        "View users"),
        (UsersWrite,       "Create, update, deactivate users"),
        (RolesRead,        "View roles and permissions"),
        (RolesWrite,       "Create, update, delete roles; manage role permissions"),
        (AuditRead,        "View audit logs"),
        (ProductsRead,     "View products and variants"),
        (ProductsWrite,    "Create, update, delete products and variants"),
        (CustomersRead,    "View customers"),
        (CustomersWrite,   "Create, update, delete customers"),
        (SuppliersRead,    "View suppliers"),
        (SuppliersWrite,   "Create, update, delete suppliers"),
        (ImportMasterData, "Import master data from Excel/CSV files"),
        (StockRead,        "View stock balances and ledger"),
        (StockPost,        "Post stock movements"),
        (WarehousesRead,   "View warehouses"),
        (WarehousesWrite,  "Create, update, delete warehouses"),
        (PurchasesRead,    "View purchase receipts"),
        (PurchasesWrite,   "Create, update, delete purchase receipts"),
        (PurchasesPost,    "Post purchase receipts to inventory"),
        (ProductionRead,   "View production batches"),
        (ProductionWrite,  "Create, update, delete production batches"),
        (ProductionPost,   "Post production batches to inventory"),
        (SalesRead,        "View sales invoices"),
        (SalesWrite,       "Create, update, delete sales invoices"),
        (SalesPost,        "Post sales invoices to inventory"),
        (AccountingRead,   "View AR/AP ledger and balances"),
        (PaymentsRead,     "View payments"),
        (PaymentsWrite,    "Create payments"),
        (ImportOpeningBalances, "Import opening balances from Excel/CSV files"),
        (ImportPayments,   "Import payments from Excel/CSV files"),
        (DashboardRead,    "View dashboard KPIs and analytics"),
        (ManagePrintingPolicy, "Manage printing profiles and rules"),
        (ManageReasonCodes,    "Create, update, disable reason codes"),
        (ViewReasonCodes,      "View reason codes catalog"),
        (SalesReturnCreate,    "Create and update draft sales returns"),
        (SalesReturnPost,      "Post sales returns to inventory"),
        (SalesReturnVoid,      "Void posted sales returns (manager/admin)"),
        (ViewSalesReturns,     "View sales returns"),
        (PurchaseReturnCreate, "Create and update draft purchase returns"),
        (PurchaseReturnPost,   "Post purchase returns to inventory"),
        (PurchaseReturnVoid,   "Void draft purchase returns"),
        (ViewPurchaseReturns,  "View purchase returns"),
        (DispositionCreate,    "Create and update draft inventory dispositions"),
        (DispositionPost,      "Post inventory dispositions to inventory"),
        (DispositionApprove,   "Approve inventory dispositions requiring manager approval"),
        (DispositionVoid,      "Void draft inventory dispositions"),
        (ViewDispositions,     "View inventory dispositions"),
    ];
}
