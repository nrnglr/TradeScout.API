using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeScout.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewBusinessFields_MobileEmailSocialMediaComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Businesses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialMedia",
                table: "Businesses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "Businesses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "GoogleMapsUrl",
                table: "Businesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "SocialMedia",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "Businesses");

            migrationBuilder.AddColumn<string>(
                name: "GoogleMapsUrl",
                table: "Businesses",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
