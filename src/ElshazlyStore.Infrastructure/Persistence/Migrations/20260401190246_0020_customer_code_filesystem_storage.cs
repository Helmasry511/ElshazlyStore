using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0020_customer_code_filesystem_storage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add CustomerCode column as nullable first (to allow backfill)
            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // 2) Backfill existing customers with YYYY-NNNNNN codes based on creation order
            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT ""Id"",
                           EXTRACT(YEAR FROM ""CreatedAtUtc"")::text || '-' ||
                           LPAD(ROW_NUMBER() OVER (
                               PARTITION BY EXTRACT(YEAR FROM ""CreatedAtUtc"")
                               ORDER BY ""CreatedAtUtc"", ""Id""
                           )::text, 6, '0') AS generated_code
                    FROM customers
                    WHERE ""CustomerCode"" IS NULL OR ""CustomerCode"" = ''
                )
                UPDATE customers
                SET ""CustomerCode"" = numbered.generated_code
                FROM numbered
                WHERE customers.""Id"" = numbered.""Id"";
            ");

            // 3) Now make it NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "CustomerCode",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<byte[]>(
                name: "FileContent",
                table: "customer_attachments",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "customer_attachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelativePath",
                table: "customer_attachments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "customer_attachments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerCode",
                table: "customers",
                column: "CustomerCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_CustomerCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "customer_attachments");

            migrationBuilder.DropColumn(
                name: "RelativePath",
                table: "customer_attachments");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "customer_attachments");

            migrationBuilder.AlterColumn<byte[]>(
                name: "FileContent",
                table: "customer_attachments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);
        }
    }
}
