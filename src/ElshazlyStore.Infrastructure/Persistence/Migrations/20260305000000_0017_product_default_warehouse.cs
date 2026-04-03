using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BACKEND 3 — Adds nullable DefaultWarehouseId to products table (metadata only).
    /// FK to warehouses with ON DELETE SET NULL.
    /// No stock quantity fields are introduced.
    /// </summary>
    public partial class _0017_product_default_warehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_DefaultWarehouseId",
                table: "products",
                column: "DefaultWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_products_warehouses_DefaultWarehouseId",
                table: "products",
                column: "DefaultWarehouseId",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_warehouses_DefaultWarehouseId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_DefaultWarehouseId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DefaultWarehouseId",
                table: "products");
        }
    }
}
