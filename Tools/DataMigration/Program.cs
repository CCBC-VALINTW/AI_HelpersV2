// One-time migration tool: copies core reference/config data (LlmDefinitions,
// HelperCategories, Stylesheets, HelperDefinitions) from the V1 govservice
// database into the new V2 AI_Helpers database. Read-only against the source;
// idempotent against the target (rows already present by Id are skipped).
//
// Usage:
//   dotnet run -- --source "<connection string>" --target "<connection string>" [--dry-run]

using AiHelpers.Data;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var options = ParseArgs(args);
if (options is null) return 1;

var dryRun = options.Value.DryRun;

await using var target = new AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(options.Value.TargetConnectionString).Options);

await using var source = new SqlConnection(options.Value.SourceConnectionString);
await source.OpenAsync();

Console.WriteLine($"Connected. Source: {source.DataSource}/{source.Database}  Target: {target.Database.GetDbConnection().DataSource}");
if (dryRun) Console.WriteLine("Dry run - no changes will be written.");

await MigrateLlmDefinitionsAsync(source, target, dryRun);
await MigrateHelperCategoriesAsync(source, target, dryRun);
await MigrateStylesheetsAsync(source, target, dryRun);
await MigrateHelperDefinitionsAsync(source, target, dryRun);

Console.WriteLine("Done.");
return 0;

static (string SourceConnectionString, string TargetConnectionString, bool DryRun)? ParseArgs(string[] args)
{
    string? sourceConn = null, targetConn = null;
    var dryRun = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--source" when i + 1 < args.Length:
                sourceConn = args[++i];
                break;
            case "--target" when i + 1 < args.Length:
                targetConn = args[++i];
                break;
            case "--dry-run":
                dryRun = true;
                break;
        }
    }

    if (sourceConn is null || targetConn is null)
    {
        Console.Error.WriteLine("Usage: dotnet run -- --source \"<connection string>\" --target \"<connection string>\" [--dry-run]");
        return null;
    }

    return (sourceConn, targetConn, dryRun);
}

static async Task MigrateLlmDefinitionsAsync(SqlConnection source, AppDbContext target, bool dryRun)
{
    var existing = (await target.LlmDefinitions.Select(l => l.Id).ToListAsync()).ToHashSet();

    await using var cmd = new SqlCommand(@"
        SELECT PK_ID, LLM_Identifier, LLM_Name, LLM_Desc, maxTokens, defTopP, defTemp,
               Text, Doc, Image, InTokCost, OutTokCost, Reasoning, RsnTks, Residency
        FROM dbo.AI_LLMDefinitions", source);
    await using var reader = await cmd.ExecuteReaderAsync();

    var added = 0;
    while (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        if (existing.Contains(id)) continue;

        target.LlmDefinitions.Add(new LlmDefinition
        {
            Id = id,
            Identifier = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.GetString(3),
            MaxTokens = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            DefaultTopP = reader.GetDecimal(5),
            DefaultTemperature = reader.GetDecimal(6),
            SupportsText = !reader.IsDBNull(7) && reader.GetBoolean(7),
            SupportsDocument = !reader.IsDBNull(8) && reader.GetBoolean(8),
            SupportsImage = !reader.IsDBNull(9) && reader.GetBoolean(9),
            InputTokenCost = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            OutputTokenCost = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
            SupportsReasoning = !reader.IsDBNull(12) && reader.GetBoolean(12),
            ReasoningTokens = reader.IsDBNull(13) ? null : reader.GetInt32(13),
            Residency = MapResidency(reader.IsDBNull(14) ? "GL" : reader.GetString(14))
        });
        added++;
    }
    await reader.CloseAsync();

    if (!dryRun) await SaveWithIdentityInsertAsync(target, "LlmDefinitions");
    Console.WriteLine($"LlmDefinitions: {added} added, {existing.Count} already present.");
}

static async Task MigrateHelperCategoriesAsync(SqlConnection source, AppDbContext target, bool dryRun)
{
    var existing = (await target.HelperCategories.Select(c => c.Id).ToListAsync()).ToHashSet();

    await using var cmd = new SqlCommand("SELECT PK_ID, CatName, CatDesc FROM dbo.AI_HelperCategories", source);
    await using var reader = await cmd.ExecuteReaderAsync();

    var added = 0;
    while (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        if (existing.Contains(id)) continue;

        target.HelperCategories.Add(new HelperCategory
        {
            Id = id,
            Name = reader.IsDBNull(1) ? $"Category {id}" : reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2)
        });
        added++;
    }
    await reader.CloseAsync();

    if (!dryRun) await SaveWithIdentityInsertAsync(target, "HelperCategories");
    Console.WriteLine($"HelperCategories: {added} added, {existing.Count} already present.");
}

static async Task MigrateStylesheetsAsync(SqlConnection source, AppDbContext target, bool dryRun)
{
    var existing = (await target.Stylesheets.Select(s => s.Id).ToListAsync()).ToHashSet();

    await using var cmd = new SqlCommand("SELECT PK_ID, StylesheetName, Stylesheet, StyleInstructions FROM dbo.AI_Stylesheets", source);
    await using var reader = await cmd.ExecuteReaderAsync();

    var added = 0;
    while (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        if (existing.Contains(id)) continue;

        target.Stylesheets.Add(new Stylesheet
        {
            Id = id,
            Name = reader.IsDBNull(1) ? $"Stylesheet {id}" : reader.GetString(1),
            Css = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            StyleInstructions = reader.IsDBNull(3) ? null : reader.GetString(3)
        });
        added++;
    }
    await reader.CloseAsync();

    if (!dryRun) await SaveWithIdentityInsertAsync(target, "Stylesheets");
    Console.WriteLine($"Stylesheets: {added} added, {existing.Count} already present.");
}

static async Task MigrateHelperDefinitionsAsync(SqlConnection source, AppDbContext target, bool dryRun)
{
    var existing = (await target.HelperDefinitions.Select(h => h.Id).ToListAsync()).ToHashSet();
    var validLlmIds = (await target.LlmDefinitions.Select(l => l.Id).ToListAsync()).ToHashSet();
    var validCategoryIds = (await target.HelperCategories.Select(c => c.Id).ToListAsync()).ToHashSet();
    var validStyleIds = (await target.Stylesheets.Select(s => s.Id).ToListAsync()).ToHashSet();

    await using var cmd = new SqlCommand(@"
        SELECT PK_ID, HelperName, HelperDesc, LLMID, temp, TopP, TempAdjustAllow, TopPAdjustAllow,
               Prim_Purpose, Methodology, Style_Tone, Outpt_Fmt, Tgt_Aud, Special, Owner, Scope,
               HelperCategory, defStyle, AllowContext, ContextPrompt, Reasoning, RsnTkns,
               Knowledge, KnowledgeData, KnowledgeFT, KnowledgePrompt, IsExternal, ExternalURL
        FROM dbo.AI_HelperDefinitions", source);
    await using var reader = await cmd.ExecuteReaderAsync();

    var added = 0;
    var droppedRefs = 0;
    while (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        if (existing.Contains(id)) continue;

        int? llmId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        if (llmId is not null && !validLlmIds.Contains(llmId.Value)) { llmId = null; droppedRefs++; }

        int? categoryId = reader.IsDBNull(16) ? null : reader.GetInt32(16);
        if (categoryId is not null && (categoryId == 0 || !validCategoryIds.Contains(categoryId.Value))) { categoryId = null; droppedRefs++; }

        int? styleId = reader.IsDBNull(17) ? null : reader.GetInt32(17);
        if (styleId is not null && !validStyleIds.Contains(styleId.Value)) { styleId = null; droppedRefs++; }

        target.HelperDefinitions.Add(new HelperDefinition
        {
            Id = id,
            Name = reader.IsDBNull(1) ? $"Helper {id}" : reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            LlmDefinitionId = llmId,
            Temperature = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
            TopP = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            TemperatureAdjustmentAllowance = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            TopPAdjustmentAllowance = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            PrimaryPurpose = reader.IsDBNull(8) ? null : reader.GetString(8),
            Methodology = reader.IsDBNull(9) ? null : reader.GetString(9),
            StyleTone = reader.IsDBNull(10) ? null : reader.GetString(10),
            OutputFormat = reader.IsDBNull(11) ? null : reader.GetString(11),
            TargetAudience = reader.IsDBNull(12) ? null : reader.GetString(12),
            SpecialInstructions = reader.IsDBNull(13) ? null : reader.GetString(13),
            OwnerEmail = reader.IsDBNull(14) ? null : reader.GetString(14),
            Scope = (!reader.IsDBNull(15) && reader.GetString(15) == "G") ? HelperScope.General : HelperScope.Personal,
            HelperCategoryId = categoryId,
            DefaultStylesheetId = styleId,
            AllowContext = !reader.IsDBNull(18) && reader.GetBoolean(18),
            ContextPrompt = reader.IsDBNull(19) ? null : reader.GetString(19),
            SupportsReasoning = !reader.IsDBNull(20) && reader.GetBoolean(20),
            ReasoningTokens = reader.IsDBNull(21) ? null : reader.GetInt32(21),
            HasKnowledge = !reader.IsDBNull(22) && reader.GetBoolean(22),
            KnowledgeData = reader.IsDBNull(23) ? null : reader.GetString(23),
            KnowledgeFileType = reader.IsDBNull(24) ? null : reader.GetString(24),
            KnowledgePrompt = reader.IsDBNull(25) ? null : reader.GetString(25),
            IsExternal = !reader.IsDBNull(26) && reader.GetBoolean(26),
            ExternalUrl = reader.IsDBNull(27) ? null : reader.GetString(27)
        });
        added++;
    }
    await reader.CloseAsync();

    if (!dryRun) await SaveWithIdentityInsertAsync(target, "HelperDefinitions");
    Console.WriteLine($"HelperDefinitions: {added} added, {existing.Count} already present, {droppedRefs} dangling reference(s) nulled out.");
}

static ModelResidency MapResidency(string code) => code switch
{
    "EU" => ModelResidency.EU,
    "UK" => ModelResidency.UK,
    _ => ModelResidency.Global
};

static async Task SaveWithIdentityInsertAsync(AppDbContext ctx, string tableName)
{
    if (!ctx.ChangeTracker.HasChanges()) return;

    await ctx.Database.OpenConnectionAsync();
    await using var tx = await ctx.Database.BeginTransactionAsync();
    try
    {
        // tableName is always one of our own hardcoded constants, never external input.
#pragma warning disable EF1002
        await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] ON");
        await ctx.SaveChangesAsync();
        await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] OFF");
#pragma warning restore EF1002
        await tx.CommitAsync();
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
