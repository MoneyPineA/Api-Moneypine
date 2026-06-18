using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoDetallePrestamoid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "prestamo_id",
                table: "pago_detalle",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_pago_detalle_prestamo_id",
                table: "pago_detalle",
                column: "prestamo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle",
                column: "prestamo_id",
                principalTable: "prestamo",
                principalColumn: "prestamo_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle");

            migrationBuilder.DropIndex(
                name: "IX_pago_detalle_prestamo_id",
                table: "pago_detalle");

            migrationBuilder.DropColumn(
                name: "prestamo_id",
                table: "pago_detalle");
        }
    }
}
