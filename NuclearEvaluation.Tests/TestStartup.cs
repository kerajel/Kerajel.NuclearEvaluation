using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.Server.Models.Identity;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Server.Validators;
using Radzen;

namespace NuclearEvaluation.Tests;

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

        services.AddDbContext<ApplicationIdentityContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);

        services.AddIdentity<ApplicationUser, ApplicationRole>()
                .AddEntityFrameworkStores<ApplicationIdentityContext>()
                .AddDefaultTokenProviders();

        services.AddScoped<ISessionCache, SessionCache>();
        services.AddScoped<SecurityService>();

        services.AddScoped<PresetFilterValidator>();
        services.AddScoped<ProjectViewValidator>();

        services.AddRadzenComponents();
    }
}