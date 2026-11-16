using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal static class AssistantExtensions
{
    public static void AddAssistant(this IServiceCollection services, bool enableLocalHttp)
    {
        IMcpServerBuilder mcp = services
            .AddMcpServer()
            .WithTools<LocalSeriesTools>()
            .WithTools<LocalSiteAssistantTools>();
        if (enableLocalHttp)
            mcp.WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless);

        services.AddSingleton<AssistantCatalogCache<SeriesSchemaDescription>>();
        services.AddSingleton<AssistantCatalogCache<SiteAssistantCatalog>>();
        services.AddSingleton(_ => new AssistantRequestLimiter(TimeProvider.System));
        services.AddSingleton<InProcessAssistantMcp>();
        services.AddTransient<LocalSiteAssistantClient>();
        services.AddTransient<OpenAiSeriesAssistant>();
        services.AddTransient<OpenAiSiteAssistant>();

        services
            .AddHttpClient<LocalAssistantToolsClient>(client =>
                client.Timeout = TimeSpan.FromSeconds(15)
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseProxy = false,
                }
            );

        services
            .AddHttpClient<OpenAiAssistantClient>(client =>
                client.Timeout = TimeSpan.FromSeconds(45)
            )
            .RedactLoggedHeaders(["Authorization"])
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }
            );
    }

    public static void UseLocalAssistantAccess(this WebApplication app)
    {
        app.Use(
            async (context, next) =>
            {
                PathString path = context.Request.Path;
                if (
                    (path.StartsWithSegments("/mcp") || path.StartsWithSegments("/api/assistant"))
                    && !IsLocalRequest(context)
                )
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next(context);
            }
        );
    }

    public static void MapAssistant(this WebApplication app, bool enableLocalHttp)
    {
        if (enableLocalHttp)
            app.MapMcp("/mcp");

        RouteGroupBuilder routes = app.MapGroup("/api/assistant");
        routes.MapGet("/status", (OpenAiAssistantClient assistant) =>
            new AssistantStatus(enableLocalHttp || assistant.IsConfigured)
        );
        AssistantRequestLimiter limiter =
            app.Services.GetRequiredService<AssistantRequestLimiter>();
        RouteGroupBuilder prompts = routes
            .MapGroup("")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .AddEndpointFilter(
                async (invocation, next) =>
                {
                    HttpContext context = invocation.HttpContext;
                    TimeSpan retryAfter = limiter.TryAcquire(context.Connection.RemoteIpAddress);
                    if (retryAfter > TimeSpan.Zero)
                    {
                        context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                            .ToString(CultureInfo.InvariantCulture);
                        return Results.Problem(
                            title: "Assistant rate limit reached.",
                            statusCode: StatusCodes.Status429TooManyRequests
                        );
                    }

                    return await next(invocation);
                }
            );
        if (enableLocalHttp)
            prompts.MapPost("/ask", AskAsync);
        prompts.MapPost("/chat", AskSiteAsync);
    }

    static Task<IResult> AskSiteAsync(
        SiteAssistantQuestion question,
        OpenAiSiteAssistant assistant,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(question.Message)
            || question.Message.Length > OpenAiSeriesAssistant.MaxQuestionLength
            || !LocalSiteAssistantTools.IsContextValid(question.Context)
            || (
                question.History is { } history
                && (
                    history.Count > OpenAiSiteAssistant.MaxHistory
                    || history.Any(turn =>
                        turn is null
                        || turn.Question
                            is not { Length: > 0 and <= OpenAiSeriesAssistant.MaxQuestionLength }
                        || turn.Reply is not { Length: <= 1_200 }
                    )
                )
            )
        )
        {
            return Task.FromResult(
                Results.Problem(
                    title: "Enter a short question for the active view.",
                    statusCode: StatusCodes.Status400BadRequest
                )
            );
        }

        return ReplyAsync(() => assistant.AskAsync(question, ct), ct);
    }

    static Task<IResult> AskAsync(
        AssistantQuestion question,
        OpenAiSeriesAssistant assistant,
        LocalAssistantToolsClient tools,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(question.Message)
            || question.Message.Length > OpenAiSeriesAssistant.MaxQuestionLength
        )
        {
            return Task.FromResult(
                Results.Problem(
                    title: $"Enter a question up to {OpenAiSeriesAssistant.MaxQuestionLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest
                )
            );
        }

        return ReplyAsync(
            () =>
                assistant.IsConfigured
                    ? assistant.AskAsync(question.Message.Trim(), ct)
                    : InterpretLocalQuestionAsync(question.Message, tools, ct),
            ct
        );
    }

    static async Task<AssistantReply> InterpretLocalQuestionAsync(
        string message,
        LocalAssistantToolsClient tools,
        CancellationToken ct
    )
    {
        SeriesSchemaDescription schema = await tools.GetSeriesSchemaAsync(ct);
        SeriesGridQuery? candidate = LocalSeriesQuestion.Parse(message, schema);
        SeriesGridQuery? query = candidate is null
            ? null
            : await tools.BuildSeriesQueryAsync(candidate, ct);
        if (query is null)
        {
            return new(
                "Try a field name and comparison, such as: show me top 5 series where Is DU is false and there is at least 1 QC sample order by ID desc.",
                null
            );
        }

        return new("Applied to the Series grid.", query);
    }

    static async Task<IResult> ReplyAsync<T>(Func<Task<T>> reply, CancellationToken ct)
    {
        try
        {
            return Results.Ok(await reply());
        }
        catch (Exception ex)
            when (ex is TimeoutException
                || (ex is OperationCanceledException && !ct.IsCancellationRequested)
            )
        {
            return Results.Problem(
                title: "The assistant timed out. Try again.",
                statusCode: StatusCodes.Status504GatewayTimeout
            );
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or McpException
                        or JsonException
                        or InvalidDataException
                        or InvalidOperationException
            )
        {
            return Results.Problem(
                title: "The assistant service is unavailable. Try again later.",
                statusCode: StatusCodes.Status502BadGateway
            );
        }
    }

    static bool IsLocalRequest(HttpContext context)
    {
        HttpRequest request = context.Request;
        if (
            context.Connection.RemoteIpAddress is not { } peer
            || !IsLoopback(peer)
            || !IsLoopbackHost(request.Host.Host)
        )
        {
            return false;
        }

        // MCP clients can omit Origin. Browser requests must come from this same local origin.
        if (!request.Headers.TryGetValue("Origin", out StringValues origins))
            return true;

        return origins.Count == 1
            && Uri.TryCreate(origins[0], UriKind.Absolute, out Uri? origin)
            && string.IsNullOrEmpty(origin.UserInfo)
            && origin.PathAndQuery == "/"
            && string.IsNullOrEmpty(origin.Fragment)
            && origin.Scheme == request.Scheme
            && origin.Host.Equals(request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && origin.Port == (request.Host.Port ?? (request.IsHttps ? 443 : 80));
    }

    static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? address) && IsLoopback(address));

    static bool IsLoopback(IPAddress address) =>
        IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
}
