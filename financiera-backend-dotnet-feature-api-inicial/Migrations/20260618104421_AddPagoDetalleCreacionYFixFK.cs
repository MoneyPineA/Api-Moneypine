using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoDetalleCreacionYFixFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_creacion",
                table: "pago_detalle",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle",
                column: "prestamo_id",
                principalTable: "prestamo",
                principalColumn: "prestamo_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle");

            migrationBuilder.DropColumn(
                name: "fecha_creacion",
                table: "pago_detalle");

            migrationBuilder.AddForeignKey(
                name: "FK_pago_detalle_prestamo_prestamo_id",
                table: "pago_detalle",
                column: "prestamo_id",
                principalTable: "prestamo",
                principalColumn: "prestamo_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
