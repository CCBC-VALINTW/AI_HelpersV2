using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddHelperFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelperFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HelperDefinitionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelperFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelperFavorites_HelperDefinitions_HelperDefinitionId",
                        column: x => x.HelperDefinitionId,
                        principalTable: "HelperDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelperFavorites_HelperDefinitionId",
                table: "HelperFavorites",
                column: "HelperDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HelperFavorites_UserEmail_HelperDefinitionId",
                table: "HelperFavorites",
                columns: new[] { "UserEmail", "HelperDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelperFavorites");
        }
    }
}
