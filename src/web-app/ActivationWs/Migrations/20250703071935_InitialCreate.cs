using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivationWs.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    InstallationId = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ConfirmationId = table.Column<string>(type: "TEXT", nullable: false),
                    ActivationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRequestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LicenseStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationRecords");
        }
    }
}
