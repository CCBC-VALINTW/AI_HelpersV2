using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class WidenContextQuestionLabelFixV1PromptMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Widen first - the correction below moves guidance text (up to ~360 characters on
            // real V1 data) into Label, which needs the room before that UPDATE can run.
            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "HelperContextQuestions",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            // Corrects the previous migration's mapping (see
            // AddSelectContextQuestionsRetireAllowContext's updated comment) - V1's ContextPrompt
            // was guidance text shown directly to the user above the upload control, so it
            // belongs in Label (what the run page actually displays), not UsageInstruction
            // (model-facing only, never shown). Scoped to exactly the rows that migration
            // synthesized - its fixed placeholder Label plus a non-null UsageInstruction is a safe,
            // specific enough marker; nothing else in this table matches both.
            migrationBuilder.Sql("""
                UPDATE [HelperContextQuestions]
                SET [Label] = [UsageInstruction], [UsageInstruction] = NULL
                WHERE [Label] = N'Additional context document'
                  AND [UsageInstruction] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reverse of the data correction - moves the text back to
            // UsageInstruction and restores the placeholder Label for exactly the rows this
            // migration's Up() touched (recognisable because Label no longer equals the
            // placeholder and UsageInstruction is empty from that same change). Rows a user
            // created or edited by hand in between aren't distinguishable from these any more and
            // are deliberately left untouched instead of guessed at.
            migrationBuilder.Sql("""
                UPDATE [HelperContextQuestions]
                SET [UsageInstruction] = [Label], [Label] = N'Additional context document'
                WHERE [Label] <> N'Additional context document'
                  AND [UsageInstruction] IS NULL
                  AND [Type] = N'Document';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "HelperContextQuestions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);
        }
    }
}
