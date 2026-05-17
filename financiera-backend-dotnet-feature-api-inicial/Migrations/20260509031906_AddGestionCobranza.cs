using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddGestionCobranza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gestion_cobranza",
                columns: table => new
                {
                    gestion_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prestamo_id = table.Column<int>(type: "int", nullable: false),
                    usuario_id = table.Column<int>(type: "int", nullable: true),
                    redaccion = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_gestion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gestion_cobranza", x => x.gestion_id);
                    table.ForeignKey(
                        name: "FK_gestion_cobranza_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gestion_cobranza_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notificacion_agendada",
                columns: table => new
                {
                    notificacion_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prestamo_id = table.Column<int>(type: "int", nullable: false),
                    titulo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detalles = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_hora = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacion_agendada", x => x.notificacion_id);
                    table.ForeignKey(
                        name: "FK_notificacion_agendada_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_gestion_cobranza_prestamo_id",
                table: "gestion_cobranza",
                column: "prestamo_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestion_cobranza_usuario_id",
                table: "gestion_cobranza",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_agendada_prestamo_id",
                table: "notificacion_agendada",
                column: "prestamo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gestion_cobranza");

            migrationBuilder.DropTable(
                name: "notificacion_agendada");
        }
    }
}
