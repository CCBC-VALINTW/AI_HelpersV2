using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class RedesignCallbackEntryForOutputRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "CallbackEntries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CallbackEntries");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "CallbackEntries",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "Initiator",
                table: "CallbackEntries",
                newName: "SuggestedFileName");

            migrationBuilder.AlterColumn<string>(
                name: "StopReason",
                table: "CallbackEntries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OutputTokens",
                table: "CallbackEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InputTokens",
                table: "CallbackEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmail",
                table: "CallbackEntries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutputHtml",
                table: "CallbackEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CallbackEntries_CreatedByEmail_CreatedAtUtc",
                table: "CallbackEntries",
                columns: new[] { "CreatedByEmail", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CallbackEntries_CreatedByEmail_CreatedAtUtc",
                table: "CallbackEntries");

            migrationBuilder.DropColumn(
                name: "CreatedByEmail",
                table: "CallbackEntries");

            migrationBuilder.DropColumn(
                name: "OutputHtml",
                table: "CallbackEntries");

            migrationBuilder.RenameColumn(
                name: "SuggestedFileName",
                table: "CallbackEntries",
                newName: "Initiator");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "CallbackEntries",
                newName: "Timestamp");

            migrationBuilder.AlterColumn<string>(
                name: "StopReason",
                table: "CallbackEntries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OutputTokens",
                table: "CallbackEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "InputTokens",
                table: "CallbackEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "CallbackEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CallbackEntries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
