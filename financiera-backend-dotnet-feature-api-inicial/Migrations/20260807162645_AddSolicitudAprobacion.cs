using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitudAprobacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitud_aprobacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estado = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDIENTE")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    solicitante_id = table.Column<int>(type: "int", nullable: false),
                    justificacion = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entidad_id = table.Column<int>(type: "int", nullable: false),
                    cliente_id = table.Column<int>(type: "int", nullable: true),
                    monto = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    payload = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resuelta_por = table.Column<int>(type: "int", nullable: true),
                    respuesta = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    resuelta_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud_aprobacion", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitud_aprobacion_usuario_resuelta_por",
                        column: x => x.resuelta_por,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_aprobacion_usuario_solicitante_id",
                        column: x => x.solicitante_id,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_aprobacion_estado_created_at",
                table: "solicitud_aprobacion",
                columns: new[] { "estado", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_aprobacion_resuelta_por",
                table: "solicitud_aprobacion",
                column: "resuelta_por");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_aprobacion_solicitante_id",
                table: "solicitud_aprobacion",
                column: "solicitante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitud_aprobacion");
        }
    }
}
