using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Kernel.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEnumsToInt16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<short>(
                name: "SeriesType",
                schema: "DATA",
                table: "Series",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<short>(
                name: "PresetFilterEntryType",
                schema: "EVALUATION",
                table: "PresetFilterEntry",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<short>(
                name: "PmiReportDistributionStatus",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<short>(
                name: "PmiReportDistributionChannel",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<short>(
                name: "Status",
                schema: "EVALUATION",
                table: "PmiReport",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SeriesType",
                schema: "DATA",
                table: "Series",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "PresetFilterEntryType",
                schema: "EVALUATION",
                table: "PresetFilterEntry",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "PmiReportDistributionStatus",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "PmiReportDistributionChannel",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "EVALUATION",
                table: "PmiReport",
                type: "int",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");
        }
    }
}
