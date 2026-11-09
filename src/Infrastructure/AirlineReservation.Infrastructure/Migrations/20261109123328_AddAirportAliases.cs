using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineReservation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "Airports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "Airports");
        }
    }
}
