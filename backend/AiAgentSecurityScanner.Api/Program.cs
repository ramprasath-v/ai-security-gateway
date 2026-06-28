using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IAiSecurityDetectionService, AiSecurityDetectionService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IPromptNormalizer, PromptNormalizer>();
builder.Services.AddScoped<IOllamaEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<ISemanticCacheService, SemanticCacheService>();
builder.Services.AddScoped<IChatGatewayService, ChatGatewayService>();
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<AiSecurityPolicyOptions>(builder.Configuration.GetSection(AiSecurityPolicyOptions.SectionName));
builder.Services.Configure<SemanticCacheOptions>(builder.Configuration.GetSection(SemanticCacheOptions.SectionName));
builder.Services.AddHttpClient("Ollama", client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 60;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("Frontend");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();
await EnsureSqliteColumnsAsync(db);
await EnsureChatTablesAsync(db);

app.Run();

static async Task EnsureSqliteColumnsAsync(AppDbContext db)
{
    var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    await using (var command = connection.CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('ChatRequestLogs');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            existingColumns.Add(reader.GetString(1));
    }

    var columns = new Dictionary<string, string>
    {
        ["RequestStartedAtUtc"] = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'",
        ["RequestCompletedAtUtc"] = "TEXT NULL",
        ["TotalDurationMs"] = "INTEGER NULL",
        ["PolicyCheckDurationMs"] = "INTEGER NULL",
        ["PromptTokens"] = "INTEGER NULL",
        ["ResponseTokens"] = "INTEGER NULL",
        ["TotalTokens"] = "INTEGER NULL",
        ["CacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["ExactCacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["SemanticCacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["SimilarityScore"] = "REAL NULL",
        ["CacheKey"] = "TEXT NULL",
        ["WasBlockedBeforeModelCall"] = "INTEGER NOT NULL DEFAULT 0",
        ["RequestStartedAt"] = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'",
        ["RequestCompletedAt"] = "TEXT NULL",
        ["DurationMs"] = "INTEGER NULL",
        ["OllamaDurationMs"] = "INTEGER NULL",
        ["DatabaseDurationMs"] = "INTEGER NULL",
        ["PromptLength"] = "INTEGER NOT NULL DEFAULT 0",
        ["ResponseLength"] = "INTEGER NOT NULL DEFAULT 0",
        ["ErrorMessage"] = "TEXT NULL"
    };

    foreach (var (name, definition) in columns)
    {
        if (existingColumns.Contains(name)) continue;
        var sql = "ALTER TABLE ChatRequestLogs ADD COLUMN " + name + " " + definition + ";";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}

static async Task EnsureChatTablesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS ChatSessions (
            Id TEXT NOT NULL CONSTRAINT PK_ChatSessions PRIMARY KEY,
            Title TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS ChatMessages (
            Id TEXT NOT NULL CONSTRAINT PK_ChatMessages PRIMARY KEY,
            SessionId TEXT NOT NULL,
            Role TEXT NOT NULL,
            Content TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            Model TEXT NULL,
            PolicyDecision TEXT NULL,
            RiskScore INTEGER NULL,
            RequestId TEXT NULL,
            CONSTRAINT FK_ChatMessages_ChatSessions_SessionId FOREIGN KEY (SessionId) REFERENCES ChatSessions (Id) ON DELETE CASCADE
        );
        """);

    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatSessions_UpdatedAtUtc ON ChatSessions (UpdatedAtUtc);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatMessages_SessionId_CreatedAtUtc ON ChatMessages (SessionId, CreatedAtUtc);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_CacheKey ON ChatRequestLogs (CacheKey);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_CacheHit ON ChatRequestLogs (CacheHit);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_ExactCacheHit ON ChatRequestLogs (ExactCacheHit);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_SemanticCacheHit ON ChatRequestLogs (SemanticCacheHit);");

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS SemanticCacheEntries (
            Id TEXT NOT NULL CONSTRAINT PK_SemanticCacheEntries PRIMARY KEY,
            OriginalPrompt TEXT NOT NULL,
            NormalizedPrompt TEXT NOT NULL,
            EmbeddingJson TEXT NOT NULL,
            CachedResponse TEXT NOT NULL,
            Model TEXT NOT NULL,
            PolicyDecision TEXT NOT NULL,
            PromptTokens INTEGER NULL,
            ResponseTokens INTEGER NULL,
            TotalTokens INTEGER NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);

    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SemanticCacheEntries_Model ON SemanticCacheEntries (Model);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SemanticCacheEntries_PolicyDecision ON SemanticCacheEntries (PolicyDecision);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SemanticCacheEntries_NormalizedPrompt ON SemanticCacheEntries (NormalizedPrompt);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SemanticCacheEntries_CreatedAtUtc ON SemanticCacheEntries (CreatedAtUtc);");
}

public partial class Program;
