using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<HelperCategory> HelperCategories => Set<HelperCategory>();
    public DbSet<LlmDefinition> LlmDefinitions => Set<LlmDefinition>();
    public DbSet<HelperDefinition> HelperDefinitions => Set<HelperDefinition>();
    public DbSet<Stylesheet> Stylesheets => Set<Stylesheet>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<CallbackEntry> CallbackEntries => Set<CallbackEntry>();
    public DbSet<Feedback> FeedbackEntries => Set<Feedback>();
    public DbSet<PersonalityPrompt> PersonalityPrompts => Set<PersonalityPrompt>();
    public DbSet<SpendCap> SpendCaps => Set<SpendCap>();
    public DbSet<ArticleStoreItem> ArticleStoreItems => Set<ArticleStoreItem>();
    public DbSet<ProviderCredential> ProviderCredentials => Set<ProviderCredential>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HelperCategory>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(128).IsRequired();
            e.Property(p => p.Description).HasMaxLength(4000);
        });

        modelBuilder.Entity<LlmDefinition>(e =>
        {
            e.Property(p => p.Provider).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Identifier).HasMaxLength(128).IsRequired();
            e.Property(p => p.Name).HasMaxLength(128).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2048).IsRequired();
            e.Property(p => p.DefaultAdherence).HasPrecision(8, 5);
            e.Property(p => p.DefaultCreativity).HasPrecision(8, 5);
            e.Property(p => p.SupportsSamplingControl).HasDefaultValue(true);
            e.Property(p => p.InputTokenCost).HasPrecision(8, 5);
            e.Property(p => p.OutputTokenCost).HasPrecision(8, 5);
            e.Property(p => p.Residency).HasConversion<string>().HasMaxLength(10);
            // Not unique: multiple named definitions (e.g. a pinned version vs. a "latest stable"
            // alias) can legitimately point at the same underlying provider model identifier.
            e.HasIndex(p => p.Identifier);
        });

        modelBuilder.Entity<Stylesheet>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<HelperDefinition>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(128).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2048);
            e.Property(p => p.Effort).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.Creativity).HasPrecision(8, 5);
            e.Property(p => p.Adherence).HasPrecision(8, 5);
            e.Property(p => p.CreativityAdjustmentAllowance).HasPrecision(8, 5);
            e.Property(p => p.AdherenceAdjustmentAllowance).HasPrecision(8, 5);
            e.Property(p => p.OwnerEmail).HasMaxLength(256);
            e.Property(p => p.Scope).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.ContextPrompt).HasMaxLength(2048);
            e.Property(p => p.KnowledgeFileType).HasMaxLength(10);
            e.Property(p => p.ExternalUrl).HasMaxLength(512);

            e.HasOne(p => p.LlmDefinition)
                .WithMany(l => l.Helpers)
                .HasForeignKey(p => p.LlmDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.HelperCategory)
                .WithMany(c => c.Helpers)
                .HasForeignKey(p => p.HelperCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.DefaultStylesheet)
                .WithMany(s => s.Helpers)
                .HasForeignKey(p => p.DefaultStylesheetId)
                .OnDelete(DeleteBehavior.SetNull);

            e.ToTable(t => t.HasCheckConstraint(
                "CK_HelperDefinition_ExternalUrl",
                "([IsExternal] = 0 AND [ExternalUrl] IS NULL) OR ([IsExternal] = 1 AND [ExternalUrl] IS NOT NULL)"));
        });

        modelBuilder.Entity<AccountingEntry>(e =>
        {
            e.Property(p => p.UserId).HasMaxLength(256).IsRequired();
            e.Property(p => p.HelperName).HasMaxLength(128);
            e.Property(p => p.Cost).HasPrecision(18, 8);
            e.HasIndex(p => new { p.UserId, p.Timestamp });

            e.HasOne(p => p.HelperDefinition)
                .WithMany(h => h.AccountingEntries)
                .HasForeignKey(p => p.HelperDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CallbackEntry>(e =>
        {
            e.Property(p => p.Initiator).HasMaxLength(256);
            e.Property(p => p.StopReason).HasMaxLength(128);
            e.Property(p => p.HelperName).HasMaxLength(128);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(p => p.CallbackGuid).IsUnique();

            e.HasOne(p => p.HelperDefinition)
                .WithMany(h => h.CallbackEntries)
                .HasForeignKey(p => p.HelperDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Feedback>(e =>
        {
            e.Property(p => p.Email).HasMaxLength(128).IsRequired();
            e.Property(p => p.Successes).HasMaxLength(1024);
            e.Property(p => p.Failures).HasMaxLength(1024);
            e.Property(p => p.Improvements).HasMaxLength(1024);
            e.Property(p => p.OtherComments).HasMaxLength(1024);

            e.HasOne(p => p.HelperDefinition)
                .WithMany(h => h.FeedbackEntries)
                .HasForeignKey(p => p.HelperDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PersonalityPrompt>(e =>
        {
            e.Property(p => p.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<SpendCap>(e =>
        {
            e.Property(p => p.UserId).HasMaxLength(512).IsRequired();
            e.Property(p => p.MonthlyCapAmount).HasPrecision(6, 2);
            e.HasIndex(p => p.UserId).IsUnique();
        });

        modelBuilder.Entity<ArticleStoreItem>(e =>
        {
            e.Property(p => p.Category).HasMaxLength(64);
            e.Property(p => p.Access).HasMaxLength(128);
            e.Property(p => p.Title).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<ProviderCredential>(e =>
        {
            e.Property(p => p.Provider).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Name).HasMaxLength(128).IsRequired();
            e.Property(p => p.CreatedBy).HasMaxLength(256);
            // At most one default credential per provider.
            e.HasIndex(p => p.Provider).IsUnique().HasFilter("[IsDefault] = 1");
        });
    }
}
