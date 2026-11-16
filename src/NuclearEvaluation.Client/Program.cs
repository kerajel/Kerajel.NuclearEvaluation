using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
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
builder.Services.AddScoped<SiteAssistantWorkspace>();
builder.Services.AddSingleton<IGridResultCache, GridResultCache>();

builder.Services.AddRadzenComponents();
builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "NuclearEvaluationTheme";
    options.Duration = TimeSpan.FromDays(365);
});

builder.Services.AddScoped<ProjectViewValidator>();
builder.Services.AddScoped<PresetFilterValidator>();

WebAssemblyHost host = builder.Build();

// Read the saved theme before the default theme can replace it on first render.
try
{
    IJSRuntime js = host.Services.GetRequiredService<IJSRuntime>();
    CookieThemeServiceOptions options = host
        .Services.GetRequiredService<IOptions<CookieThemeServiceOptions>>()
        .Value;
    string? savedTheme = await js.InvokeAsync<string?>("Radzen.getCookie", options.Name);
    if (savedTheme is "software" or "software-dark")
        host.Services.GetRequiredService<ThemeService>().SetTheme(savedTheme, triggerChange: false);
}
catch (JSException exception)
{
    host.Services.GetRequiredService<ILogger<App>>()
        .LogDebug(exception, "Unable to restore the browser appearance.");
}

await host.RunAsync();
