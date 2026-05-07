using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteGeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "colonia",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "latitud",
                table: "cliente",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitud",
                table: "cliente",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "colonia",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "latitud",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "longitud",
                table: "cliente");
        }
    }
}
