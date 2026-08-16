using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class MT02_PrestamistaDatosContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ciudad",
                table: "prestamista",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "correo_contacto",
                table: "prestamista",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "direccion",
                table: "prestamista",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "prestamista",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "notas",
                table: "prestamista",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "persona_contacto",
                table: "prestamista",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "telefono_contacto",
                table: "prestamista",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ciudad",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "correo_contacto",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "direccion",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "notas",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "persona_contacto",
                table: "prestamista");

            migrationBuilder.DropColumn(
                name: "telefono_contacto",
                table: "prestamista");
        }
    }
}
