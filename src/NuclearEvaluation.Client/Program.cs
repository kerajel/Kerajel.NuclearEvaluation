using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NuclearEvaluation.Client;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using Radzen;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromMinutes(5),
});

builder.Services.AddScoped<INuclearEvaluationApi, NuclearEvaluationApiClient>();
builder.Services.AddScoped<ISessionCache, SessionCache>();

builder.Services.AddRadzenComponents();
builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "NuclearEvaluationTheme";
    options.Duration = TimeSpan.FromDays(365);
});

builder.Services.AddScoped<ProjectViewValidator>();
builder.Services.AddScoped<PresetFilterValidator>();
builder.Services.AddScoped<PmiReportSubmissionValidator>();

await builder.Build().RunAsync();
