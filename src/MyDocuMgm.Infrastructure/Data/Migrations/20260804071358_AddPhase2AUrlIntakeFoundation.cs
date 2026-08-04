using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDocuMgm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2AUrlIntakeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntakeStatus",
                table: "Contents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUrl",
                table: "Contents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "NormalizedUrlHash",
                table: "Contents",
                type: "binary(32)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalUrl",
                table: "Contents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "Contents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentMediaLinks",
                columns: table => new
                {
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentMediaLinks", x => new { x.ContentId, x.MediaAssetId });
                    table.ForeignKey(
                        name: "FK_ContentMediaLinks_Contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentMediaLinks_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contents_NormalizedUrlHash",
                table: "Contents",
                column: "NormalizedUrlHash",
                unique: true,
                filter: "[NormalizedUrlHash] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_IntakeStatus",
                table: "Contents",
                sql: "[IntakeStatus] IS NULL OR [IntakeStatus] IN ('URL_ACCEPTED','MANUAL_INPUT_REQUIRED','CONTENT_READY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_SourceKind",
                table: "Contents",
                sql: "[SourceKind] IS NULL OR [SourceKind] IN ('GENERIC','INSTAGRAM')");

            migrationBuilder.CreateIndex(
                name: "IX_ContentMediaLinks_MediaAssetId",
                table: "ContentMediaLinks",
                column: "MediaAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentMediaLinks");

            migrationBuilder.DropIndex(
                name: "IX_Contents_NormalizedUrlHash",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_IntakeStatus",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_SourceKind",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "IntakeStatus",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "NormalizedUrl",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "NormalizedUrlHash",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "OriginalUrl",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "Contents");
        }
    }
}
