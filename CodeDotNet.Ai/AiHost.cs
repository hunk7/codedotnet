using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Knowledge;
using CodeDotNet.Ai.Providers;
using CodeDotNet.Ai.Resources;
using CodeDotNet.Ai.Security;
using CodeDotNet.Ai.Services;
using CodeDotNet.Ai.Streaming;

namespace CodeDotNet.Ai;

public sealed record PairRequest(string? Code);

public sealed record KbDocumentRequest(string? Title, string? Source, string? Text);

public sealed record HealthCheck(string Name, string Status, string? Detail = null);

public static class AiApp
{
    public const long MaxRequestBodyBytes = 256 * 1024;

    public static WebApplication Build(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        var options = new AiOptions();
        builder.Configuration.GetSection(AiOptions.SectionName).Bind(options);
        builder.Services.AddSingleton(options);

        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxRequestBodyBytes);
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.WebHost.UseUrls($"http://{options.Host}:{options.Port}");
        }

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<PairingService>();
        builder.Services.AddHostedService<PairingRotationService>();
        builder.Services.AddSingleton<IHardwareProbe, HardwareProbe>();
        builder.Services.AddSingleton<IProfileResolver, ProfileResolver>();
        builder.Services.AddSingleton<IResourceGovernor, ResourceGovernor>();
        builder.Services.AddSingleton<KbDatabase>();
        builder.Services.AddSingleton<IChunkRepository, SqliteChunkRepository>();
        options.ValidateVectorStore();
        if (options.UseQdrant)
        {
            builder.Services.AddHttpClient<QdrantVectorStore>(client =>
            {
                client.BaseAddress = new Uri(options.QdrantUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                if (!string.IsNullOrEmpty(options.QdrantApiKey))
                {
                    client.DefaultRequestHeaders.Add("api-key", options.QdrantApiKey);
                }
            });
            builder.Services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<QdrantVectorStore>());
        }
        else
        {
            builder.Services.AddSingleton<IVectorStore, SqliteVectorStore>();
        }
        builder.Services.AddSingleton<KnowledgeService>();
        builder.Services.AddSingleton<AskService>();
        builder.Services.AddSingleton<ISandboxClient, WebSocketSandboxClient>();
        builder.Services.AddSingleton<CodeWorkflowService>();
        builder.Services.AddSingleton<TestGenerationService>();
        builder.Services.AddSingleton<AgentRunService>();

        builder.Services.AddHttpClient<OllamaProvider>(client =>
        {
            client.BaseAddress = new Uri(options.OllamaBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });
        builder.Services.AddSingleton<IChatProvider>(sp => sp.GetRequiredService<OllamaProvider>());
        builder.Services.AddSingleton<IEmbeddingProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        builder.Services.AddSingleton(sp =>
        {
            var hardware = sp.GetRequiredService<IHardwareProbe>().ProbeAsync().GetAwaiter().GetResult();
            var resolved = sp.GetRequiredService<IProfileResolver>().Resolve(hardware, new ProfileOverrides(options.Profile));
            return options.ApplyOverrides(resolved);
        });

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseMiddleware<SecurityMiddleware>();
        MapEndpoints(app);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var pairing = app.Services.GetRequiredService<PairingService>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CodeDotNet.Ai");
            logger.LogInformation("CodeDotNet.Ai listening on http://{Host}:{Port}", options.Host, options.Port);
            if (!app.Environment.IsEnvironment("Testing"))
            {
                Console.WriteLine($"Pairing code (valid {PairingService.CodeLifetime.TotalMinutes:0} min): {pairing.CurrentCode}");
            }
        });

        return app;
    }

    private static void MapEndpoints(WebApplication app)
    {
        var v1 = app.MapGroup("/v1");

        v1.MapGet("/health", async (IChatProvider chat, IVectorStore vectorStore, ResourceProfile profile, CancellationToken ct) =>
        {
            var caps = await chat.GetCapabilitiesAsync(ct);
            var checks = new List<HealthCheck> { new("service", "ok") };

            if (vectorStore is QdrantVectorStore qdrant)
            {
                try
                {
                    checks.Add(new("qdrant", "ok", await qdrant.PingAsync(ct)));
                }
                catch (HttpRequestException ex)
                {
                    checks.Add(new("qdrant", "degraded", "unreachable; retrieval falls back to lexical search (" + ex.Message + ")"));
                }
            }

            if (!caps.Reachable)
            {
                checks.Add(new("ollama", "unavailable", caps.Error));
            }
            else
            {
                checks.Add(new("ollama", "ok", caps.Version));
                checks.Add(HasModel(caps, profile.ChatModel)
                    ? new("chatModel", "ok", profile.ChatModel)
                    : new("chatModel", "missing", "model not pulled"));
                checks.Add(HasModel(caps, profile.EmbeddingModel)
                    ? new("embeddingModel", "ok", profile.EmbeddingModel)
                    : new("embeddingModel", "missing", "model not pulled"));
            }

            var status = checks.Any(c => c.Status == "unavailable") ? "unavailable"
                : checks.Any(c => c.Status is "missing" or "degraded") ? "degraded"
                : "ok";
            return Results.Ok(new { status, checks });
        });

        v1.MapPost("/pair", (PairRequest? body, PairingService pairing) =>
        {
            var result = pairing.TryPair(body?.Code);
            if (result.Success)
            {
                return Results.Ok(new { token = result.Token, expiresAt = result.ExpiresAt });
            }

            return result.CodeExpiredOrLocked
                ? ApiErrors.Error(StatusCodes.Status403Forbidden, "pairing_code_expired", "Pairing code expired or locked. Use the newest code printed by the service.")
                : ApiErrors.Error(StatusCodes.Status401Unauthorized, "pairing_failed", "Invalid pairing code. Codes are single-use; enter the newest code printed by the service.");
        });

        v1.MapGet("/status", async (IChatProvider chat, ResourceProfile profile, AiOptions options, CancellationToken ct) =>
        {
            var caps = await chat.GetCapabilitiesAsync(ct);
            return Results.Ok(new
            {
                profile = profile.Name,
                gpuMode = options.GpuMode.ToString().ToLowerInvariant(),
                numGpu = options.ResolveNumGpu(),
                provider = new { reachable = caps.Reachable, version = caps.Version, models = caps.Models },
                chatModel = profile.ChatModel,
                embeddingModel = profile.EmbeddingModel,
                service = typeof(AiApp).Assembly.GetName().Version?.ToString(),
            });
        });

        v1.MapGet("/profile", (ResourceProfile profile) => Results.Ok(profile));

        v1.MapGet("/capacity", async (IHardwareProbe probe, CancellationToken ct) =>
        {
            var hw = await probe.ProbeAsync(ct);
            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            return Results.Ok(new
            {
                logicalCores = hw.LogicalCores,
                totalRamBytes = hw.TotalRamBytes,
                freeDiskBytes = hw.FreeDiskBytes,
                serviceWorkingSetBytes = proc.WorkingSet64,
                os = hw.OsDescription,
                gpu = hw.GpuName,
            });
        });

        v1.MapPost("/ask", async (HttpContext http, AskRequest? body, AskService ask) =>
        {
            if (string.IsNullOrWhiteSpace(body?.Question))
            {
                await ApiErrors.WriteAsync(http, StatusCodes.Status400BadRequest, "invalid_request", "'question' is required.");
                return;
            }

            if (body.Question.Length > AskService.MaxQuestionChars)
            {
                await ApiErrors.WriteAsync(http, StatusCodes.Status413PayloadTooLarge, "question_too_long", $"'question' exceeds {AskService.MaxQuestionChars} characters.");
                return;
            }

            await Sse.WriteAsync(http.Response, ask.AskAsync(body.Question, body.UseKnowledge ?? true, http.RequestAborted), http.RequestAborted);
        });
        v1.MapPost("/kb/documents", async (HttpContext http, KbDocumentRequest? body, KnowledgeService kb) =>
        {
            var problem = KnowledgeService.Validate(body?.Title, body?.Source, body?.Text);
            if (problem is not null)
            {
                var tooLong = problem.Contains("exceeds");
                await ApiErrors.WriteAsync(http, tooLong ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest,
                    tooLong ? "document_too_large" : "invalid_request", problem);
                return;
            }

            var result = await kb.IngestAsync(body!.Title!, body.Source, body.Text!, http.RequestAborted);
            await Results.Json(result, statusCode: StatusCodes.Status201Created).ExecuteAsync(http);
        });

        v1.MapGet("/kb/documents", async (KnowledgeService kb, CancellationToken ct) =>
            Results.Ok(new { documents = await kb.ListAsync(ct), chunks = await kb.CountChunksAsync(ct) }));

        v1.MapDelete("/kb/documents/{id}", async (string id, KnowledgeService kb, CancellationToken ct) =>
            await kb.DeleteAsync(id, ct)
                ? Results.NoContent()
                : ApiErrors.Error(StatusCodes.Status404NotFound, "not_found", "Document not found."));

        MapCodeWorkflow(v1, "/review", (svc, req, ct) => svc.ReviewAsync(req, ct));
        MapCodeWorkflow(v1, "/debug", (svc, req, ct) => svc.DebugAsync(req, ct));
        MapCodeWorkflow(v1, "/tests", (svc, req, ct) => svc.TestsAsync(req, ct));
        MapAgent(v1);
    }

    private static void MapAgent(RouteGroupBuilder v1)
    {
        v1.MapPost("/agent/runs", async (HttpContext http, AgentRunRequest? body, AgentRunService agent, ResourceProfile profile) =>
        {
            var problem = AgentRunService.Validate(body, profile);
            if (problem is not null)
            {
                await ApiErrors.WriteAsync(http, StatusCodes.Status400BadRequest, "invalid_request", problem);
                return;
            }

            var run = agent.Start(body!);
            if (run is null)
            {
                await ApiErrors.WriteAsync(http, StatusCodes.Status409Conflict, "agent_busy", "Another agent run is active. Stop it or wait for it to finish.");
                return;
            }

            http.Response.StatusCode = StatusCodes.Status202Accepted;
            await http.Response.WriteAsJsonAsync(new { runId = run.Id }, http.RequestAborted);
        });

        v1.MapGet("/agent/runs/{id}/events", async (HttpContext http, string id, AgentRunService agent) =>
        {
            var run = agent.Get(id);
            if (run is null)
            {
                await ApiErrors.WriteAsync(http, StatusCodes.Status404NotFound, "not_found", "Unknown run.");
                return;
            }

            await Sse.WriteAsync(http.Response, run.ReadAsync(http.RequestAborted), http.RequestAborted);
        });

        v1.MapPost("/agent/runs/{id}/stop", (string id, AgentRunService agent) =>
        {
            var run = agent.Get(id);
            if (run is null)
            {
                return ApiErrors.Error(StatusCodes.Status404NotFound, "not_found", "Unknown run.");
            }

            run.Stop();
            return Results.Accepted();
        });
    }

    private static void MapCodeWorkflow(
        RouteGroupBuilder group, string path, Func<CodeWorkflowService, CodeRequest, CancellationToken, IAsyncEnumerable<SseEvent>> run)
    {
        group.MapPost(path, async (HttpContext http, CodeRequest? body, CodeWorkflowService svc) =>
        {
            var problem = CodeWorkflowService.Validate(body);
            if (problem is not null)
            {
                var tooLong = problem.Contains("exceeds");
                await ApiErrors.WriteAsync(
                    http,
                    tooLong ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest,
                    tooLong ? "source_too_long" : "invalid_request",
                    problem);
                return;
            }

            await Sse.WriteAsync(http.Response, run(svc, body!, http.RequestAborted), http.RequestAborted);
        });
    }

    private static bool HasModel(ProviderCapabilities caps, string model) =>
        caps.Models.Any(m => string.Equals(m.Name, model, StringComparison.OrdinalIgnoreCase)
            || m.Name.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase));
}
