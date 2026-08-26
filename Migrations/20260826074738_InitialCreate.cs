using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleStoreItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Access = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ArticleHtml = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleStoreItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelperCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelperCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identifier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    DefaultTopP = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: false),
                    DefaultTemperature = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: false),
                    SupportsText = table.Column<bool>(type: "bit", nullable: false),
                    SupportsDocument = table.Column<bool>(type: "bit", nullable: false),
                    SupportsImage = table.Column<bool>(type: "bit", nullable: false),
                    InputTokenCost = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    OutputTokenCost = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    SupportsReasoning = table.Column<bool>(type: "bit", nullable: false),
                    ReasoningTokens = table.Column<int>(type: "int", nullable: true),
                    Residency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalityPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalityPrompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpendCaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MonthlyCapAmount = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendCaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stylesheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Css = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stylesheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelperDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LlmDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    TopP = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    TemperatureAdjustmentAllowance = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    TopPAdjustmentAllowance = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: true),
                    PrimaryPurpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Methodology = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleTone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetAudience = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HelperCategoryId = table.Column<int>(type: "int", nullable: true),
                    DefaultStylesheetId = table.Column<int>(type: "int", nullable: true),
                    AllowContext = table.Column<bool>(type: "bit", nullable: false),
                    ContextPrompt = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SupportsReasoning = table.Column<bool>(type: "bit", nullable: false),
                    ReasoningTokens = table.Column<int>(type: "int", nullable: true),
                    HasKnowledge = table.Column<bool>(type: "bit", nullable: false),
                    KnowledgeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KnowledgeFileType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    KnowledgePrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsExternal = table.Column<bool>(type: "bit", nullable: false),
                    ExternalUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelperDefinitions", x => x.Id);
                    table.CheckConstraint("CK_HelperDefinition_ExternalUrl", "([IsExternal] = 0 AND [ExternalUrl] IS NULL) OR ([IsExternal] = 1 AND [ExternalUrl] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_HelperDefinitions_HelperCategories_HelperCategoryId",
                        column: x => x.HelperCategoryId,
                        principalTable: "HelperCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HelperDefinitions_LlmDefinitions_LlmDefinitionId",
                        column: x => x.LlmDefinitionId,
                        principalTable: "LlmDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelperDefinitions_Stylesheets_DefaultStylesheetId",
                        column: x => x.DefaultStylesheetId,
                        principalTable: "Stylesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HelperDefinitionId = table.Column<int>(type: "int", nullable: true),
                    HelperName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEntries_HelperDefinitions_HelperDefinitionId",
                        column: x => x.HelperDefinitionId,
                        principalTable: "HelperDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CallbackEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CallbackGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Initiator = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StopReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    HelperDefinitionId = table.Column<int>(type: "int", nullable: true),
                    HelperName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallbackEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallbackEntries_HelperDefinitions_HelperDefinitionId",
                        column: x => x.HelperDefinitionId,
                        principalTable: "HelperDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Overall = table.Column<byte>(type: "tinyint", nullable: false),
                    DidSaveTime = table.Column<bool>(type: "bit", nullable: false),
                    HoursSaved = table.Column<int>(type: "int", nullable: true),
                    MinutesSaved = table.Column<byte>(type: "tinyint", nullable: true),
                    Successes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Failures = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Improvements = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OtherComments = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    HelperDefinitionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackEntries_HelperDefinitions_HelperDefinitionId",
                        column: x => x.HelperDefinitionId,
                        principalTable: "HelperDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_HelperDefinitionId",
                table: "AccountingEntries",
                column: "HelperDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_UserId_Timestamp",
                table: "AccountingEntries",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CallbackEntries_CallbackGuid",
                table: "CallbackEntries",
                column: "CallbackGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallbackEntries_HelperDefinitionId",
                table: "CallbackEntries",
                column: "HelperDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackEntries_HelperDefinitionId",
                table: "FeedbackEntries",
                column: "HelperDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDefinitions_DefaultStylesheetId",
                table: "HelperDefinitions",
                column: "DefaultStylesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDefinitions_HelperCategoryId",
                table: "HelperDefinitions",
                column: "HelperCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDefinitions_LlmDefinitionId",
                table: "HelperDefinitions",
                column: "LlmDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmDefinitions_Identifier",
                table: "LlmDefinitions",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityPrompts_Email",
                table: "PersonalityPrompts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpendCaps_UserId",
                table: "SpendCaps",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingEntries");

            migrationBuilder.DropTable(
                name: "ArticleStoreItems");

            migrationBuilder.DropTable(
                name: "CallbackEntries");

            migrationBuilder.DropTable(
                name: "FeedbackEntries");

            migrationBuilder.DropTable(
                name: "PersonalityPrompts");

            migrationBuilder.DropTable(
                name: "SpendCaps");

            migrationBuilder.DropTable(
                name: "HelperDefinitions");

            migrationBuilder.DropTable(
                name: "HelperCategories");

            migrationBuilder.DropTable(
                name: "LlmDefinitions");

            migrationBuilder.DropTable(
                name: "Stylesheets");
        }
    }
}
