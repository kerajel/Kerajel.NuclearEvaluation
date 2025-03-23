using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Kernel.Models.Identity;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Data.Context;

public partial class NuclearEvaluationServerDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    private const string AdminSchema = "ADMIN";
    private const string DboSchema = "DBO";
    private const string DataSchema = "DATA";
    private const string EvaluationSchema = "EVALUATION";

    public NuclearEvaluationServerDbContext()
    {
    }

    public NuclearEvaluationServerDbContext(DbContextOptions<NuclearEvaluationServerDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Project { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<Sample> Sample { get; set; }
    public DbSet<SubSample> SubSample { get; set; }
    public DbSet<Apm> Apm { get; set; }
    public DbSet<Particle> Particle { get; set; }
    public DbSet<ProjectSeries> ProjectSeries { get; set; }
    public DbSet<PresetFilterEntry> PresetFilterEntry { get; set; }
    public DbSet<PresetFilter> PresetFilter { get; set; }

    public DbSet<ProjectView> ProjectView { get; set; }
    public DbSet<SeriesView> SeriesView { get; set; }
    public DbSet<SampleView> SampleView { get; set; }
    public DbSet<SubSampleView> SubSampleView { get; set; }
    public DbSet<ApmView> ApmView { get; set; }
    public DbSet<ParticleView> ParticleView { get; set; }
    public DbSet<ProjectViewSeriesView> ProjectViewSeriesView { get; set; }
    public DbSet<ProjectDecayCorrectedParticleView> ProjectDecayCorrectedParticleView { get; set; }
    public DbSet<ProjectDecayCorrectedApmView> ProjectDecayCorrectedApmView { get; set; }
    public DbSet<PmiReport> PmiReport { get; set; }
    public DbSet<PmiReportDistributionEntry> PmiReportDistributionEntry { get; set; }

    partial void OnModelBuilding(ModelBuilder builder);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(""
                , b => b.MigrationsHistoryTable("__EFMigrationsHistory", DboSchema));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureAdminSchema(modelBuilder);
        OnModelBuilding(modelBuilder);
        ConfigureDboSchema(modelBuilder);
        ConfigureDataSchema(modelBuilder);
        ConfigureEvaluationSchema(modelBuilder);
        ConfigureDataSchemaViews(modelBuilder);
        ConfigureEvaluationSchemaViews(modelBuilder);
        ConfigureDefaultOnDeleteBehavior(modelBuilder);
        ConfigureCascadeOnDeleteBehavior(modelBuilder);
    }

    static void ConfigureAdminSchema(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AspNetUsers", AdminSchema);
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("AspNetRoles", AdminSchema);
        });

        modelBuilder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("AspNetUserRoles", AdminSchema);
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("AspNetUserClaims", AdminSchema);
        });

        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("AspNetUserLogins", AdminSchema);
        });

        modelBuilder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("AspNetRoleClaims", AdminSchema);
        });

        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("AspNetUserTokens", AdminSchema);
        });

        modelBuilder.Entity<ApplicationUser>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<IdentityUserRole<string>>();
    }

    static void ConfigureDboSchema(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PresetFilter>(entity =>
        {
            entity.ToTable("PresetFilter", EvaluationSchema);
        });

        modelBuilder.Entity<PresetFilterEntry>(entity =>
        {
            entity.ToTable("PresetFilterEntry", EvaluationSchema);
            entity.Property(b => b.SerializedDescriptors)
                  .HasField("_serializedDescriptors")
                  .UsePropertyAccessMode(PropertyAccessMode.Property);
        });
    }

    static void ConfigureDataSchema(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>(entity =>
        {
            entity.ToTable("Series", DataSchema);
            entity.Property(e => e.Id)
                  .UseIdentityColumn(seed: 10000);
        });

        modelBuilder.Entity<Sample>(entity =>
        {
            entity.ToTable("Sample", DataSchema);
            entity.Property(x => x.SampleType)
                  .HasColumnType("tinyint")
                  .HasComputedColumnSql(Models.Domain.Sample.GetSampleTypeSqlExpression());
        });

        modelBuilder.Entity<SubSample>(entity =>
        {
            entity.ToTable("SubSample", DataSchema);
        });

        modelBuilder.Entity<Apm>(entity =>
        {
            entity.ToTable("Apm", DataSchema);
        });

        modelBuilder.Entity<Particle>(entity =>
        {
            entity.ToTable("Particle", DataSchema);
        });
    }

    static void ConfigureEvaluationSchema(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Project", EvaluationSchema);
        });

        modelBuilder.Entity<ProjectSeries>(entity =>
        {
            entity.ToTable("ProjectSeries", EvaluationSchema);
            entity.HasKey(ps => new { ps.ProjectId, ps.SeriesId });
            entity.HasOne(ps => ps.Project)
                  .WithMany(p => p.ProjectSeries)
                  .HasForeignKey(ps => ps.ProjectId);
            entity.HasOne(ps => ps.Series)
                  .WithMany(s => s.ProjectSeries)
                  .HasForeignKey(ps => ps.SeriesId);
        });

        modelBuilder.Entity<PmiReport>(entity =>
        {
            entity.ToTable("PmiReport", EvaluationSchema);
            entity.HasKey(pr => pr.Id);
            entity.HasMany(pr => pr.PmiReportDistributionEntries)
                  .WithOne(de => de.PmiReport)
                  .HasForeignKey(de => de.PmiReportId);
            entity.HasOne(pr => pr.Author)
                  .WithMany()
                  .HasForeignKey(pr => pr.AuthorId)
                  .IsRequired();
        });

        modelBuilder.Entity<PmiReportDistributionEntry>(entity =>
        {
            entity.ToTable("PmiReportDistributionEntry", EvaluationSchema);
            entity.HasKey(de => de.Id);
            entity.HasOne(de => de.PmiReport)
                  .WithMany(pr => pr.PmiReportDistributionEntries)
                  .HasForeignKey(de => de.PmiReportId);
        });
    }

    static void ConfigureDataSchemaViews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeriesView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("SeriesView", DataSchema);
            entity.HasMany(sv => sv.ProjectSeries)
                  .WithOne(pvs => pvs.Series)
                  .HasForeignKey(pvs => pvs.SeriesId);
        });

        modelBuilder.Entity<SampleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("SampleView", DataSchema);
        });

        modelBuilder.Entity<SubSampleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("SubSampleView", DataSchema);
        });

        modelBuilder.Entity<ParticleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("ParticleView", DataSchema);
        });

        modelBuilder.Entity<ApmView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("ApmView", DataSchema);
        });

        modelBuilder.Entity<ParticleView>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ToView("ParticleView", DataSchema);
        });
    }

    static void ConfigureEvaluationSchemaViews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView("ProjectView", EvaluationSchema);
            entity.HasMany(pv => pv.ProjectSeries)
                  .WithOne(pvs => pvs.Project)
                  .HasForeignKey(pvs => pvs.ProjectId);
        });

        modelBuilder.Entity<ProjectViewSeriesView>(entity =>
        {
            entity.HasKey(pvs => new { pvs.ProjectId, pvs.SeriesId });
            entity.ToView("ProjectViewSeriesView", EvaluationSchema);
            entity.HasOne(pvs => pvs.Project)
                  .WithMany(pv => pv.ProjectSeries)
                  .HasForeignKey(pvs => pvs.ProjectId);
            entity.HasOne(pvs => pvs.Series)
                  .WithMany(sv => sv.ProjectSeries)
                  .HasForeignKey(pvs => pvs.SeriesId);
        });

        modelBuilder.Entity<ProjectDecayCorrectedParticleView>(entity =>
        {
            entity.ToView("ProjectDecayCorrectedParticleView", EvaluationSchema);
            entity.HasBaseType(null as Type);
        });

        modelBuilder.Entity<ProjectDecayCorrectedApmView>(entity =>
        {
            entity.ToView("ProjectDecayCorrectedApmView", EvaluationSchema);
            entity.HasBaseType(null as Type);
        });
    }

    static void ConfigureDefaultOnDeleteBehavior(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    static void ConfigureCascadeOnDeleteBehavior(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PresetFilter>()
            .HasMany(p => p.Entries)
            .WithOne(e => e.PresetFilter)
            .HasForeignKey(e => e.PresetFilterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}