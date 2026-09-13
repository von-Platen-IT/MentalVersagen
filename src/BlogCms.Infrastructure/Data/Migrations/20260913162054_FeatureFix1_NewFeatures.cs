using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeatureFix1_NewFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHighlighted",
                table: "Comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "Public");

            // Preserve the previous paywall flag: premium articles keep premium access.
            migrationBuilder.Sql("UPDATE \"Articles\" SET \"AccessLevel\" = 'Premium' WHERE \"IsPremium\" = true;");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Articles");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleImageUrl",
                table: "Articles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Hashtags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hashtags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ratings_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleHashtags",
                columns: table => new
                {
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    HashtagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleHashtags", x => new { x.ArticleId, x.HashtagId });
                    table.ForeignKey(
                        name: "FK_ArticleHashtags_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleHashtags_Hashtags_HashtagId",
                        column: x => x.HashtagId,
                        principalTable: "Hashtags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkListItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinkListItems_LinkLists_LinkListId",
                        column: x => x.LinkListId,
                        principalTable: "LinkLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleHashtags_HashtagId",
                table: "ArticleHashtags",
                column: "HashtagId");

            migrationBuilder.CreateIndex(
                name: "IX_Hashtags_Name",
                table: "Hashtags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hashtags_Slug",
                table: "Hashtags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkListItems_ArticleId",
                table: "LinkListItems",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkListItems_LinkListId_Position",
                table: "LinkListItems",
                columns: new[] { "LinkListId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkLists_Slug",
                table: "LinkLists",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_ArticleId",
                table: "Ratings",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CommentId",
                table: "Ratings",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_ArticleId",
                table: "Ratings",
                columns: new[] { "UserId", "ArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_CommentId",
                table: "Ratings",
                columns: new[] { "UserId", "CommentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleHashtags");

            migrationBuilder.DropTable(
                name: "LinkListItems");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "Hashtags");

            migrationBuilder.DropTable(
                name: "LinkLists");

            migrationBuilder.DropColumn(
                name: "IsHighlighted",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "TitleImageUrl",
                table: "Articles");

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Reconstruct the legacy premium flag before dropping AccessLevel.
            migrationBuilder.Sql("UPDATE \"Articles\" SET \"IsPremium\" = true WHERE \"AccessLevel\" = 'Premium';");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Articles");
        }
    }
}
