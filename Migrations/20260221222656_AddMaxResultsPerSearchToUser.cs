using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeScout.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxResultsPerSearchToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxResultsPerSearch",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxResultsPerSearch",
                table: "Users");
        }
    }
}
