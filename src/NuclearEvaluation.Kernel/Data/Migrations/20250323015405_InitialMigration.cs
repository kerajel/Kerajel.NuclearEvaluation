using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuclearEvaluation.Kernel.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DATA");

            migrationBuilder.EnsureSchema(
                name: "ADMIN");

            migrationBuilder.EnsureSchema(
                name: "EVALUATION");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "ADMIN",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "ADMIN",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
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
                    SeriesType = table.Column<byte>(type: "tinyint", nullable: false),
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
                name: "AspNetRoleClaims",
                schema: "ADMIN",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "ADMIN",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "ADMIN",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "ADMIN",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "ADMIN",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PmiReport",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmiReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PmiReport_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "ADMIN",
                        principalTable: "AspNetUsers",
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
                    PresetFilterEntryType = table.Column<byte>(type: "tinyint", nullable: false),
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
                name: "PmiReportDistributionEntry",
                schema: "EVALUATION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PmiReportId = table.Column<int>(type: "int", nullable: false),
                    PmiReportDistributionChannel = table.Column<byte>(type: "tinyint", nullable: false),
                    PmiReportDistributionStatus = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmiReportDistributionEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PmiReportDistributionEntry_PmiReport_PmiReportId",
                        column: x => x.PmiReportId,
                        principalSchema: "EVALUATION",
                        principalTable: "PmiReport",
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
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "ADMIN",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "ADMIN",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "ADMIN",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "ADMIN",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "ADMIN",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "ADMIN",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "ADMIN",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Particle_SubSampleId",
                schema: "DATA",
                table: "Particle",
                column: "SubSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_PmiReport_AuthorId",
                schema: "EVALUATION",
                table: "PmiReport",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PmiReportDistributionEntry_PmiReportId",
                schema: "EVALUATION",
                table: "PmiReportDistributionEntry",
                column: "PmiReportId");

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
                name: "AspNetRoleClaims",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "Particle",
                schema: "DATA");

            migrationBuilder.DropTable(
                name: "PmiReportDistributionEntry",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "PresetFilterEntry",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "ProjectSeries",
                schema: "EVALUATION");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "ADMIN");

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
                name: "AspNetUsers",
                schema: "ADMIN");

            migrationBuilder.DropTable(
                name: "Series",
                schema: "DATA");
        }
    }
}
