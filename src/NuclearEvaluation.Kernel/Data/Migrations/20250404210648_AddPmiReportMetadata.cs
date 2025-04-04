using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Kernel.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPmiReportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PmiReportFileMetadata",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PmiReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmiReportFileMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PmiReportFileMetadata_PmiReport_PmiReportId",
                        column: x => x.PmiReportId,
                        principalSchema: "EVALUATION",
                        principalTable: "PmiReport",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PmiReportFileMetadata_PmiReportId",
                schema: "EVALUATION",
                table: "PmiReportFileMetadata",
                column: "PmiReportId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PmiReportFileMetadata",
                schema: "EVALUATION");
        }
    }
}
