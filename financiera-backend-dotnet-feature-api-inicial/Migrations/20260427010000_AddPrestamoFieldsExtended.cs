using ApiEjemplo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260427010000_AddPrestamoFieldsExtended")]
    /// <inheritdoc />
    public partial class AddPrestamoFieldsExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "comision_apertura",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "seguro_credito",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ing_semanal",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ing_otros",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ing_total",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_alimento",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_luz",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_telefono",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_transporte",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_renta",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_inversion",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_creditos",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "g_otros",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_gasto",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_disponible",
                table: "prestamo",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombre_banco",
                table: "prestamo",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "num_cuenta",
                table: "prestamo",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "comision_apertura",  table: "prestamo");
            migrationBuilder.DropColumn(name: "seguro_credito",     table: "prestamo");
            migrationBuilder.DropColumn(name: "ing_semanal",        table: "prestamo");
            migrationBuilder.DropColumn(name: "ing_otros",          table: "prestamo");
            migrationBuilder.DropColumn(name: "ing_total",          table: "prestamo");
            migrationBuilder.DropColumn(name: "g_alimento",         table: "prestamo");
            migrationBuilder.DropColumn(name: "g_luz",              table: "prestamo");
            migrationBuilder.DropColumn(name: "g_telefono",         table: "prestamo");
            migrationBuilder.DropColumn(name: "g_transporte",       table: "prestamo");
            migrationBuilder.DropColumn(name: "g_renta",            table: "prestamo");
            migrationBuilder.DropColumn(name: "g_inversion",        table: "prestamo");
            migrationBuilder.DropColumn(name: "g_creditos",         table: "prestamo");
            migrationBuilder.DropColumn(name: "g_otros",            table: "prestamo");
            migrationBuilder.DropColumn(name: "total_gasto",        table: "prestamo");
            migrationBuilder.DropColumn(name: "total_disponible",   table: "prestamo");
            migrationBuilder.DropColumn(name: "nombre_banco",       table: "prestamo");
            migrationBuilder.DropColumn(name: "num_cuenta",         table: "prestamo");
        }
    }
}
