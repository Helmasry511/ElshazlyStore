namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Maps all API endpoint groups under /api/v1.
/// </summary>
public static class EndpointMapper
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapHealthEndpoints();
        v1.MapAuthEndpoints();
        v1.MapUserEndpoints();
        v1.MapRoleEndpoints();

        // Phase 2 — Master Data
        v1.MapProductEndpoints();
        v1.MapVariantEndpoints();
        v1.MapBarcodeEndpoints();
        v1.MapCustomerEndpoints();
        v1.MapSupplierEndpoints();
        v1.MapImportEndpoints();

        // Phase 3 — Inventory
        v1.MapWarehouseEndpoints();
        v1.MapStockMovementEndpoints();
        v1.MapStockEndpoints();

        // Phase 4 — Procurement
        v1.MapPurchaseEndpoints();

        // Phase 5 — Production
        v1.MapProductionEndpoints();

        // Phase 6 — POS / Sales
        v1.MapSalesEndpoints();

        // Phase 7 — AR/AP Accounting & Payments
        v1.MapAccountingEndpoints();
        v1.MapPaymentEndpoints();

        // Phase 8 — Dashboard
        v1.MapDashboardEndpoints();

        // Phase 9 — Printing Policy
        v1.MapPrintingPolicyEndpoints();

        // Phase RET 0 — Reason Codes
        v1.MapReasonCodeEndpoints();

        // Phase RET 1 — Sales Returns
        v1.MapSalesReturnEndpoints();
        v1.MapPurchaseReturnEndpoints();

        // Phase RET 3 — Inventory Dispositions
        v1.MapDispositionEndpoints();

        return app;
    }
}
