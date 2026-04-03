using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0012_perf_indexes_trgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Token",
                table: "refresh_tokens",
                newName: "TokenHash");

            migrationBuilder.RenameColumn(
                name: "ReplacedByToken",
                table: "refresh_tokens",
                newName: "ReplacedByTokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                newName: "IX_refresh_tokens_TokenHash");

            // ── B-tree composite indexes for list/filter queries ──

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_reference",
                table: "stock_movements",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_type_posted",
                table: "stock_movements",
                columns: new[] { "Type", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_status_created",
                table: "purchase_receipts",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_status_created",
                table: "production_batches",
                columns: new[] { "Status", "CreatedAtUtc" });

            // ── Unique filtered index on Reference for idempotency ──
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_stock_movements_reference_unique""
    ON stock_movements (""Reference"")
    WHERE ""Reference"" IS NOT NULL;
");

            // ── pg_trgm extension + GIN indexes for text search ──
            // These support ILIKE '%pattern%' queries via gin_trgm_ops.
            // Wrapped in DO block so they are no-ops on non-PostgreSQL providers.

            migrationBuilder.Sql(@"
DO $$ BEGIN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;

    -- Products
    CREATE INDEX IF NOT EXISTS ""IX_products_name_trgm""
        ON products USING GIN (""Name"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_products_category_trgm""
        ON products USING GIN (""Category"" gin_trgm_ops);

    -- Product Variants
    CREATE INDEX IF NOT EXISTS ""IX_product_variants_sku_trgm""
        ON product_variants USING GIN (""Sku"" gin_trgm_ops);

    -- Customers
    CREATE INDEX IF NOT EXISTS ""IX_customers_name_trgm""
        ON customers USING GIN (""Name"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_customers_code_trgm""
        ON customers USING GIN (""Code"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_customers_phone_trgm""
        ON customers USING GIN (""Phone"" gin_trgm_ops);

    -- Suppliers
    CREATE INDEX IF NOT EXISTS ""IX_suppliers_name_trgm""
        ON suppliers USING GIN (""Name"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_suppliers_code_trgm""
        ON suppliers USING GIN (""Code"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_suppliers_phone_trgm""
        ON suppliers USING GIN (""Phone"" gin_trgm_ops);

    -- Sales Invoices
    CREATE INDEX IF NOT EXISTS ""IX_sales_invoices_number_trgm""
        ON sales_invoices USING GIN (""InvoiceNumber"" gin_trgm_ops);

    -- Purchase Receipts
    CREATE INDEX IF NOT EXISTS ""IX_purchase_receipts_docnum_trgm""
        ON purchase_receipts USING GIN (""DocumentNumber"" gin_trgm_ops);

    -- Production Batches
    CREATE INDEX IF NOT EXISTS ""IX_production_batches_batchnum_trgm""
        ON production_batches USING GIN (""BatchNumber"" gin_trgm_ops);

    -- Warehouses
    CREATE INDEX IF NOT EXISTS ""IX_warehouses_name_trgm""
        ON warehouses USING GIN (""Name"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_warehouses_code_trgm""
        ON warehouses USING GIN (""Code"" gin_trgm_ops);

    -- Print Profiles
    CREATE INDEX IF NOT EXISTS ""IX_print_profiles_name_trgm""
        ON print_profiles USING GIN (""Name"" gin_trgm_ops);

    -- Print Rules (screen code search)
    CREATE INDEX IF NOT EXISTS ""IX_print_rules_screencode_trgm""
        ON print_rules USING GIN (""ScreenCode"" gin_trgm_ops);

    -- Payments (reference / wallet search)
    CREATE INDEX IF NOT EXISTS ""IX_payments_reference_trgm""
        ON payments USING GIN (""Reference"" gin_trgm_ops);
    CREATE INDEX IF NOT EXISTS ""IX_payments_walletname_trgm""
        ON payments USING GIN (""WalletName"" gin_trgm_ops);

    -- ── PostgreSQL sequences for document numbering ──
    CREATE SEQUENCE IF NOT EXISTS sales_invoice_number_seq START 1;
    CREATE SEQUENCE IF NOT EXISTS purchase_receipt_number_seq START 1;
    CREATE SEQUENCE IF NOT EXISTS production_batch_number_seq START 1;

    -- Advance sequences past the MAX existing auto-generated number
    PERFORM setval('sales_invoice_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""InvoiceNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM sales_invoices WHERE ""InvoiceNumber"" LIKE 'INV-%'), 0) + 1), false);

    PERFORM setval('purchase_receipt_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""DocumentNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM purchase_receipts WHERE ""DocumentNumber"" LIKE 'PR-%'), 0) + 1), false);

    PERFORM setval('production_batch_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""BatchNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM production_batches WHERE ""BatchNumber"" LIKE 'PB-%'), 0) + 1), false);

END $$;
", suppressTransaction: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Drop GIN trgm indexes ──
            migrationBuilder.Sql(@"
DO $$ BEGIN
    DROP INDEX IF EXISTS ""IX_products_name_trgm"";
    DROP INDEX IF EXISTS ""IX_products_category_trgm"";
    DROP INDEX IF EXISTS ""IX_product_variants_sku_trgm"";
    DROP INDEX IF EXISTS ""IX_customers_name_trgm"";
    DROP INDEX IF EXISTS ""IX_customers_code_trgm"";
    DROP INDEX IF EXISTS ""IX_suppliers_name_trgm"";
    DROP INDEX IF EXISTS ""IX_suppliers_code_trgm"";
    DROP INDEX IF EXISTS ""IX_sales_invoices_number_trgm"";
    DROP INDEX IF EXISTS ""IX_purchase_receipts_docnum_trgm"";
    DROP INDEX IF EXISTS ""IX_production_batches_batchnum_trgm"";
    DROP INDEX IF EXISTS ""IX_warehouses_name_trgm"";
    DROP INDEX IF EXISTS ""IX_warehouses_code_trgm"";
    DROP INDEX IF EXISTS ""IX_print_profiles_name_trgm"";
    DROP INDEX IF EXISTS ""IX_print_rules_screencode_trgm"";
    DROP INDEX IF EXISTS ""IX_customers_phone_trgm"";
    DROP INDEX IF EXISTS ""IX_suppliers_phone_trgm"";
    DROP INDEX IF EXISTS ""IX_payments_reference_trgm"";
    DROP INDEX IF EXISTS ""IX_payments_walletname_trgm"";
    DROP INDEX IF EXISTS ""IX_stock_movements_reference_unique"";

    DROP SEQUENCE IF EXISTS sales_invoice_number_seq;
    DROP SEQUENCE IF EXISTS purchase_receipt_number_seq;
    DROP SEQUENCE IF EXISTS production_batch_number_seq;
END $$;
");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_reference",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_type_posted",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_status_created",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_production_batches_status_created",
                table: "production_batches");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "refresh_tokens",
                newName: "Token");

            migrationBuilder.RenameColumn(
                name: "ReplacedByTokenHash",
                table: "refresh_tokens",
                newName: "ReplacedByToken");

            migrationBuilder.RenameIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                newName: "IX_refresh_tokens_Token");
        }
    }
}
