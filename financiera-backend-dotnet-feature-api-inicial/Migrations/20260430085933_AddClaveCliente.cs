using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddClaveCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clave_cliente",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "clave_cliente",
                table: "cliente");
        }
    }
}
