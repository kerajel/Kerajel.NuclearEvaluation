using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Kernel.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DATA");

            migrationBuilder.EnsureSchema(
                name: "EVALUATION");

            migrationBuilder.CreateTable(
                name: "PmiReport",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmiReport", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PresetFilter",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetFilter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Project",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Conclusions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FollowUpActionsRecommended = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecayCorrectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                schema: "DATA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "10000, 1"),
                    SeriesType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SgasComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WorkingPaperLink = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDu = table.Column<bool>(type: "bit", nullable: false),
                    IsNu = table.Column<bool>(type: "bit", nullable: false),
                    AnalysisCompleteDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "PresetFilterEntry",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PresetFilterEntryType = table.Column<int>(type: "int", nullable: false),
                    LogicalFilterOperator = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PresetFilterId = table.Column<int>(type: "int", nullable: false),
                    SerializedDescriptors = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetFilterEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresetFilterEntry_PresetFilter_PresetFilterId",
                        column: x => x.PresetFilterId,
                        principalSchema: "EVALUATION",
                        principalTable: "PresetFilter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSeries",
                schema: "EVALUATION",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    SeriesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSeries", x => new { x.ProjectId, x.SeriesId });
                    table.ForeignKey(
                        name: "FK_ProjectSeries_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "EVALUATION",
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSeries_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "DATA",
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sample",
                schema: "DATA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeriesId = table.Column<int>(type: "int", nullable: false),
                    ExternalCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SamplingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SampleClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleType = table.Column<byte>(type: "tinyint", nullable: false, computedColumnSql: "CASE WHEN SampleClass LIKE 'PIC%' THEN 3 WHEN SampleClass LIKE '%QC%' THEN 4 ELSE 2 END"),
                    Latitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sample_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "DATA",
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubSample",
                schema: "DATA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SampleId = table.Column<int>(type: "int", nullable: false),
                    ExternalCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ScreeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadResultDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsFromLegacySystem = table.Column<bool>(type: "bit", nullable: false),
                    ActivityNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubSample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubSample_Sample_SampleId",
                        column: x => x.SampleId,
                        principalSchema: "DATA",
                        principalTable: "Sample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Apm",
                schema: "DATA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubSampleId = table.Column<int>(type: "int", nullable: false),
                    U234 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU234 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    U235 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU235 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    U236 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU236 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    U238 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU238 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apm_SubSample_SubSampleId",
                        column: x => x.SubSampleId,
                        principalSchema: "DATA",
                        principalTable: "SubSample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Particle",
                schema: "DATA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubSampleId = table.Column<int>(type: "int", nullable: false),
                    ParticleExternalId = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AnalysisDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsNu = table.Column<bool>(type: "bit", nullable: false),
                    LaboratoryCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    U234 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU234 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    U235 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    ErU235 = table.Column<decimal>(type: "decimal(38,15)", precision: 38, scale: 15, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Particle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Particle_SubSample_SubSampleId",
                        column: x => x.SubSampleId,
                        principalSchema: "DATA",
                        principalTable: "SubSample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apm_SubSampleId",
                schema: "DATA",
                table: "Apm",
                column: "SubSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Particle_SubSampleId",
                schema: "DATA",
                table: "Particle",
                column: "SubSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_PmiReportFileMetadata_PmiReportId",
                schema: "EVALUATION",
                table: "PmiReportFileMetadata",
                column: "PmiReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PresetFilterEntry_PresetFilterId",
                schema: "EVALUATION",
                table: "PresetFilterEntry",
                column: "PresetFilterId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Name",
                schema: "EVALUATION",
                table: "Project",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSeries_SeriesId",
                schema: "EVALUATION",
                table: "ProjectSeries",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Sample_SeriesId",
                schema: "DATA",
                table: "Sample",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSample_SampleId",
                schema: "DATA",
                table: "SubSample",
                column: "SampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apm",
                schema: "DATA");

            migrationBuilder.DropTable(
                name: "Particle",
                schema: "DATA");

            migrationBuilder.DropTable(
                name: "PmiReportFileMetadata",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "PresetFilterEntry",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "ProjectSeries",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "SubSample",
                schema: "DATA");

            migrationBuilder.DropTable(
                name: "PmiReport",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "PresetFilter",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "Project",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "Sample",
                schema: "DATA");

            migrationBuilder.DropTable(
                name: "Series",
                schema: "DATA");
        }
    }
}
