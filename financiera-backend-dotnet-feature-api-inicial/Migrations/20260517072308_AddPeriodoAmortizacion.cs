using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodoAmortizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "periodo_amortizacion",
                columns: table => new
                {
                    periodo_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prestamo_id = table.Column<int>(type: "int", nullable: false),
                    periodo = table.Column<int>(type: "int", nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    capital_pendiente = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    abono_capital = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    interes_normal = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    interes_iva = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    ahorro_por_pago = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    interes_moratorio = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    gasto_cobranza = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    pago_pactado = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    saldo_final = table.Column<decimal>(type: "decimal(16,4)", nullable: false),
                    fecha_pagado = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    dias_moratorio = table.Column<int>(type: "int", nullable: false),
                    estado_pago = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodo_amortizacion", x => x.periodo_id);
                    table.ForeignKey(
                        name: "FK_periodo_amortizacion_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_periodo_amortizacion_prestamo_id",
                table: "periodo_amortizacion",
                column: "prestamo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "periodo_amortizacion");
        }
    }
}
