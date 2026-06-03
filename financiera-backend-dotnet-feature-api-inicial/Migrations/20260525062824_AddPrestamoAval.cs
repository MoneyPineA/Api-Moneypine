using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class AddPrestamoAval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "notifications");

            migrationBuilder.AddColumn<decimal>(
                name: "abono_capital",
                table: "pago",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "cp",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "estado_domicilio",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "municipio",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "num_ext",
                table: "cliente",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "buro_exclusion",
                columns: table => new
                {
                    cliente_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    excluido_por = table.Column<int>(type: "int", nullable: true),
                    fecha = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    motivo = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buro_exclusion", x => x.cliente_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "concepto_sistema",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concepto_sistema", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prestamo_aval",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prestamo_id = table.Column<int>(type: "int", nullable: false),
                    cliente_id_aval = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestamo_aval", x => x.id);
                    table.ForeignKey(
                        name: "FK_prestamo_aval_cliente_cliente_id_aval",
                        column: x => x.cliente_id_aval,
                        principalTable: "cliente",
                        principalColumn: "cliente_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prestamo_aval_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "producto_ahorro",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tasa_anual = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    plazo_dias = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_ahorro", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "producto_credito",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tipo_credito = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    monto_base = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    forma_pago = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    plazo = table.Column<int>(type: "int", nullable: false),
                    tasa_interes = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    mora_diaria = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    comision_apertura = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    es_defecto = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_credito", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cuenta_ahorro",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<int>(type: "int", nullable: false),
                    producto_ahorro_id = table.Column<int>(type: "int", nullable: false),
                    monto_inicial = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    saldo_actual = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    fecha_apertura = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    estatus = table.Column<string>(type: "longtext", nullable: false, defaultValue: "ACTIVA")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ejecutivo_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuenta_ahorro", x => x.id);
                    table.ForeignKey(
                        name: "FK_cuenta_ahorro_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "cliente_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cuenta_ahorro_producto_ahorro_producto_ahorro_id",
                        column: x => x.producto_ahorro_id,
                        principalTable: "producto_ahorro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "movimiento_ahorro",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cuenta_ahorro_id = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    monto = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    descripcion = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_ahorro", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimiento_ahorro_cuenta_ahorro_cuenta_ahorro_id",
                        column: x => x.cuenta_ahorro_id,
                        principalTable: "cuenta_ahorro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cuenta_ahorro_cliente_id",
                table: "cuenta_ahorro",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_cuenta_ahorro_producto_ahorro_id",
                table: "cuenta_ahorro",
                column: "producto_ahorro_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_ahorro_cuenta_ahorro_id",
                table: "movimiento_ahorro",
                column: "cuenta_ahorro_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_aval_cliente_id_aval",
                table: "prestamo_aval",
                column: "cliente_id_aval");

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_aval_prestamo_id",
                table: "prestamo_aval",
                column: "prestamo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buro_exclusion");

            migrationBuilder.DropTable(
                name: "concepto_sistema");

            migrationBuilder.DropTable(
                name: "movimiento_ahorro");

            migrationBuilder.DropTable(
                name: "prestamo_aval");

            migrationBuilder.DropTable(
                name: "producto_credito");

            migrationBuilder.DropTable(
                name: "cuenta_ahorro");

            migrationBuilder.DropTable(
                name: "producto_ahorro");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "abono_capital",
                table: "pago");

            migrationBuilder.DropColumn(
                name: "cp",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "estado_domicilio",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "municipio",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "num_ext",
                table: "cliente");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "Notifications");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications",
                column: "Id");
        }
    }
}
