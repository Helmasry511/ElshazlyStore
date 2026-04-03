using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElshazlyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0010_printing_policy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "print_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreenCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_print_rules_print_profiles_PrintProfileId",
                        column: x => x.PrintProfileId,
                        principalTable: "print_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_print_profiles_is_default",
                table: "print_profiles",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_print_profiles_Name",
                table: "print_profiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_print_rules_profile_screen",
                table: "print_rules",
                columns: new[] { "PrintProfileId", "ScreenCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_print_rules_screen_code",
                table: "print_rules",
                column: "ScreenCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_rules");

            migrationBuilder.DropTable(
                name: "print_profiles");
        }
    }
}
