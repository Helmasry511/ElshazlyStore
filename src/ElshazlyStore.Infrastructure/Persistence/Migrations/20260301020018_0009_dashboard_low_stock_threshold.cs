using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0009_dashboard_low_stock_threshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LowStockThreshold",
                table: "product_variants",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_status_posted",
                table: "sales_invoices",
                columns: new[] { "Status", "PostedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_status_posted",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "product_variants");
        }
    }
}
