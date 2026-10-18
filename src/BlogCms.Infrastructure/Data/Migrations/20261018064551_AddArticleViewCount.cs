using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleViewCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: die bisherigen ArticleViewed-Events aus dem Activity-Log
            // werden einmalig in die neuen Zähler übernommen, damit die Sortierung
            // der Landing Page ab dem ersten Start auf dem historischen Stand basiert.
            migrationBuilder.Sql("""
                UPDATE "Articles" AS a
                SET "ViewCount" = COALESCE(sub.cnt, 0)
                FROM (
                    SELECT "ArticleId", COUNT(*) AS cnt
                    FROM "ActivityLogEntries"
                    WHERE "EventType" = 'ArticleViewed' AND "ArticleId" IS NOT NULL
                    GROUP BY "ArticleId"
                ) AS sub
                WHERE a."Id" = sub."ArticleId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Articles");
        }
    }
}
