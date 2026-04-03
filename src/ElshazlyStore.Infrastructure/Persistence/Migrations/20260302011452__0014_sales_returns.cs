using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0014_sales_returns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalSalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_returns_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_sales_invoices_OriginalSalesInvoiceId",
                        column: x => x.OriginalSalesInvoiceId,
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_stock_movements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_returns_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispositionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_return_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_lines_reason_codes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "reason_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_lines_sales_returns_SalesReturnId",
                        column: x => x.SalesReturnId,
                        principalTable: "sales_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_lines_ReasonCodeId",
                table: "sales_return_lines",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_lines_SalesReturnId",
                table: "sales_return_lines",
                column: "SalesReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_lines_VariantId",
                table: "sales_return_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_CreatedAtUtc",
                table: "sales_returns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_CreatedByUserId",
                table: "sales_returns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_CustomerId",
                table: "sales_returns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_OriginalSalesInvoiceId",
                table: "sales_returns",
                column: "OriginalSalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_PostedByUserId",
                table: "sales_returns",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_ReturnDateUtc",
                table: "sales_returns",
                column: "ReturnDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_ReturnNumber",
                table: "sales_returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_Status",
                table: "sales_returns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_status_posted",
                table: "sales_returns",
                columns: new[] { "Status", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_StockMovementId",
                table: "sales_returns",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_WarehouseId",
                table: "sales_returns",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_return_lines");

            migrationBuilder.DropTable(
                name: "sales_returns");
        }
    }
}
