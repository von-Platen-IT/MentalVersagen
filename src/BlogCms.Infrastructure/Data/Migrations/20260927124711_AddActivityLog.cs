using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogCms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RatingValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogEntries_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityLogEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActivityLogEntries_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_ArticleId_EventType",
                table: "ActivityLogEntries",
                columns: new[] { "ArticleId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_CommentId",
                table: "ActivityLogEntries",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_CreatedAt",
                table: "ActivityLogEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_UserId",
                table: "ActivityLogEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogEntries");
        }
    }
}
