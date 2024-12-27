using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Filters;
using NuclearEvaluation.Library.Models.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NuclearEvaluation.Server.Data;

public class NuclearEvaluationServerDbContext : DbContext
{
    public NuclearEvaluationServerDbContext(DbContextOptions<NuclearEvaluationServerDbContext> options) : base(options) { }

    // Domain Entities
    public DbSet<Project> Project { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<Sample> Sample { get; set; }
    public DbSet<SubSample> SubSample { get; set; }
    public DbSet<Apm> Apm { get; set; }
    public DbSet<Particle> Particle { get; set; }
    public DbSet<ProjectSeries> ProjectSeries { get; set; }
    public DbSet<PresetFilterEntry> PresetFilterEntry { get; set; }
    public DbSet<PresetFilter> PresetFilter { get; set; }

    // View Entities
    public DbSet<ProjectView> ProjectView { get; set; }
    public DbSet<SeriesView> SeriesView { get; set; }
    public DbSet<SampleView> SampleView { get; set; }
    public DbSet<SubSampleView> SubSampleView { get; set; }
    public DbSet<ApmView> ApmView { get; set; }
    public DbSet<ParticleView> ParticleView { get; set; }
    public DbSet<ProjectViewSeriesView> ProjectViewSeriesView { get; set; }
    public DbSet<ProjectDecayCorrectedParticleView> ProjectDecayCorrectedParticleView { get; set; }
    public DbSet<ProjectDecayCorrectedApmView> ProjectDecayCorrectedApmView { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureDomainModels(modelBuilder);
        ConfigureViewModels(modelBuilder);
        ConfigureManyToManyRelationships(modelBuilder);
        ConfigureDefaultOnDeleteBehavior(modelBuilder);
        ConfigureCascadeOnDeleteBehavior(modelBuilder);
    }

    static void ConfigureDomainModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>()
            .Property(e => e.Id)
            .UseIdentityColumn(seed: 10000);

        modelBuilder.Entity<Sample>()
            .Property(x => x.SampleType)
            .HasColumnType("tinyint")
            .HasComputedColumnSql(NuclearEvaluation.Library.Models.Domain.Sample.GetSampleTypeSqlExpression());

        modelBuilder.Entity<PresetFilterEntry>()
            .Property(b => b.SerializedDescriptors)
            .HasField("_serializedDescriptors")
            .UsePropertyAccessMode(PropertyAccessMode.Property);
    }

    static void ConfigureViewModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(ProjectView));
            entity.HasMany(pv => pv.ProjectSeries)
                  .WithOne(pvs => pvs.Project)
                  .HasForeignKey(pvs => pvs.ProjectId);
        });

        modelBuilder.Entity<SeriesView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(SeriesView));
            entity.HasMany(sv => sv.ProjectSeries)
                  .WithOne(pvs => pvs.Series)
                  .HasForeignKey(pvs => pvs.SeriesId);
        });

        modelBuilder.Entity<ProjectViewSeriesView>(entity =>
        {
            entity.HasKey(pvs => new { pvs.ProjectId, pvs.SeriesId });
            entity.ToView(nameof(ProjectViewSeriesView));
            entity.HasOne(pvs => pvs.Project)
                  .WithMany(pv => pv.ProjectSeries)
                  .HasForeignKey(pvs => pvs.ProjectId);
            entity.HasOne(pvs => pvs.Series)
                  .WithMany(sv => sv.ProjectSeries)
                  .HasForeignKey(pvs => pvs.SeriesId);
        });

        modelBuilder.Entity<SampleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(SampleView));
        });

        modelBuilder.Entity<SubSampleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(SubSampleView));
        });

        modelBuilder.Entity<ParticleView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(ParticleView));
        });

        modelBuilder.Entity<ApmView>(entity =>
        {
            entity.HasAlternateKey(x => x.Id);
            entity.ToView(nameof(ApmView));
        });

        modelBuilder.Entity<ParticleView>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ToView(nameof(ParticleView));
        });

        modelBuilder.Entity<ProjectDecayCorrectedParticleView>(entity =>
        {
            entity.ToView(nameof(ProjectDecayCorrectedParticleView));
            entity.HasBaseType(null as Type);
        });

        modelBuilder.Entity<ProjectDecayCorrectedApmView>(entity =>
        {
            entity.ToView(nameof(ProjectDecayCorrectedApmView));
            entity.HasBaseType(null as Type);
        });
    }

    static void ConfigureManyToManyRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectSeries>()
            .HasKey(ps => new { ps.ProjectId, ps.SeriesId });

        modelBuilder.Entity<ProjectSeries>()
            .HasOne(ps => ps.Project)
            .WithMany(p => p.ProjectSeries)
            .HasForeignKey(ps => ps.ProjectId);

        modelBuilder.Entity<ProjectSeries>()
            .HasOne(ps => ps.Series)
            .WithMany(s => s.ProjectSeries)
            .HasForeignKey(ps => ps.SeriesId);
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