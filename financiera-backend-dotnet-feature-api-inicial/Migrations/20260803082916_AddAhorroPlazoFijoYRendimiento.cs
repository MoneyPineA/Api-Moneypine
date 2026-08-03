using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddAhorroPlazoFijoYRendimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "producto_ahorro",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "VISTA")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_ultimo_rendimiento",
                table: "cuenta_ahorro",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rendimiento_acumulado",
                table: "cuenta_ahorro",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo",
                table: "producto_ahorro");

            migrationBuilder.DropColumn(
                name: "fecha_ultimo_rendimiento",
                table: "cuenta_ahorro");

            migrationBuilder.DropColumn(
                name: "rendimiento_acumulado",
                table: "cuenta_ahorro");
        }
    }
}
