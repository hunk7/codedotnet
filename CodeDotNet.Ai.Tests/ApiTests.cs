using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Providers;
using CodeDotNet.Ai.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeDotNet.Ai.Tests;

public sealed class AiServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "codedotnet-ai-tests-" + Guid.NewGuid().ToString("N"));

    public AiServer(FakeProvider? provider = null, Action<IServiceCollection>? extra = null)
    {
        Provider = provider ?? new FakeProvider(_ => "hello from fake");
        _app = AiApp.Build(
            ["--environment=Testing", "--urls=http://127.0.0.1:0", "--Ai:DataDirectory=" + _dataDir],
            services =>
            {
                services.AddSingleton<IChatProvider>(Provider);
                services.AddSingleton<IEmbeddingProvider>(Provider);
                extra?.Invoke(services);
            });
        _app.StartAsync().GetAwaiter().GetResult();
        BaseUrl = _app.Urls.First();
        Pairing = _app.Services.GetRequiredService<PairingService>();
    }

    public FakeProvider Provider { get; }

    public PairingService Pairing { get; }

    public IServiceProvider Services => _app.Services;

    public string BaseUrl { get; }

    public HttpClient CreateClient() => new() { BaseAddress = new Uri(BaseUrl) };

    public async Task<HttpClient> CreatePairedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/v1/pair", new { code = Pairing.CurrentCode });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDir, true); } catch (IOException) { }
    }
}

public class ApiSecurityTests
{
    [Fact]
    public async Task Health_IsAnonymous()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var response = await client.GetAsync("/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("degraded", json.GetProperty("status").GetString());
        Assert.Contains(json.GetProperty("checks").EnumerateArray(), c => c.GetProperty("name").GetString() == "chatModel" && c.GetProperty("status").GetString() == "missing");
    }

    [Fact]
    public async Task Health_AllModelsPresent_IsOk()
    {
        var models = new[] { Resources.Profiles.Low, Resources.Profiles.Standard }.SelectMany(p => new[] { p.ChatModel, p.EmbeddingModel }).Distinct().ToArray();
        await using var server = new AiServer(new FakeProvider(_ => "x", models));
        using var client = server.CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/v1/health");
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/v1/status")]
    [InlineData("/v1/profile")]
    [InlineData("/v1/capacity")]
    public async Task ProtectedEndpoints_RequireToken(string path)
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unauthorized", json.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ask_WithoutToken_IsRejected()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/ask", new { question = "hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(server.Provider.Requests);
    }

    [Fact]
    public async Task Pair_WrongCode_IsRejected()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/pair", new { code = "WRONG123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pair_CodeIsSingleUse()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var code = server.Pairing.CurrentCode;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/pair", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/v1/pair", new { code })).StatusCode);
        Assert.NotEqual(code, server.Pairing.CurrentCode);
    }

    [Fact]
    public async Task Pair_LocksAfterTooManyFailures()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        var original = server.Pairing.CurrentCode;
        HttpStatusCode last = default;
        for (var i = 0; i < PairingService.MaxFailedAttempts; i++)
        {
            last = (await client.PostAsJsonAsync("/v1/pair", new { code = "BADBADXX" })).StatusCode;
        }

        Assert.Equal(HttpStatusCode.Forbidden, last);
        Assert.NotEqual(original, server.Pairing.CurrentCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/v1/pair", new { code = original })).StatusCode);
    }

    [Fact]
    public async Task DisallowedOrigin_IsRejected_EvenWithValidToken()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/status");
        request.Headers.Add("Origin", "https://evil.example");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("origin_not_allowed", json.GetProperty("error").GetProperty("code").GetString());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task LoopbackOrigin_IsAllowed_AndGetsCorsHeader()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/profile");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_IsRejected()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/ask");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonLoopbackHostHeader_IsRejected()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Host = "evil.example";
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BogusBearerToken_IsRejected()
    {
        await using var server = new AiServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/status")).StatusCode);
    }
}

public class ApiBehaviorTests
{
    [Fact]
    public async Task Profile_ReturnsExplicitLimits()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var json = await client.GetFromJsonAsync<JsonElement>("/v1/profile");
        Assert.True(json.GetProperty("numCtx").GetInt32() >= 4096);
        Assert.False(json.GetProperty("llmRerankerEnabled").GetBoolean());
    }

    [Fact]
    public async Task Status_ReportsCpuGpuPolicy()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var json = await client.GetFromJsonAsync<JsonElement>("/v1/status");
        Assert.Equal("auto", json.GetProperty("gpuMode").GetString());
        Assert.Equal(0, json.GetProperty("numGpu").GetInt32());
        Assert.True(json.GetProperty("provider").GetProperty("reachable").GetBoolean());
    }

    [Fact]
    public async Task Capacity_ReportsHardware()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var json = await client.GetFromJsonAsync<JsonElement>("/v1/capacity");
        Assert.True(json.GetProperty("logicalCores").GetInt32() > 0);
        Assert.True(json.GetProperty("totalRamBytes").GetInt64() > 0);
    }

    [Fact]
    public async Task Ask_StreamsTokensThenDone_AndSetsExplicitModelOptions()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var response = await client.PostAsJsonAsync("/v1/ask", new { question = "what is a span?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: token", body);
        Assert.Contains("hello", body);
        Assert.Contains("event: done", body);
        Assert.Contains("\"grounded\":false", body);

        var request = Assert.Single(server.Provider.Requests);
        Assert.Equal(0, request.Options.NumGpu);
        Assert.True(request.Options.NumCtx > 0);
        Assert.True(request.Options.NumPredict > 0);
        Assert.False(string.IsNullOrEmpty(request.Options.KeepAlive));
        Assert.Equal("what is a span?", request.Messages[^1].Content);
    }

    [Fact]
    public async Task Ask_MissingQuestion_Is400()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var response = await client.PostAsJsonAsync("/v1/ask", new { question = " " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_QuestionTooLong_Is413()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var response = await client.PostAsJsonAsync("/v1/ask", new { question = new string('x', 9000) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Ask_OversizedBody_IsRejectedByKestrelLimit()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        using var content = new StringContent("{\"question\":\"" + new string('x', 300_000) + "\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/v1/ask", content);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
        Assert.Empty(server.Provider.Requests);
    }

    [Fact]
    public async Task Ask_ProviderFailure_EmitsErrorEventAndDone()
    {
        var failing = new FakeProvider(_ => throw new InvalidOperationException("boom"));
        await using var server = new AiServer(failing);
        using var client = await server.CreatePairedClientAsync();
        var response = await client.PostAsJsonAsync("/v1/ask", new { question = "x" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: error", body);
        Assert.Contains("provider_error", body);
        Assert.Contains("event: done", body);
        Assert.Contains("\"outcome\":\"error\"", body);
    }
}

public class PairingServiceTests
{
    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static PairingService Create(FakeTime time) =>
        new(time, Microsoft.Extensions.Logging.Abstractions.NullLogger<PairingService>.Instance);

    [Fact]
    public void Code_ExpiresAfterLifetime()
    {
        var time = new FakeTime();
        var svc = Create(time);
        var code = svc.CurrentCode;
        time.Advance(PairingService.CodeLifetime + TimeSpan.FromSeconds(1));
        var result = svc.TryPair(code);
        Assert.False(result.Success);
        Assert.True(result.CodeExpiredOrLocked);
    }

    [Fact]
    public void Token_ExpiresAfterSessionLifetime()
    {
        var time = new FakeTime();
        var svc = Create(time);
        var result = svc.TryPair(svc.CurrentCode);
        Assert.True(result.Success);
        Assert.True(svc.ValidateToken(result.Token));
        time.Advance(PairingService.SessionLifetime + TimeSpan.FromSeconds(1));
        Assert.False(svc.ValidateToken(result.Token));
    }

    [Fact]
    public void Code_IsCaseAndWhitespaceInsensitive()
    {
        var svc = Create(new FakeTime());
        var result = svc.TryPair("  " + svc.CurrentCode.ToLowerInvariant() + " ");
        Assert.True(result.Success);
    }

    [Fact]
    public void CodeLifetime_IsAtLeastThirtyMinutes()
    {
        Assert.True(PairingService.CodeLifetime >= TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Code_IsStillValidJustBeforeLifetimeEnds()
    {
        var time = new FakeTime();
        var svc = Create(time);
        var code = svc.CurrentCode;
        time.Advance(TimeSpan.FromMinutes(29));
        Assert.True(svc.TryPair(code).Success);
    }

    [Fact]
    public void Code_ChangesAfterSuccessfulPairing()
    {
        var svc = Create(new FakeTime());
        var first = svc.CurrentCode;
        Assert.True(svc.TryPair(first).Success);
        var second = svc.CurrentCode;
        Assert.NotEqual(first, second);
        Assert.False(svc.TryPair(first).Success);
        Assert.True(svc.TryPair(second).Success);
    }

    [Fact]
    public void RotateIfExpired_OnlyRotatesAfterExpiry()
    {
        var time = new FakeTime();
        var svc = Create(time);
        var code = svc.CurrentCode;
        Assert.False(svc.RotateIfExpired());
        Assert.Equal(code, svc.CurrentCode);

        time.Advance(PairingService.CodeLifetime + TimeSpan.FromSeconds(1));
        Assert.True(svc.RotateIfExpired());
        Assert.NotEqual(code, svc.CurrentCode);
        Assert.False(svc.TryPair(code).Success);
    }

    [Fact]
    public void Token_SurvivesCodeRotation()
    {
        var svc = Create(new FakeTime());
        var result = svc.TryPair(svc.CurrentCode);
        svc.Rotate("test");
        Assert.True(svc.ValidateToken(result.Token));
    }

    [Fact]
    public void Rotate_InvalidatesOldCode()
    {
        var svc = Create(new FakeTime());
        var old = svc.CurrentCode;
        svc.Rotate("test");
        Assert.NotEqual(old, svc.CurrentCode);
        Assert.False(svc.TryPair(old).Success);
    }

    [Fact]
    public void Sessions_AreCapped()
    {
        var svc = Create(new FakeTime());
        for (var i = 0; i < PairingService.MaxSessions + 4; i++)
        {
            Assert.True(svc.TryPair(svc.CurrentCode).Success);
        }

        Assert.Equal(PairingService.MaxSessions, svc.ActiveSessionCount);
    }

    [Fact]
    public void Null_Candidate_Fails()
    {
        var svc = Create(new FakeTime());
        Assert.False(svc.TryPair(null).Success);
    }
}

public class OriginPolicyTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://127.0.0.1:3000", true)]
    [InlineData("http://[::1]:3000", true)]
    [InlineData("https://evil.example", false)]
    [InlineData("https://localhost.evil.example", false)]
    [InlineData("null", false)]
    [InlineData("ftp://localhost", false)]
    public void IsOriginAllowed_DefaultPolicy(string? origin, bool expected) =>
        Assert.Equal(expected, OriginPolicy.IsOriginAllowed(origin, []));

    [Fact]
    public void IsOriginAllowed_ExplicitList() =>
        Assert.True(OriginPolicy.IsOriginAllowed("https://hunk7.github.io", ["https://hunk7.github.io"]));

    [Theory]
    [InlineData("127.0.0.1:7901", true)]
    [InlineData("localhost:7901", true)]
    [InlineData("[::1]:7901", true)]
    [InlineData("localhost", true)]
    [InlineData("evil.example", false)]
    [InlineData("127.0.0.1.evil.example:7901", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsHostAllowed(string? host, bool expected) =>
        Assert.Equal(expected, OriginPolicy.IsHostAllowed(host));
}
