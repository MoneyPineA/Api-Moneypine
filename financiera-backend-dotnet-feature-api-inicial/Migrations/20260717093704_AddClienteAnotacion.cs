using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteAnotacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: las columnas fec_alta, fec_baja, numero_int, ref_adicional,
            // ref_calle1, ref_calle2 y tel_celular YA EXISTEN en la BD de Railway
            // (se agregaron via ALTER TABLE manual desde el sistema anterior).
            // Esta migración solo las incorpora al snapshot del modelo EF;
            // NO deben crearse aquí o el deploy fallaría con "Duplicate column".

            migrationBuilder.CreateTable(
                name: "cliente_anotacion",
                columns: table => new
                {
                    anotacion_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<int>(type: "int", nullable: false),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    origen = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    anotacion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_creacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cliente_anotacion", x => x.anotacion_id);
                    table.ForeignKey(
                        name: "FK_cliente_anotacion_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "cliente_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cliente_anotacion_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_anotacion_cliente_id",
                table: "cliente_anotacion",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_anotacion_usuario_id",
                table: "cliente_anotacion",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se revierte la tabla creada; las columnas de cliente
            // preexistían a esta migración y no deben eliminarse.
            migrationBuilder.DropTable(
                name: "cliente_anotacion");
        }
    }
}
