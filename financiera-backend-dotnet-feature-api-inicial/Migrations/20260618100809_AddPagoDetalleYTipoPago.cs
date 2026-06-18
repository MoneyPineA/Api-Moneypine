using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoDetalleYTipoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MONEYPINE-FIX: tipo_pago ya existía en DTO; ahora se persiste en la tabla pago
            migrationBuilder.AddColumn<string>(
                name: "tipo_pago",
                table: "pago",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pago_detalle",
                columns: table => new
                {
                    pago_detalle_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    pago_id = table.Column<int>(type: "int", nullable: false),
                    periodo_id = table.Column<int>(type: "int", nullable: true),
                    capital_aplicado = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    interes_aplicado = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    iva_aplicado = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    mora_aplicada = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    periodo_cerrado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tipo_pago = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pago_detalle", x => x.pago_detalle_id);
                    table.ForeignKey(
                        name: "FK_pago_detalle_pago_pago_id",
                        column: x => x.pago_id,
                        principalTable: "pago",
                        principalColumn: "pago_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pago_detalle_periodo_amortizacion_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo_amortizacion",
                        principalColumn: "periodo_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_pago_detalle_pago_id",
                table: "pago_detalle",
                column: "pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_pago_detalle_periodo_id",
                table: "pago_detalle",
                column: "periodo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pago_detalle");

            migrationBuilder.DropColumn(
                name: "tipo_pago",
                table: "pago");
        }
    }
}
