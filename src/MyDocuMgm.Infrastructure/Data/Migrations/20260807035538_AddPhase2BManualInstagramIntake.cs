using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDocuMgm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2BManualInstagramIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstagramContentType",
                table: "Contents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualCaption",
                table: "Contents",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinnedAuthorCommentState",
                table: "Contents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinnedAuthorCommentText",
                table: "Contents",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAcquisitionMode",
                table: "Contents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_InstagramContentType",
                table: "Contents",
                sql: "[InstagramContentType] IS NULL OR [InstagramContentType] IN ('POST','REEL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_PinnedAuthorCommentConsistency",
                table: "Contents",
                sql: "([PinnedAuthorCommentState] IS NULL AND [PinnedAuthorCommentText] IS NULL) OR ([PinnedAuthorCommentState] = 'NONE' AND [PinnedAuthorCommentText] IS NULL) OR ([PinnedAuthorCommentState] = 'PRESENT' AND LEN(LTRIM(RTRIM([PinnedAuthorCommentText]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_PinnedAuthorCommentState",
                table: "Contents",
                sql: "[PinnedAuthorCommentState] IS NULL OR [PinnedAuthorCommentState] IN ('PRESENT','NONE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents",
                sql: "[SourceAcquisitionMode] IS NULL OR [SourceAcquisitionMode] = 'MANUAL'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_InstagramContentType",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_PinnedAuthorCommentConsistency",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_PinnedAuthorCommentState",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_SourceAcquisitionMode",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "InstagramContentType",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "ManualCaption",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "PinnedAuthorCommentState",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "PinnedAuthorCommentText",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "SourceAcquisitionMode",
                table: "Contents");
        }
    }
}
