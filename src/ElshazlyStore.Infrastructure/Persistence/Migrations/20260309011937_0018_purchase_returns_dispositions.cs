using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0018_purchase_returns_dispositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_dispositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DispositionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DispositionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_dispositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_dispositions_stock_movements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_dispositions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_dispositions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_dispositions_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_dispositions_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalPurchaseReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_purchase_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_receipts_OriginalPurchaseReceiptId",
                        column: x => x.OriginalPurchaseReceiptId,
                        principalTable: "purchase_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_stock_movements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_returns_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_disposition_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryDispositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispositionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_disposition_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_disposition_lines_inventory_dispositions_Inventor~",
                        column: x => x.InventoryDispositionId,
                        principalTable: "inventory_dispositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_disposition_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_disposition_lines_reason_codes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "reason_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispositionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_purchase_returns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "purchase_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_reason_codes_ReasonCodeId",
                        column: x => x.ReasonCodeId,
                        principalTable: "reason_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_disposition_lines_InventoryDispositionId",
                table: "inventory_disposition_lines",
                column: "InventoryDispositionId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_disposition_lines_ReasonCodeId",
                table: "inventory_disposition_lines",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_disposition_lines_VariantId",
                table: "inventory_disposition_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_ApprovedByUserId",
                table: "inventory_dispositions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_CreatedAtUtc",
                table: "inventory_dispositions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_CreatedByUserId",
                table: "inventory_dispositions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_DispositionDateUtc",
                table: "inventory_dispositions",
                column: "DispositionDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_DispositionNumber",
                table: "inventory_dispositions",
                column: "DispositionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_PostedByUserId",
                table: "inventory_dispositions",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_Status",
                table: "inventory_dispositions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_status_posted",
                table: "inventory_dispositions",
                columns: new[] { "Status", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_StockMovementId",
                table: "inventory_dispositions",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_dispositions_WarehouseId",
                table: "inventory_dispositions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_PurchaseReturnId",
                table: "purchase_return_lines",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_ReasonCodeId",
                table: "purchase_return_lines",
                column: "ReasonCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_VariantId",
                table: "purchase_return_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_CreatedAtUtc",
                table: "purchase_returns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_CreatedByUserId",
                table: "purchase_returns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_OriginalPurchaseReceiptId",
                table: "purchase_returns",
                column: "OriginalPurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_PostedByUserId",
                table: "purchase_returns",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_ReturnDateUtc",
                table: "purchase_returns",
                column: "ReturnDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_ReturnNumber",
                table: "purchase_returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_Status",
                table: "purchase_returns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_status_posted",
                table: "purchase_returns",
                columns: new[] { "Status", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_StockMovementId",
                table: "purchase_returns",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_SupplierId",
                table: "purchase_returns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_WarehouseId",
                table: "purchase_returns",
                column: "WarehouseId");

            // ── GIN trgm indexes + sequences (from former 0016) ──
            migrationBuilder.Sql(@"
DO $$ BEGIN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;

    -- Sales Returns
    CREATE INDEX IF NOT EXISTS ""IX_sales_returns_return_number_trgm""
        ON sales_returns USING GIN (""ReturnNumber"" gin_trgm_ops);

    -- Purchase Returns
    CREATE INDEX IF NOT EXISTS ""IX_purchase_returns_return_number_trgm""
        ON purchase_returns USING GIN (""ReturnNumber"" gin_trgm_ops);

    -- Inventory Dispositions
    CREATE INDEX IF NOT EXISTS ""IX_inventory_dispositions_disposition_number_trgm""
        ON inventory_dispositions USING GIN (""DispositionNumber"" gin_trgm_ops);

    -- Sequences for document numbering (idempotent)
    CREATE SEQUENCE IF NOT EXISTS sales_return_number_seq START WITH 1 INCREMENT BY 1;
    CREATE SEQUENCE IF NOT EXISTS purchase_return_number_seq START WITH 1 INCREMENT BY 1;
    CREATE SEQUENCE IF NOT EXISTS disposition_number_seq START WITH 1 INCREMENT BY 1;

    PERFORM setval('sales_return_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""ReturnNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM sales_returns WHERE ""ReturnNumber"" LIKE 'RET-%'), 0) + 1), false);

    PERFORM setval('purchase_return_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""ReturnNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM purchase_returns WHERE ""ReturnNumber"" LIKE 'PRET-%'), 0) + 1), false);

    PERFORM setval('disposition_number_seq',
        GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(""DispositionNumber"" FROM '[0-9]+$') AS INTEGER))
            FROM inventory_dispositions WHERE ""DispositionNumber"" LIKE 'DISP-%'), 0) + 1), false);
END $$;
", suppressTransaction: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ BEGIN
    DROP INDEX IF EXISTS ""IX_sales_returns_return_number_trgm"";
    DROP INDEX IF EXISTS ""IX_purchase_returns_return_number_trgm"";
    DROP INDEX IF EXISTS ""IX_inventory_dispositions_disposition_number_trgm"";
    DROP SEQUENCE IF EXISTS sales_return_number_seq;
    DROP SEQUENCE IF EXISTS purchase_return_number_seq;
    DROP SEQUENCE IF EXISTS disposition_number_seq;
END $$;
");

            migrationBuilder.DropTable(
                name: "inventory_disposition_lines");

            migrationBuilder.DropTable(
                name: "purchase_return_lines");

            migrationBuilder.DropTable(
                name: "inventory_dispositions");

            migrationBuilder.DropTable(
                name: "purchase_returns");
        }
    }
}
