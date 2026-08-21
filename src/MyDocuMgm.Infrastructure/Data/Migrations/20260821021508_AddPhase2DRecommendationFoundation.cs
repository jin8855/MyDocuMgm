using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDocuMgm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2DRecommendationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisRecommendationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderIdentifier = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRecommendationRuns", x => x.Id);
                    table.CheckConstraint("CK_AnalysisRecommendationRuns_Status", "[Status] IN ('REQUESTED','SUCCEEDED','PARTIALLY_SUCCEEDED','FAILED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_AnalysisRecommendationRuns_Contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRecommendationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecommendedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModifiedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRecommendationItems", x => x.Id);
                    table.CheckConstraint("CK_AnalysisRecommendationItems_Confidence", "[Confidence] IN ('LOW','MEDIUM','HIGH')");
                    table.CheckConstraint("CK_AnalysisRecommendationItems_Decision", "[Decision] IN ('PENDING','APPLIED','MODIFIED','REJECTED')");
                    table.CheckConstraint("CK_AnalysisRecommendationItems_Kind", "[Kind] IN ('TITLE','SUMMARY','CATEGORY','TAG')");
                    table.ForeignKey(
                        name: "FK_AnalysisRecommendationItems_AnalysisRecommendationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AnalysisRecommendationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRecommendationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRecommendationEvidence", x => x.Id);
                    table.CheckConstraint("CK_AnalysisRecommendationEvidence_Type", "[EvidenceType] IN ('DETAIL_CONTENT','MANUAL_CAPTION','PINNED_AUTHOR_COMMENT','SOURCE_EVIDENCE')");
                    table.ForeignKey(
                        name: "FK_AnalysisRecommendationEvidence_AnalysisRecommendationItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "AnalysisRecommendationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisRecommendationEvidence_SourceEvidence_SourceEvidenceId",
                        column: x => x.SourceEvidenceId,
                        principalTable: "SourceEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecommendationEvidence_ItemId",
                table: "AnalysisRecommendationEvidence",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecommendationEvidence_SourceEvidenceId",
                table: "AnalysisRecommendationEvidence",
                column: "SourceEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecommendationItems_RunId_Kind",
                table: "AnalysisRecommendationItems",
                columns: new[] { "RunId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecommendationRuns_ContentId_IdempotencyKey",
                table: "AnalysisRecommendationRuns",
                columns: new[] { "ContentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecommendationRuns_ContentId_RequestedAtUtc",
                table: "AnalysisRecommendationRuns",
                columns: new[] { "ContentId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisRecommendationEvidence");

            migrationBuilder.DropTable(
                name: "AnalysisRecommendationItems");

            migrationBuilder.DropTable(
                name: "AnalysisRecommendationRuns");
        }
    }
}
