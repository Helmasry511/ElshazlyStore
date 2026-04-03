using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0005_procurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_stock_movements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_receipt_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_receipt_lines_purchase_receipts_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "purchase_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_payables_purchase_receipts_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "purchase_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_payables_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_lines_PurchaseReceiptId",
                table: "purchase_receipt_lines",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_lines_VariantId",
                table: "purchase_receipt_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_CreatedAtUtc",
                table: "purchase_receipts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_CreatedByUserId",
                table: "purchase_receipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_DocumentNumber",
                table: "purchase_receipts",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_Status",
                table: "purchase_receipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_StockMovementId",
                table: "purchase_receipts",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_SupplierId",
                table: "purchase_receipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_WarehouseId",
                table: "purchase_receipts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payables_PurchaseReceiptId",
                table: "supplier_payables",
                column: "PurchaseReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payables_SupplierId",
                table: "supplier_payables",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_receipt_lines");

            migrationBuilder.DropTable(
                name: "supplier_payables");

            migrationBuilder.DropTable(
                name: "purchase_receipts");
        }
    }
}
