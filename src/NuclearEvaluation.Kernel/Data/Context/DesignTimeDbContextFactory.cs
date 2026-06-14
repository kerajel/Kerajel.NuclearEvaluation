using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NuclearEvaluation.Kernel.Data.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NuclearEvaluationServerDbContext>
{
    public NuclearEvaluationServerDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<NuclearEvaluationServerDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=NuclearEvaluation;Trusted_Connection=True;TrustServerCertificate=True;",
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "DBO"));
        return new NuclearEvaluationServerDbContext(optionsBuilder.Options);
    }
}
