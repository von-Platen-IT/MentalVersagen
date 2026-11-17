using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryColorAndSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Categories",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#7f8c8d");

            // Give the built-in categories a recognisable default palette.
            migrationBuilder.Sql("""
                UPDATE "Categories" SET "Color" = '#b03a2e' WHERE "Slug" = 'politik';
                UPDATE "Categories" SET "Color" = '#b7950b' WHERE "Slug" = 'satire';
                UPDATE "Categories" SET "Color" = '#6c3483' WHERE "Slug" = 'verschwoerungstheorien';
                UPDATE "Categories" SET "Color" = '#7f8c8d' WHERE "Slug" = 'undefined';
                """);

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Categories");
        }
    }
}
