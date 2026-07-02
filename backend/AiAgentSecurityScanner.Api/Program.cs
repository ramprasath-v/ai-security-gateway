using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.Models;
using AiAgentSecurityScanner.Api.Services;
using Microsoft.EntityFrameworkCore;

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

    if (await db.AiApplications.AnyAsync()) return;

    var now = DateTime.UtcNow;
    db.AiApplications.AddRange(
        new AiApplication
        {
            AppKey = "ai-chat-portal",
            Name = "AI Chat Portal",
            Owner = "IT",
            Environment = "Dev",
            Description = "Default demo chat application.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "hr-assistant",
            Name = "HR Assistant",
            Owner = "HR",
            Environment = "Test",
            Description = "HR prompt testing application.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "service-desk-agent",
            Name = "Service Desk Agent",
            Owner = "IT Operations",
            Environment = "Dev",
            Description = "Service desk agent test harness.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "code-assistant",
            Name = "Code Assistant",
            Owner = "Engineering",
            Environment = "Dev",
            Description = "Engineering code assistant demo.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "finance-assistant",
            Name = "Finance Assistant",
            Owner = "Finance",
            Environment = "Test",
            Description = "Finance assistant policy demo.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        },
        new AiApplication
        {
            AppKey = "customer-support-bot",
            Name = "Customer Support Bot",
            Owner = "Support",
            Environment = "Dev",
            Description = "Customer support bot demo.",
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    await db.SaveChangesAsync();
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
