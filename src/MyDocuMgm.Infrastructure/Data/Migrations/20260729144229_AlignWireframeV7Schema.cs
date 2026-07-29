using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MyDocuMgm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignWireframeV7Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                table: "MediaAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SourceTimestampMs",
                table: "MediaAssets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngredientType",
                table: "CookingIngredients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "부재료");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "CookingIngredients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CookingIngredients",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaAssetId",
                table: "ContentSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentWorkflowStep",
                table: "Contents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "URL");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Categories",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "CategorySearchAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSearchable = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorySearchAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorySearchAttributes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "DisplayName", "IsActive" },
                values: new object[] { "폰&컴", true });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"),
                column: "IsActive",
                value: true);

            migrationBuilder.InsertData(
                table: "CategorySearchAttributes",
                columns: new[] { "Id", "AttributeKey", "CategoryId", "DisplayName", "IsActive", "IsSearchable", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0001-000000000001"), "region", new Guid("10000000-0000-0000-0000-000000000001"), "지역", true, true, 1 },
                    { new Guid("20000000-0000-0000-0001-000000000002"), "parking", new Guid("10000000-0000-0000-0000-000000000001"), "주차", true, true, 2 },
                    { new Guid("20000000-0000-0000-0002-000000000001"), "primaryIngredient", new Guid("10000000-0000-0000-0000-000000000002"), "주재료", true, true, 1 },
                    { new Guid("20000000-0000-0000-0002-000000000002"), "difficulty", new Guid("10000000-0000-0000-0000-000000000002"), "난이도", true, true, 2 },
                    { new Guid("20000000-0000-0000-0002-000000000003"), "time", new Guid("10000000-0000-0000-0000-000000000002"), "소요시간", true, true, 3 },
                    { new Guid("20000000-0000-0000-0003-000000000001"), "targetArea", new Guid("10000000-0000-0000-0000-000000000003"), "운동 부위", true, true, 1 },
                    { new Guid("20000000-0000-0000-0003-000000000002"), "equipment", new Guid("10000000-0000-0000-0000-000000000003"), "준비물", true, true, 2 },
                    { new Guid("20000000-0000-0000-0004-000000000001"), "target", new Guid("10000000-0000-0000-0000-000000000004"), "대상", true, true, 1 },
                    { new Guid("20000000-0000-0000-0004-000000000002"), "supplies", new Guid("10000000-0000-0000-0000-000000000004"), "세제·제품", true, true, 2 },
                    { new Guid("20000000-0000-0000-0005-000000000001"), "destination", new Guid("10000000-0000-0000-0000-000000000005"), "국가·지역", true, true, 1 },
                    { new Guid("20000000-0000-0000-0005-000000000002"), "transport", new Guid("10000000-0000-0000-0000-000000000005"), "교통", true, true, 2 },
                    { new Guid("20000000-0000-0000-0006-000000000001"), "camera", new Guid("10000000-0000-0000-0000-000000000006"), "기기", true, true, 1 },
                    { new Guid("20000000-0000-0000-0006-000000000002"), "location", new Guid("10000000-0000-0000-0000-000000000006"), "촬영 장소", true, true, 2 },
                    { new Guid("20000000-0000-0000-0007-000000000001"), "subject", new Guid("10000000-0000-0000-0000-000000000007"), "분야", true, true, 1 },
                    { new Guid("20000000-0000-0000-0007-000000000002"), "resource", new Guid("10000000-0000-0000-0000-000000000007"), "참고 자료", true, true, 2 },
                    { new Guid("20000000-0000-0000-0008-000000000001"), "brand", new Guid("10000000-0000-0000-0000-000000000008"), "브랜드", true, true, 1 },
                    { new Guid("20000000-0000-0000-0008-000000000002"), "store", new Guid("10000000-0000-0000-0000-000000000008"), "구매처", true, true, 2 },
                    { new Guid("20000000-0000-0000-0008-000000000003"), "price", new Guid("10000000-0000-0000-0000-000000000008"), "가격대", true, true, 3 },
                    { new Guid("20000000-0000-0000-0009-000000000001"), "deviceOrOs", new Guid("10000000-0000-0000-0000-000000000009"), "기기·OS", true, true, 1 },
                    { new Guid("20000000-0000-0000-0009-000000000002"), "problem", new Guid("10000000-0000-0000-0000-000000000009"), "문제 유형", true, true, 2 },
                    { new Guid("20000000-0000-0000-0010-000000000001"), "situation", new Guid("10000000-0000-0000-0000-000000000010"), "적용 상황", true, true, 1 },
                    { new Guid("20000000-0000-0000-0010-000000000002"), "keyPoint", new Guid("10000000-0000-0000-0000-000000000010"), "주제", true, true, 2 },
                    { new Guid("20000000-0000-0000-0011-000000000001"), "customLabel", new Guid("10000000-0000-0000-0000-000000000011"), "주제", true, true, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ContentId_IsSelected_SourceTimestampMs",
                table: "MediaAssets",
                columns: new[] { "ContentId", "IsSelected", "SourceTimestampMs" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ContentId_Sha256",
                table: "MediaAssets",
                columns: new[] { "ContentId", "Sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentSteps_MediaAssetId",
                table: "ContentSteps",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_CurrentWorkflowStep_UpdatedAtUtc",
                table: "Contents",
                columns: new[] { "CurrentWorkflowStep", "UpdatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_CurrentWorkflowStep",
                table: "Contents",
                sql: "[CurrentWorkflowStep] IN ('URL','ANALYSIS_REVIEW','CATEGORY_EDIT','MEDIA','DETAIL','BLOG_DRAFT','COMPLETED')");

            migrationBuilder.CreateIndex(
                name: "IX_CategorySearchAttributes_CategoryId_AttributeKey",
                table: "CategorySearchAttributes",
                columns: new[] { "CategoryId", "AttributeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategorySearchAttributes_CategoryId_IsActive_IsSearchable_SortOrder",
                table: "CategorySearchAttributes",
                columns: new[] { "CategoryId", "IsActive", "IsSearchable", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentSteps_MediaAssets_MediaAssetId",
                table: "ContentSteps",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentSteps_MediaAssets_MediaAssetId",
                table: "ContentSteps");

            migrationBuilder.DropTable(
                name: "CategorySearchAttributes");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ContentId_IsSelected_SourceTimestampMs",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ContentId_Sha256",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_ContentSteps_MediaAssetId",
                table: "ContentSteps");

            migrationBuilder.DropIndex(
                name: "IX_Contents_CurrentWorkflowStep_UpdatedAtUtc",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_CurrentWorkflowStep",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "SourceTimestampMs",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "IngredientType",
                table: "CookingIngredients");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "CookingIngredients");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CookingIngredients");

            migrationBuilder.DropColumn(
                name: "MediaAssetId",
                table: "ContentSteps");

            migrationBuilder.DropColumn(
                name: "CurrentWorkflowStep",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Categories");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                column: "DisplayName",
                value: "폰·컴퓨터");
        }
    }
}
