using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Server.Migrations
{
    /// <inheritdoc />
    public partial class NuclearEvaluationServerDbContext_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PresetFilter",
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
                name: "PresetFilterEntry",
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
                        principalTable: "PresetFilter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSeries",
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
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSeries_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sample",
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
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubSample",
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
                        principalTable: "Sample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Apm",
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
                        principalTable: "SubSample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Particle",
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
                        principalTable: "SubSample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apm_SubSampleId",
                table: "Apm",
                column: "SubSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Particle_SubSampleId",
                table: "Particle",
                column: "SubSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_PresetFilterEntry_PresetFilterId",
                table: "PresetFilterEntry",
                column: "PresetFilterId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Name",
                table: "Project",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSeries_SeriesId",
                table: "ProjectSeries",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Sample_SeriesId",
                table: "Sample",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSample_SampleId",
                table: "SubSample",
                column: "SampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apm");

            migrationBuilder.DropTable(
                name: "Particle");

            migrationBuilder.DropTable(
                name: "PresetFilterEntry");

            migrationBuilder.DropTable(
                name: "ProjectSeries");

            migrationBuilder.DropTable(
                name: "SubSample");

            migrationBuilder.DropTable(
                name: "PresetFilter");

            migrationBuilder.DropTable(
                name: "Project");

            migrationBuilder.DropTable(
                name: "Sample");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
