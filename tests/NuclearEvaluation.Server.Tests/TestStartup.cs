using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Shared.Services;
using NuclearEvaluation.Shared.Validators;
using Radzen;

namespace NuclearEvaluation.Server.Tests;

public static class TestStartup
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("TestDbConnection")!;

        services.AddDbContext<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);

        services.AddDbContextFactory<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);

        services.AddScoped<ISessionCache, SessionCache>();
        services.AddScoped<SecurityService>();

        services.AddScoped<PresetFilterValidator>();
        services.AddScoped<ProjectViewValidator>();

        services.AddRadzenComponents();
    }
}