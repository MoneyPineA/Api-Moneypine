using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddCondonacionCreditoToPeriodoAmortizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "capital_condonado",
                table: "periodo_amortizacion",
                type: "decimal(16,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "interes_condonado",
                table: "periodo_amortizacion",
                type: "decimal(16,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "iva_condonado",
                table: "periodo_amortizacion",
                type: "decimal(16,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capital_condonado",
                table: "periodo_amortizacion");

            migrationBuilder.DropColumn(
                name: "interes_condonado",
                table: "periodo_amortizacion");

            migrationBuilder.DropColumn(
                name: "iva_condonado",
                table: "periodo_amortizacion");
        }
    }
}
