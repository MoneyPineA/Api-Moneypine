using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPagoAndCobradorToPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cobrador_id",
                table: "prestamo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "forma_pago",
                table: "prestamo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_cobrador_id",
                table: "prestamo",
                column: "cobrador_id");

            migrationBuilder.AddForeignKey(
                name: "FK_prestamo_usuario_cobrador_id",
                table: "prestamo",
                column: "cobrador_id",
                principalTable: "usuario",
                principalColumn: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prestamo_usuario_cobrador_id",
                table: "prestamo");

            migrationBuilder.DropIndex(
                name: "IX_prestamo_cobrador_id",
                table: "prestamo");

            migrationBuilder.DropColumn(
                name: "cobrador_id",
                table: "prestamo");

            migrationBuilder.DropColumn(
                name: "forma_pago",
                table: "prestamo");
        }
    }
}
