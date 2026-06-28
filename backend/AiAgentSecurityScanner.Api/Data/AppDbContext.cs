using AiAgentSecurityScanner.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ChatRequestLog> ChatRequestLogs => Set<ChatRequestLog>();
    public DbSet<SensitiveDataFinding> SensitiveDataFindings => Set<SensitiveDataFinding>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SemanticCacheEntry> SemanticCacheEntries => Set<SemanticCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.TimestampUtc);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.Model);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.PolicyAction);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.CacheKey);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.CacheHit);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.ExactCacheHit);
        modelBuilder.Entity<ChatRequestLog>().HasIndex(x => x.SemanticCacheHit);
        modelBuilder.Entity<ChatRequestLog>().Property(x => x.PromptPreview).HasMaxLength(1000);
        modelBuilder.Entity<ChatRequestLog>().Property(x => x.ResponsePreview).HasMaxLength(1000);
        modelBuilder.Entity<SensitiveDataFinding>()
            .HasOne(x => x.ChatRequestLog)
            .WithMany(x => x.Findings)
            .HasForeignKey(x => x.ChatRequestLogId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChatSession>().HasIndex(x => x.UpdatedAtUtc);
        modelBuilder.Entity<ChatMessage>().HasIndex(x => new { x.SessionId, x.CreatedAtUtc });
        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SemanticCacheEntry>().HasIndex(x => x.Model);
        modelBuilder.Entity<SemanticCacheEntry>().HasIndex(x => x.PolicyDecision);
        modelBuilder.Entity<SemanticCacheEntry>().HasIndex(x => x.NormalizedPrompt);
        modelBuilder.Entity<SemanticCacheEntry>().HasIndex(x => x.CreatedAtUtc);
    }
}
