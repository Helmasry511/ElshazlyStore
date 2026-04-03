using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0011_hardening_indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_payments_date",
                table: "payments",
                column: "PaymentDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_payments_method",
                table: "payments",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_created",
                table: "ledger_entries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_entry_type",
                table: "ledger_entries",
                column: "EntryType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_date",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_method",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_created",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_entry_type",
                table: "ledger_entries");
        }
    }
}
