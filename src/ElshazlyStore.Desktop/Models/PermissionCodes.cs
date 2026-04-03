namespace ElshazlyStore.Desktop.Models;

/// <summary>
/// Permission code constants mirroring the backend Permissions.cs.
/// Used by IPermissionService to gate UI elements.
/// </summary>
public static class PermissionCodes
{
    // Identity
    public const string UsersRead = "USERS_READ";
    public const string UsersWrite = "USERS_WRITE";
    public const string RolesRead = "ROLES_READ";
    public const string RolesWrite = "ROLES_WRITE";
    public const string AuditRead = "AUDIT_READ";

    // Catalog
    public const string ProductsRead = "PRODUCTS_READ";
    public const string ProductsWrite = "PRODUCTS_WRITE";
    public const string CustomersRead = "CUSTOMERS_READ";
    public const string CustomersWrite = "CUSTOMERS_WRITE";
    public const string SuppliersRead = "SUPPLIERS_READ";
    public const string SuppliersWrite = "SUPPLIERS_WRITE";
    public const string ImportMasterData = "IMPORT_MASTER_DATA";

    // Stock
    public const string StockRead = "STOCK_READ";
    public const string StockPost = "STOCK_POST";
    public const string WarehousesRead = "WAREHOUSES_READ";
    public const string WarehousesWrite = "WAREHOUSES_WRITE";

    // Purchases
    public const string PurchasesRead = "PURCHASES_READ";
    public const string PurchasesWrite = "PURCHASES_WRITE";
    public const string PurchasesPost = "PURCHASES_POST";

    // Production
    public const string ProductionRead = "PRODUCTION_READ";
    public const string ProductionWrite = "PRODUCTION_WRITE";
    public const string ProductionPost = "PRODUCTION_POST";

    // Sales
    public const string SalesRead = "SALES_READ";
    public const string SalesWrite = "SALES_WRITE";
    public const string SalesPost = "SALES_POST";

    // Accounting
    public const string AccountingRead = "ACCOUNTING_READ";
    public const string PaymentsRead = "PAYMENTS_READ";
    public const string PaymentsWrite = "PAYMENTS_WRITE";
    public const string ImportOpeningBalances = "IMPORT_OPENING_BALANCES";
    public const string ImportPayments = "IMPORT_PAYMENTS";

    // Dashboard
    public const string DashboardRead = "DASHBOARD_READ";

    // Printing
    public const string ManagePrintingPolicy = "MANAGE_PRINTING_POLICY";

    // Returns
    public const string ManageReasonCodes = "MANAGE_REASON_CODES";
    public const string ViewReasonCodes = "VIEW_REASON_CODES";
    public const string SalesReturnCreate = "SALES_RETURN_CREATE";
    public const string SalesReturnPost = "SALES_RETURN_POST";
    public const string SalesReturnVoid = "SALES_RETURN_VOID";
    public const string ViewSalesReturns = "VIEW_SALES_RETURNS";
    public const string PurchaseReturnCreate = "PURCHASE_RETURN_CREATE";
    public const string PurchaseReturnPost = "PURCHASE_RETURN_POST";
    public const string PurchaseReturnVoid = "PURCHASE_RETURN_VOID";
    public const string ViewPurchaseReturns = "VIEW_PURCHASE_RETURNS";
    public const string DispositionCreate = "DISPOSITION_CREATE";
    public const string DispositionPost = "DISPOSITION_POST";
    public const string DispositionApprove = "DISPOSITION_APPROVE";
    public const string DispositionVoid = "DISPOSITION_VOID";
    public const string ViewDispositions = "VIEW_DISPOSITIONS";
}
