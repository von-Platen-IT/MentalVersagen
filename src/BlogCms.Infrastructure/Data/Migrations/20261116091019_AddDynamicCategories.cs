using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicCategories : Migration
    {
        // Stable ids shared with BlogCms.Domain.Categories.CategoryIds.
        private const string UndefinedId = "00000000-0000-0000-0000-000000000001";
        private const string PolitikId = "00000000-0000-0000-0000-000000000002";
        private const string SatireId = "00000000-0000-0000-0000-000000000003";
        private const string VerschwoerungId = "00000000-0000-0000-0000-000000000004";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Category catalogue first, so the new FK can point at it.
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Disclaimer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            // 2) Seed the system category and the three legacy enum values.
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[]
                {
                    "Id", "Name", "Slug", "Description", "Disclaimer",
                    "DisplayOrder", "IsSystem", "CreatedAt"
                },
                values: new object[,]
                {
                    {
                        new Guid(UndefinedId), "undefined", "undefined",
                        "Beiträge ohne ausdrückliche Kategorie.", null,
                        1000, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid(PolitikId), "Politik", "politik",
                        null, null,
                        1, false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid(SatireId), "Satire", "satire",
                        null,
                        "Dieser Beitrag kann satirische Inhalte enthalten und unter Zuhilfenahme von KI erstellt " +
                        "worden sein. Es handelt sich um ein Kunstwerk!",
                        2, false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid(VerschwoerungId), "Verschwörungstheorien", "verschwoerungstheorien",
                        null,
                        "Dieser Beitrag behandelt verschwörungstheoretische Inhalte zur kritischen Auseinandersetzung " +
                        "und dient nicht der Verbreitung von Falschinformationen.",
                        3, false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                });

            // 3) Add the FK column as nullable, backfill from the old enum text,
            //    then make it required with "undefined" as default.
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                $"""
                UPDATE "Articles"
                SET "CategoryId" = CASE "Category"
                    WHEN 'Politik' THEN '{PolitikId}'::uuid
                    WHEN 'Satire' THEN '{SatireId}'::uuid
                    WHEN 'Verschwoerungstheorien' THEN '{VerschwoerungId}'::uuid
                    ELSE '{UndefinedId}'::uuid
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Articles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid(UndefinedId));

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Articles");

            // 4) Indexes and relationship.
            migrationBuilder.CreateIndex(
                name: "IX_Articles_CategoryId",
                table: "Articles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Categories_CategoryId",
                table: "Articles",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Categories_CategoryId",
                table: "Articles");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "Politik");

            migrationBuilder.Sql(
                $"""
                UPDATE "Articles"
                SET "Category" = CASE "CategoryId"
                    WHEN '{SatireId}'::uuid THEN 'Satire'
                    WHEN '{VerschwoerungId}'::uuid THEN 'Verschwoerungstheorien'
                    ELSE 'Politik'
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Articles_CategoryId",
                table: "Articles");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Articles");
        }
    }
}
