using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "documento",
                columns: table => new
                {
                    documento_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<int>(type: "int", nullable: false),
                    prestamo_id = table.Column<int>(type: "int", nullable: true),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    nombre_original = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre_archivo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo_mime = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo_documento = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    ruta_archivo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_subida = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento", x => x.documento_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    usuario_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    correo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    apellido = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telefono = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rol = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estado = table.Column<string>(type: "longtext", nullable: false, defaultValue: "ACTIVO")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_registro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.usuario_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cliente",
                columns: table => new
                {
                    cliente_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    direccion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_nacimiento = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    curp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rfc = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cliente", x => x.cliente_id);
                    table.ForeignKey(
                        name: "FK_cliente_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    token = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_expiracion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_revoked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_revocacion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.refresh_token_id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "prestamo",
                columns: table => new
                {
                    prestamo_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cliente_id = table.Column<int>(type: "int", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    monto_total = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    saldo_actual = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    tasa_interes = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    plazo_meses = table.Column<int>(type: "int", nullable: false),
                    pago_mes = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_proximo_pago = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    fecha_fin = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    estatus = table.Column<string>(type: "longtext", nullable: false, defaultValue: "ACTIVO")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dias_gracia = table.Column<int>(type: "int", nullable: false),
                    mora_diaria = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestamo", x => x.prestamo_id);
                    table.ForeignKey(
                        name: "FK_prestamo_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "cliente_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pago",
                columns: table => new
                {
                    pago_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prestamo_id = table.Column<int>(type: "int", nullable: false),
                    cobrador_id = table.Column<int>(type: "int", nullable: true),
                    fecha_pago = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    monto_pagado = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    interes_pagado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    mora_pagada = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    saldo_restante = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    metodo_pago = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estatus = table.Column<string>(type: "longtext", nullable: false, defaultValue: "APLICADO")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pago", x => x.pago_id);
                    table.ForeignKey(
                        name: "FK_pago_prestamo_prestamo_id",
                        column: x => x.prestamo_id,
                        principalTable: "prestamo",
                        principalColumn: "prestamo_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_usuario_id",
                table: "cliente",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pago_prestamo_id",
                table: "pago",
                column: "prestamo_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_cliente_id",
                table: "prestamo",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_usuario_id",
                table: "refresh_tokens",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "documento");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "pago");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "prestamo");

            migrationBuilder.DropTable(
                name: "cliente");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
