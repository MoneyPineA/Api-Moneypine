using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "grupo_id",
                table: "prestamo",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grupo",
                columns: table => new
                {
                    grupo_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_creacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    estatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clasificacion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    monto = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    tasa_interes = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    plazo_meses = table.Column<int>(type: "int", nullable: false),
                    forma_pago = table.Column<int>(type: "int", nullable: false),
                    tipo_cnbv = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tb_interes_normal = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo_tasa = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tb_interes_moratorio = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo_tasa_moratorio = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    moratorio_por_dia = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    destino = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupo", x => x.grupo_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_grupo_id",
                table: "prestamo",
                column: "grupo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_prestamo_grupo_grupo_id",
                table: "prestamo",
                column: "grupo_id",
                principalTable: "grupo",
                principalColumn: "grupo_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prestamo_grupo_grupo_id",
                table: "prestamo");

            migrationBuilder.DropTable(
                name: "grupo");

            migrationBuilder.DropIndex(
                name: "IX_prestamo_grupo_id",
                table: "prestamo");

            migrationBuilder.DropColumn(
                name: "grupo_id",
                table: "prestamo");
        }
    }
}
