using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultiplePersonalities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalityPrompts_Email",
                table: "PersonalityPrompts");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "PersonalityPrompts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PersonalityPrompts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "My personality");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityPrompts_Email",
                table: "PersonalityPrompts",
                column: "Email",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityPrompts_Email_Name",
                table: "PersonalityPrompts",
                columns: new[] { "Email", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalityPrompts_Email",
                table: "PersonalityPrompts");

            migrationBuilder.DropIndex(
                name: "IX_PersonalityPrompts_Email_Name",
                table: "PersonalityPrompts");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "PersonalityPrompts");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PersonalityPrompts");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityPrompts_Email",
                table: "PersonalityPrompts",
                column: "Email",
                unique: true);
        }
    }
}
