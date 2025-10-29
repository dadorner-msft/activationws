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
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtendedProductID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InstallationID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ConfirmationID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LicenseAcquisitionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivationRecords_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRecord_LicenseDate",
                table: "ActivationRecords",
                column: "LicenseAcquisitionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRecord_Unique",
                table: "ActivationRecords",
                columns: new[] { "MachineId", "InstallationID", "ExtendedProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Hostname",
                table: "Machines",
                column: "Hostname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationRecords");

            migrationBuilder.DropTable(
                name: "Machines");
        }
    }
}
