using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusReportSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DislikesCount",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikesCount",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DislikesCount",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "LikesCount",
                table: "Feedbacks");
        }
    }
}
