using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectContextQuestionsRetireAllowContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "HelperContextQuestions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            // AllowContext/ContextPrompt are being retired in favour of the richer
            // HelperContextQuestion list - before dropping them, port any Helper that still has
            // real data in them into the V2-native equivalent: V1's pairing (a bit that turns on a
            // second, always-optional upload control, plus a prompt shown as guidance text above
            // it - see Project Info/Conwy AI Helpers Govservice Process definition.json) maps onto
            // exactly one Document-type question, with the guidance text carried over as its
            // UsageInstruction. Appended after any existing questions (MAX(SortOrder)+1) rather
            // than assuming SortOrder 0 is free.
            //
            // CORRECTED by a later migration (WidenContextQuestionLabelFixV1PromptMapping): the
            // guidance text belongs in Label (what the user actually sees, matching what V1
            // showed it as) not UsageInstruction (model-facing only) - left as originally applied
            // here rather than rewritten in place, since this had already run against the shared
            // test DB by the time the mapping was corrected. Read that migration alongside this
            // one for the real end state; Tools/DataMigration/Program.cs was fixed directly (not a
            // migration) since it only affects Helpers migrated from V1 after the fix landed.
            migrationBuilder.Sql("""
                INSERT INTO [HelperContextQuestions] ([HelperDefinitionId], [Label], [Type], [IsMandatory], [UsageInstruction], [SortOrder])
                SELECT
                    hd.[Id],
                    N'Additional context document',
                    N'Document',
                    0,
                    LEFT(LTRIM(RTRIM(hd.[ContextPrompt])), 1024),
                    ISNULL((SELECT MAX(hcq.[SortOrder]) FROM [HelperContextQuestions] hcq WHERE hcq.[HelperDefinitionId] = hd.[Id]), -1) + 1
                FROM [HelperDefinitions] hd
                WHERE hd.[AllowContext] = 1
                  AND hd.[ContextPrompt] IS NOT NULL
                  AND LTRIM(RTRIM(hd.[ContextPrompt])) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "AllowContext",
                table: "HelperDefinitions");

            migrationBuilder.DropColumn(
                name: "ContextPrompt",
                table: "HelperDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately doesn't attempt to un-synthesize the HelperContextQuestion rows Up()
            // may have created - there's no reliable way to tell those apart from ones a user
            // created by hand afterwards. AllowContext/ContextPrompt come back empty either way.
            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "HelperContextQuestions");

            migrationBuilder.AddColumn<bool>(
                name: "AllowContext",
                table: "HelperDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContextPrompt",
                table: "HelperDefinitions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
