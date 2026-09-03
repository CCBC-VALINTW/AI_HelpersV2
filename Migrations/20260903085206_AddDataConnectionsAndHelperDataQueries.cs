using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddDataConnectionsAndHelperDataQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EncryptedConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastTestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestSucceeded = table.Column<bool>(type: "bit", nullable: true),
                    LastTestMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelperDataQueries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelperDefinitionId = table.Column<int>(type: "int", nullable: false),
                    DataConnectionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaxRows = table.Column<int>(type: "int", nullable: false),
                    UsageInstruction = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelperDataQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelperDataQueries_DataConnections_DataConnectionId",
                        column: x => x.DataConnectionId,
                        principalTable: "DataConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelperDataQueries_HelperDefinitions_HelperDefinitionId",
                        column: x => x.HelperDefinitionId,
                        principalTable: "HelperDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataQueryExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelperDataQueryId = table.Column<int>(type: "int", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    Truncated = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQueryExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataQueryExecutionLogs_HelperDataQueries_HelperDataQueryId",
                        column: x => x.HelperDataQueryId,
                        principalTable: "HelperDataQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataQueryExecutionLogs_HelperDataQueryId",
                table: "DataQueryExecutionLogs",
                column: "HelperDataQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_DataQueryExecutionLogs_TimestampUtc",
                table: "DataQueryExecutionLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDataQueries_DataConnectionId",
                table: "HelperDataQueries",
                column: "DataConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDataQueries_HelperDefinitionId",
                table: "HelperDataQueries",
                column: "HelperDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataQueryExecutionLogs");

            migrationBuilder.DropTable(
                name: "HelperDataQueries");

            migrationBuilder.DropTable(
                name: "DataConnections");
        }
    }
}
