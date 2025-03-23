using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace NuclearEvaluation.Kernel.Data.Context;

public class NuclearEvaluationServerDbContextFactory : IDesignTimeDbContextFactory<NuclearEvaluationServerDbContext>
{
    public NuclearEvaluationServerDbContext CreateDbContext(string[] args)
    {
        if (args.Length < 1)
        {
            throw new Exception("Connection string is required as the first argument");
        }

        string connectionString = args[0];

        DbContextOptionsBuilder<NuclearEvaluationServerDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(connectionString);

        return new NuclearEvaluationServerDbContext(optionsBuilder.Options);
    }
}