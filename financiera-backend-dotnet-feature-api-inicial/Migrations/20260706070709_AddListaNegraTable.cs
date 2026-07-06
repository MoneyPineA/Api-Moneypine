using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddListaNegraTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lista_negra",
                columns: table => new
                {
                    lista_negra_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<int>(type: "int", nullable: false),
                    prestamo_id = table.Column<int>(type: "int", nullable: true),
                    motivo = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dias_mora = table.Column<int>(type: "int", nullable: false),
                    monto_mora = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    estado = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    origen = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_alta = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_baja = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creado_por = table.Column<int>(type: "int", nullable: true),
                    actualizado_por = table.Column<int>(type: "int", nullable: true),
                    observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_creacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_negra", x => x.lista_negra_id);
                    table.ForeignKey(
                        name: "FK_lista_negra_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "cliente_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lista_negra_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_lista_negra_cliente_id",
                table: "lista_negra",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_lista_negra_cliente_id_prestamo_id_estado",
                table: "lista_negra",
                columns: new[] { "cliente_id", "prestamo_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_lista_negra_estado",
                table: "lista_negra",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_lista_negra_prestamo_id",
                table: "lista_negra",
                column: "prestamo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lista_negra");
        }
    }
}
