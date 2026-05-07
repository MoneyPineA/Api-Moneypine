using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPrestamoNuevosCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All columns already applied to DB during partial migration run.
            // This body is intentionally empty so EF Core marks the migration as complete.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "destino",             table: "prestamo");
            migrationBuilder.DropColumn(name: "iva",                 table: "prestamo");
            migrationBuilder.DropColumn(name: "tb_interes_moratorio",table: "prestamo");
            migrationBuilder.DropColumn(name: "tb_interes_normal",   table: "prestamo");
            migrationBuilder.DropColumn(name: "tipo_cnbv",           table: "prestamo");
            migrationBuilder.DropColumn(name: "tipo_tasa",           table: "prestamo");
            migrationBuilder.DropColumn(name: "tipo_tasa_moratorio", table: "prestamo");
        }
    }
}
