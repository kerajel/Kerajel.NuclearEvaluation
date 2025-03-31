using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Kernel.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangePmiReportDistributionEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PmiReportDistributionStatus",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                newName: "DistributionStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DistributionStatus",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                newName: "PmiReportDistributionStatus");
        }
    }
}
