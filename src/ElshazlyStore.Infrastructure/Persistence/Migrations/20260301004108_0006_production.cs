using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0006_production : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "production_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsumeMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProduceMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_batches_stock_movements_ConsumeMovementId",
                        column: x => x.ConsumeMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_production_batches_stock_movements_ProduceMovementId",
                        column: x => x.ProduceMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_production_batches_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_batches_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_batch_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_batch_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_batch_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_batch_lines_production_batches_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "production_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_production_batch_lines_ProductionBatchId",
                table: "production_batch_lines",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_production_batch_lines_VariantId",
                table: "production_batch_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_BatchNumber",
                table: "production_batches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_ConsumeMovementId",
                table: "production_batches",
                column: "ConsumeMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_CreatedAtUtc",
                table: "production_batches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_CreatedByUserId",
                table: "production_batches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_ProduceMovementId",
                table: "production_batches",
                column: "ProduceMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_Status",
                table: "production_batches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_WarehouseId",
                table: "production_batches",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "production_batch_lines");

            migrationBuilder.DropTable(
                name: "production_batches");
        }
    }
}
