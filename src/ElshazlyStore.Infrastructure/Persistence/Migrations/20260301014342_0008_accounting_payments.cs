using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0008_accounting_payments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RelatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WalletName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PaymentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_CreatedByUserId",
                table: "ledger_entries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PartyType_PartyId",
                table: "ledger_entries",
                columns: new[] { "PartyType", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_RelatedInvoiceId",
                table: "ledger_entries",
                column: "RelatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_RelatedPaymentId",
                table: "ledger_entries",
                column: "RelatedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_CreatedByUserId",
                table: "payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_PartyType_PartyId",
                table: "payments",
                columns: new[] { "PartyType", "PartyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
