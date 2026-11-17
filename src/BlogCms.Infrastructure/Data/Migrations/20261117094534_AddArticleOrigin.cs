using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "Human");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Articles");
        }
    }
}
