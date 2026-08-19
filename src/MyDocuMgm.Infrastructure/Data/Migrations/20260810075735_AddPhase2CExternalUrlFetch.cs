using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDocuMgm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2CExternalUrlFetch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalContentBlogReuseConfirmedAtUtc",
                table: "Contents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalFetchAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseMimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ETag = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PageTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PageDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFetchAttempts", x => x.Id);
                    table.CheckConstraint("CK_ExternalFetchAttempts_Status", "[Status] IN ('STARTED','SUCCEEDED','FAILED','CANCELLED','APPLIED')");
                    table.ForeignKey(
                        name: "FK_ExternalFetchAttempts_Contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents",
                sql: "[SourceAcquisitionMode] IS NULL OR [SourceAcquisitionMode] IN ('MANUAL','HTTP_METADATA')");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFetchAttempts_ContentId_AttemptNumber",
                table: "ExternalFetchAttempts",
                columns: new[] { "ContentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFetchAttempts_ContentId_StartedAtUtc",
                table: "ExternalFetchAttempts",
                columns: new[] { "ContentId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalFetchAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "ExternalContentBlogReuseConfirmedAtUtc",
                table: "Contents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents",
                sql: "[SourceAcquisitionMode] IS NULL OR [SourceAcquisitionMode] = 'MANUAL'");
        }
    }
}
