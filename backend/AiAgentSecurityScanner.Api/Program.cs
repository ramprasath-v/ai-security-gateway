using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.Models;
using AiAgentSecurityScanner.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IAiSecurityDetectionService, AiSecurityDetectionService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<ILLMProvider, OllamaLLMProvider>();
builder.Services.AddScoped<ILLMProviderResolver, LLMProviderResolver>();
builder.Services.AddScoped<IPromptNormalizer, PromptNormalizer>();
builder.Services.AddScoped<ISemanticCacheService, SemanticCacheService>();
builder.Services.AddScoped<IChatGatewayService, ChatGatewayService>();
builder.Services.Configure<LLMProviderOptions>(builder.Configuration.GetSection(LLMProviderOptions.SectionName));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.SectionName));
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
    .WithOrigins(
        "http://localhost:4300",
        "http://127.0.0.1:4300",
        "http://localhost:4200",
        "http://127.0.0.1:4200")
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
await EnsureSecurityPoliciesAsync(db);
await EnsureApplicationsAsync(db);

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
        ["LlmTokensUsed"] = "INTEGER NOT NULL DEFAULT 0",
        ["TokensSaved"] = "INTEGER NOT NULL DEFAULT 0",
        ["CachedTokensServed"] = "INTEGER NOT NULL DEFAULT 0",
        ["CacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["ExactCacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["SemanticCacheHit"] = "INTEGER NOT NULL DEFAULT 0",
        ["SimilarityScore"] = "REAL NULL",
        ["CacheKey"] = "TEXT NULL",
        ["MatchedCachePrompt"] = "TEXT NULL",
        ["WasBlockedBeforeModelCall"] = "INTEGER NOT NULL DEFAULT 0",
        ["ApplicationId"] = "TEXT NULL",
        ["ApplicationKey"] = "TEXT NULL",
        ["ApplicationName"] = "TEXT NULL",
        ["ApplicationOwner"] = "TEXT NULL",
        ["ApplicationEnvironment"] = "TEXT NULL",
        ["Provider"] = "TEXT NOT NULL DEFAULT 'Ollama'",
        ["PromptText"] = "TEXT NULL",
        ["ResponseText"] = "TEXT NULL",
        ["RequestStartedAt"] = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'",
        ["RequestCompletedAt"] = "TEXT NULL",
        ["DurationMs"] = "INTEGER NULL",
        ["OllamaDurationMs"] = "INTEGER NULL",
        ["DatabaseDurationMs"] = "INTEGER NULL",
        ["PromptLength"] = "INTEGER NOT NULL DEFAULT 0",
        ["ResponseLength"] = "INTEGER NOT NULL DEFAULT 0",
        ["MatchedPolicyName"] = "TEXT NULL",
        ["ErrorMessage"] = "TEXT NULL"
    };

    foreach (var (name, definition) in columns)
    {
        if (existingColumns.Contains(name)) continue;
        var sql = "ALTER TABLE ChatRequestLogs ADD COLUMN " + name + " " + definition + ";";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}

static async Task EnsureSqliteColumnAsync(AppDbContext db, string tableName, string columnName, string definition)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using (var command = connection.CreateCommand())
    {
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
    }

    await db.Database.ExecuteSqlRawAsync("ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + definition + ";");
}

static async Task EnsureApplicationsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS AiApplications (
            Id TEXT NOT NULL CONSTRAINT PK_AiApplications PRIMARY KEY,
            AppKey TEXT NOT NULL,
            Name TEXT NOT NULL,
            Owner TEXT NOT NULL,
            Environment TEXT NOT NULL,
            Description TEXT NOT NULL,
            Enabled INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);

    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_AiApplications_AppKey ON AiApplications (AppKey);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_AiApplications_Enabled ON AiApplications (Enabled);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_ApplicationId ON ChatRequestLogs (ApplicationId);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatRequestLogs_ApplicationName ON ChatRequestLogs (ApplicationName);");

    var now = DateTime.UtcNow;
    var seedApplications = new[]
    {
        new AiApplication
        {
            AppKey = "security-gateway-chat",
            Name = "Security Gateway Chat",
            Owner = "IT",
            Environment = "Dev",
            Description = "Built-in Security Gateway chat UI.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "rag-app",
            Name = "RagApp",
            Owner = "Knowledge Platform",
            Environment = "Dev",
            Description = "Secure RAG assistant client application.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "sample-app",
            Name = "Sample App",
            Owner = "Demo",
            Environment = "Test",
            Description = "Sample external client for testing application controls.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }
    };

    foreach (var seedApplication in seedApplications)
    {
        if (await db.AiApplications.AnyAsync(x => x.AppKey == seedApplication.AppKey)) continue;
        db.AiApplications.Add(seedApplication);
    }

    await db.SaveChangesAsync();

    var oldSeedAppKeys = new[]
    {
        "ai-chat-portal",
        "hr-assistant",
        "service-desk-agent",
        "code-assistant",
        "finance-assistant",
        "customer-support-bot"
    };
    var oldSeedApps = await db.AiApplications
        .Where(x => oldSeedAppKeys.Contains(x.AppKey))
        .ToListAsync();
    if (oldSeedApps.Count > 0)
    {
        db.AiApplications.RemoveRange(oldSeedApps);
        await db.SaveChangesAsync();
    }
}

static async Task EnsureSecurityPoliciesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS SecurityPolicies (
            Id TEXT NOT NULL CONSTRAINT PK_SecurityPolicies PRIMARY KEY,
            Name TEXT NOT NULL,
            Description TEXT NOT NULL,
            FindingType TEXT NOT NULL,
            Action TEXT NOT NULL,
            RiskScore INTEGER NOT NULL,
            Enabled INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);

    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SecurityPolicies_FindingType ON SecurityPolicies (FindingType);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SecurityPolicies_Enabled ON SecurityPolicies (Enabled);");

    if (await db.SecurityPolicies.AnyAsync()) return;

    var now = DateTime.UtcNow;
    db.SecurityPolicies.AddRange(
        new SecurityPolicy
        {
            Name = "Block SSN",
            Description = "Block prompts containing Social Security numbers.",
            FindingType = "SSN",
            Action = "Block",
            RiskScore = 90,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new SecurityPolicy
        {
            Name = "Block API Keys",
            Description = "Block prompts containing API keys or credential-like secrets.",
            FindingType = "API Key",
            Action = "Block",
            RiskScore = 95,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new SecurityPolicy
        {
            Name = "Mask Email",
            Description = "Mask email addresses before prompts are sent to the model.",
            FindingType = "Email",
            Action = "Mask",
            RiskScore = 30,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new SecurityPolicy
        {
            Name = "Mask Phone",
            Description = "Mask phone numbers before prompts are sent to the model.",
            FindingType = "Phone",
            Action = "Mask",
            RiskScore = 35,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    await db.SaveChangesAsync();
}

static async Task EnsureChatTablesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS ChatSessions (
            Id TEXT NOT NULL CONSTRAINT PK_ChatSessions PRIMARY KEY,
            Title TEXT NOT NULL,
            ApplicationId TEXT NULL,
            ApplicationKey TEXT NULL,
            ApplicationName TEXT NULL,
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
    await EnsureSqliteColumnAsync(db, "ChatSessions", "ApplicationId", "TEXT NULL");
    await EnsureSqliteColumnAsync(db, "ChatSessions", "ApplicationKey", "TEXT NULL");
    await EnsureSqliteColumnAsync(db, "ChatSessions", "ApplicationName", "TEXT NULL");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_ChatSessions_ApplicationKey_UpdatedAtUtc ON ChatSessions (ApplicationKey, UpdatedAtUtc);");
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
