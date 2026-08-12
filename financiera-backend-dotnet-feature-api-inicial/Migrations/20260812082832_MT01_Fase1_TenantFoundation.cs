using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEjemplo.Migrations
{
    /// <inheritdoc />
    // MONEYPINE-MT: Fase 1 — cimientos de multi-tenancy (Parte 4.2 / 4.3 del
    // documento de arquitectura). Crea la tabla `prestamista`, siembra el
    // tenant 1 = MoneyPine, y agrega prestamista_id NOT NULL (sin DEFAULT al
    // terminar) a las 27 tablas de negocio del EF model + configuracion_sistema
    // (que no tiene entidad EF). refresh_tokens queda fuera a propósito: llega
    // al tenant vía FK a usuario.
    //
    // RIESGO DE ORDEN (ver Program.cs líneas ~262-392): db.Database.Migrate()
    // corre ANTES que los bloques ExecuteSqlRawAsync que crean
    // producto_ahorro, cuenta_ahorro, movimiento_ahorro, concepto_sistema y
    // buro_exclusion. En un entorno limpio esas tablas NO EXISTEN todavía
    // cuando esta migración corre. Se resuelve adoptándolas aquí con
    // CREATE TABLE IF NOT EXISTS usando el DDL exacto de Program.cs antes de
    // alterarlas — así el bloque de Program.cs, que corre después, encuentra
    // la tabla ya creada y su CREATE TABLE IF NOT EXISTS es un no-op.
    // configuracion_sistema no la crea nadie en el código (deuda preexistente,
    // no introducida por esta migración); se adopta igual para no romper el
    // arranque en limpio.
    public partial class MT01_Fase1_TenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =======================================================
            // 1) Tabla de tenants + seed (tenant 1 = MoneyPine)
            // =======================================================
            migrationBuilder.CreateTable(
                name: "prestamista",
                columns: table => new
                {
                    prestamista_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    slug = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre_comercial = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    razon_social = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estatus = table.Column<string>(type: "longtext", nullable: false, defaultValue: "ACTIVO")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    plan = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    moneda = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    zona_horaria = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    logo_url = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    color_primario = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    config_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_alta = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_baja = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestamista", x => x.prestamista_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                INSERT INTO prestamista (prestamista_id, slug, nombre_comercial, estatus, plan, moneda, zona_horaria, fecha_alta)
                VALUES (1, 'moneypine', 'MoneyPine', 'ACTIVO', 'MVP', 'MXN', 'America/Mexico_City', NOW());
            ");

            // =======================================================
            // 2) Adopta las tablas creadas por SQL crudo en Program.cs (y
            //    configuracion_sistema, huérfana) ANTES de alterarlas, para
            //    que esta migración no explote en un entorno limpio.
            //    DDL copiado literal de Program.cs líneas ~286-391.
            // =======================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS producto_ahorro (
                    id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    nombre     VARCHAR(100) NOT NULL,
                    tasa_anual DECIMAL(8,2) NOT NULL DEFAULT 0.00,
                    plazo_dias INT          NOT NULL DEFAULT 365,
                    descripcion VARCHAR(300) NULL,
                    activo     TINYINT(1)   NOT NULL DEFAULT 1,
                    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS cuenta_ahorro (
                    id                 INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    cliente_id         INT          NOT NULL,
                    producto_ahorro_id INT          NOT NULL,
                    monto_inicial      DECIMAL(12,2) NOT NULL DEFAULT 0.00,
                    saldo_actual       DECIMAL(12,2) NOT NULL DEFAULT 0.00,
                    fecha_apertura     DATE         NOT NULL,
                    fecha_vencimiento  DATE         NOT NULL,
                    estatus            VARCHAR(20)  NOT NULL DEFAULT 'ACTIVA',
                    ejecutivo_id       INT          NULL,
                    created_at         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT fk_ca_cliente   FOREIGN KEY (cliente_id)        REFERENCES cliente(cliente_id)   ON DELETE RESTRICT,
                    CONSTRAINT fk_ca_producto  FOREIGN KEY (producto_ahorro_id) REFERENCES producto_ahorro(id)  ON DELETE RESTRICT,
                    CONSTRAINT fk_ca_ejecutivo FOREIGN KEY (ejecutivo_id)       REFERENCES usuario(usuario_id)  ON DELETE SET NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS movimiento_ahorro (
                    id               INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    cuenta_ahorro_id INT          NOT NULL,
                    tipo             VARCHAR(20)  NOT NULL,
                    monto            DECIMAL(12,2) NOT NULL DEFAULT 0.00,
                    descripcion      VARCHAR(300) NULL,
                    fecha            DATE         NOT NULL,
                    created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT fk_ma_cuenta FOREIGN KEY (cuenta_ahorro_id) REFERENCES cuenta_ahorro(id) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS concepto_sistema (
                    id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    nombre     VARCHAR(100) NOT NULL,
                    tipo       VARCHAR(20)  NOT NULL DEFAULT 'GASTO',
                    activo     TINYINT(1)   NOT NULL DEFAULT 1,
                    orden      INT          NOT NULL DEFAULT 0,
                    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS buro_exclusion (
                    cliente_id   INT          NOT NULL PRIMARY KEY,
                    excluido_por INT          NULL,
                    fecha        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    motivo       VARCHAR(300) NOT NULL DEFAULT 'Excluido por administrador'
                );
            ");

            // MONEYPINE-MT: configuracion_sistema existe en Railway/local pero
            // ningún código la crea (deuda ajena a esta migración). Se adopta
            // igual: si no la creamos aquí, el ALTER de abajo también
            // explotaría en un entorno limpio.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS configuracion_sistema (
                    clave          VARCHAR(50)  NOT NULL PRIMARY KEY,
                    valor          VARCHAR(200) NOT NULL,
                    actualizado_en DATETIME     NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );
            ");

            // =======================================================
            // 3) Columnas que entran a un índice compuesto necesitan un tipo
            //    con longitud explícita — MySQL no indexa LONGTEXT sin prefijo.
            //    Sin riesgo de truncar datos: correo máx. 31 chars, clave_cliente
            //    máx. 8 chars, estatus es un enum corto (verificado en local).
            // =======================================================
            migrationBuilder.AlterColumn<string>(
                name: "correo",
                table: "usuario",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "estatus",
                table: "prestamo",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "ACTIVO",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "ACTIVO")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "clave_cliente",
                table: "cliente",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // =======================================================
            // 4) prestamista_id en las 27 tablas EF + configuracion_sistema.
            //    Patrón sin downtime: ADD COLUMN ... DEFAULT 1 (backfill
            //    automático de MySQL) -> UPDATE explícito (defensivo) ->
            //    DROP DEFAULT. Sin DROP DEFAULT un INSERT que olvide el
            //    tenant caería en silencio en el prestamista 1.
            // =======================================================
            AddTenantColumn(migrationBuilder, "usuario");
            AddTenantColumn(migrationBuilder, "solicitud_aprobacion");
            AddTenantColumn(migrationBuilder, "ruta");
            AddTenantColumn(migrationBuilder, "producto_credito");
            AddTenantColumn(migrationBuilder, "producto_ahorro");
            AddTenantColumn(migrationBuilder, "prestamo_aval");
            AddTenantColumn(migrationBuilder, "prestamo");
            AddTenantColumn(migrationBuilder, "periodo_amortizacion");
            AddTenantColumn(migrationBuilder, "pago_detalle");
            AddTenantColumn(migrationBuilder, "pago");
            AddTenantColumn(migrationBuilder, "notifications");
            AddTenantColumn(migrationBuilder, "notificacion_agendada");
            AddTenantColumn(migrationBuilder, "movimiento_ahorro");
            AddTenantColumn(migrationBuilder, "lista_negra");
            AddTenantColumn(migrationBuilder, "grupo");
            AddTenantColumn(migrationBuilder, "gestion_cobranza");
            AddTenantColumn(migrationBuilder, "gerencia");
            AddTenantColumn(migrationBuilder, "gastos_recientes");
            AddTenantColumn(migrationBuilder, "formato_documento");
            AddTenantColumn(migrationBuilder, "documento");
            AddTenantColumn(migrationBuilder, "cuenta_ahorro");
            AddTenantColumn(migrationBuilder, "concepto_sistema");
            AddTenantColumn(migrationBuilder, "cliente_anotacion");
            AddTenantColumn(migrationBuilder, "cliente");
            AddTenantColumn(migrationBuilder, "buro_exclusion");
            AddTenantColumn(migrationBuilder, "buro_auto_reporte");
            AddTenantColumn(migrationBuilder, "ActivityLogs");

            // configuracion_sistema no tiene entidad EF -> 100% SQL crudo.
            migrationBuilder.Sql("ALTER TABLE configuracion_sistema ADD COLUMN prestamista_id INT NOT NULL DEFAULT 1;");
            migrationBuilder.Sql("UPDATE configuracion_sistema SET prestamista_id = 1;");
            migrationBuilder.Sql("ALTER TABLE configuracion_sistema ALTER COLUMN prestamista_id DROP DEFAULT;");

            // =======================================================
            // 5) Índices — el tenant siempre va primero (Parte 4.3).
            // =======================================================
            migrationBuilder.CreateIndex(
                name: "ix_usuario_correo",
                table: "usuario",
                columns: new[] { "prestamista_id", "correo" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_aprobacion_prestamista_id",
                table: "solicitud_aprobacion",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ix_ruta_codigo",
                table: "ruta",
                columns: new[] { "prestamista_id", "codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_producto_credito_prestamista_id",
                table: "producto_credito",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_producto_ahorro_prestamista_id",
                table: "producto_ahorro",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamo_aval_prestamista_id",
                table: "prestamo_aval",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ix_prestamo_tenant",
                table: "prestamo",
                columns: new[] { "prestamista_id", "estatus", "fecha_proximo_pago" });

            migrationBuilder.CreateIndex(
                name: "IX_periodo_amortizacion_prestamista_id",
                table: "periodo_amortizacion",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_pago_detalle_prestamista_id",
                table: "pago_detalle",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ix_pago_tenant",
                table: "pago",
                columns: new[] { "prestamista_id", "fecha_pago" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_prestamista_id",
                table: "notifications",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_agendada_prestamista_id",
                table: "notificacion_agendada",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_ahorro_prestamista_id",
                table: "movimiento_ahorro",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_lista_negra_prestamista_id",
                table: "lista_negra",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_grupo_prestamista_id",
                table: "grupo",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_gestion_cobranza_prestamista_id",
                table: "gestion_cobranza",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ux_gerencia_codigo",
                table: "gerencia",
                columns: new[] { "prestamista_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gastos_recientes_prestamista_id",
                table: "gastos_recientes",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_formato_documento_prestamista_id",
                table: "formato_documento",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_prestamista_id",
                table: "documento",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_cuenta_ahorro_prestamista_id",
                table: "cuenta_ahorro",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_concepto_sistema_prestamista_id",
                table: "concepto_sistema",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_anotacion_prestamista_id",
                table: "cliente_anotacion",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_tenant",
                table: "cliente",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "ux_cliente_clave",
                table: "cliente",
                columns: new[] { "prestamista_id", "clave_cliente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_buro_exclusion_prestamista_id",
                table: "buro_exclusion",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_buro_auto_reporte_prestamista_id",
                table: "buro_auto_reporte",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_prestamista_id",
                table: "ActivityLogs",
                column: "prestamista_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamista_slug",
                table: "prestamista",
                column: "slug",
                unique: true);

            migrationBuilder.Sql("CREATE INDEX IX_configuracion_sistema_prestamista_id ON configuracion_sistema (prestamista_id);");

            // =======================================================
            // 6) Foreign keys hacia prestamista. Restrict: borrar un tenant
            //    no debe cascadear en silencio sobre miles de filas.
            // =======================================================
            AddTenantForeignKey(migrationBuilder, "ActivityLogs");
            AddTenantForeignKey(migrationBuilder, "buro_auto_reporte");
            AddTenantForeignKey(migrationBuilder, "buro_exclusion");
            AddTenantForeignKey(migrationBuilder, "cliente");
            AddTenantForeignKey(migrationBuilder, "cliente_anotacion");
            AddTenantForeignKey(migrationBuilder, "concepto_sistema");
            AddTenantForeignKey(migrationBuilder, "cuenta_ahorro");
            AddTenantForeignKey(migrationBuilder, "documento");
            AddTenantForeignKey(migrationBuilder, "formato_documento");
            AddTenantForeignKey(migrationBuilder, "gastos_recientes");
            AddTenantForeignKey(migrationBuilder, "gerencia");
            AddTenantForeignKey(migrationBuilder, "gestion_cobranza");
            AddTenantForeignKey(migrationBuilder, "grupo");
            AddTenantForeignKey(migrationBuilder, "lista_negra");
            AddTenantForeignKey(migrationBuilder, "movimiento_ahorro");
            AddTenantForeignKey(migrationBuilder, "notificacion_agendada");
            AddTenantForeignKey(migrationBuilder, "notifications");
            AddTenantForeignKey(migrationBuilder, "pago");
            AddTenantForeignKey(migrationBuilder, "pago_detalle");
            AddTenantForeignKey(migrationBuilder, "periodo_amortizacion");
            AddTenantForeignKey(migrationBuilder, "prestamo");
            AddTenantForeignKey(migrationBuilder, "prestamo_aval");
            AddTenantForeignKey(migrationBuilder, "producto_ahorro");
            AddTenantForeignKey(migrationBuilder, "producto_credito");
            AddTenantForeignKey(migrationBuilder, "ruta");
            AddTenantForeignKey(migrationBuilder, "solicitud_aprobacion");
            AddTenantForeignKey(migrationBuilder, "usuario");

            migrationBuilder.Sql(@"
                ALTER TABLE configuracion_sistema
                ADD CONSTRAINT FK_configuracion_sistema_prestamista_prestamista_id
                FOREIGN KEY (prestamista_id) REFERENCES prestamista (prestamista_id) ON DELETE RESTRICT;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE configuracion_sistema DROP FOREIGN KEY FK_configuracion_sistema_prestamista_prestamista_id;");

            migrationBuilder.DropForeignKey(name: "FK_ActivityLogs_prestamista_prestamista_id", table: "ActivityLogs");
            migrationBuilder.DropForeignKey(name: "FK_buro_auto_reporte_prestamista_prestamista_id", table: "buro_auto_reporte");
            migrationBuilder.DropForeignKey(name: "FK_buro_exclusion_prestamista_prestamista_id", table: "buro_exclusion");
            migrationBuilder.DropForeignKey(name: "FK_cliente_prestamista_prestamista_id", table: "cliente");
            migrationBuilder.DropForeignKey(name: "FK_cliente_anotacion_prestamista_prestamista_id", table: "cliente_anotacion");
            migrationBuilder.DropForeignKey(name: "FK_concepto_sistema_prestamista_prestamista_id", table: "concepto_sistema");
            migrationBuilder.DropForeignKey(name: "FK_cuenta_ahorro_prestamista_prestamista_id", table: "cuenta_ahorro");
            migrationBuilder.DropForeignKey(name: "FK_documento_prestamista_prestamista_id", table: "documento");
            migrationBuilder.DropForeignKey(name: "FK_formato_documento_prestamista_prestamista_id", table: "formato_documento");
            migrationBuilder.DropForeignKey(name: "FK_gastos_recientes_prestamista_prestamista_id", table: "gastos_recientes");
            migrationBuilder.DropForeignKey(name: "FK_gerencia_prestamista_prestamista_id", table: "gerencia");
            migrationBuilder.DropForeignKey(name: "FK_gestion_cobranza_prestamista_prestamista_id", table: "gestion_cobranza");
            migrationBuilder.DropForeignKey(name: "FK_grupo_prestamista_prestamista_id", table: "grupo");
            migrationBuilder.DropForeignKey(name: "FK_lista_negra_prestamista_prestamista_id", table: "lista_negra");
            migrationBuilder.DropForeignKey(name: "FK_movimiento_ahorro_prestamista_prestamista_id", table: "movimiento_ahorro");
            migrationBuilder.DropForeignKey(name: "FK_notificacion_agendada_prestamista_prestamista_id", table: "notificacion_agendada");
            migrationBuilder.DropForeignKey(name: "FK_notifications_prestamista_prestamista_id", table: "notifications");
            migrationBuilder.DropForeignKey(name: "FK_pago_prestamista_prestamista_id", table: "pago");
            migrationBuilder.DropForeignKey(name: "FK_pago_detalle_prestamista_prestamista_id", table: "pago_detalle");
            migrationBuilder.DropForeignKey(name: "FK_periodo_amortizacion_prestamista_prestamista_id", table: "periodo_amortizacion");
            migrationBuilder.DropForeignKey(name: "FK_prestamo_prestamista_prestamista_id", table: "prestamo");
            migrationBuilder.DropForeignKey(name: "FK_prestamo_aval_prestamista_prestamista_id", table: "prestamo_aval");
            migrationBuilder.DropForeignKey(name: "FK_producto_ahorro_prestamista_prestamista_id", table: "producto_ahorro");
            migrationBuilder.DropForeignKey(name: "FK_producto_credito_prestamista_prestamista_id", table: "producto_credito");
            migrationBuilder.DropForeignKey(name: "FK_ruta_prestamista_prestamista_id", table: "ruta");
            migrationBuilder.DropForeignKey(name: "FK_solicitud_aprobacion_prestamista_prestamista_id", table: "solicitud_aprobacion");
            migrationBuilder.DropForeignKey(name: "FK_usuario_prestamista_prestamista_id", table: "usuario");

            migrationBuilder.DropTable(name: "prestamista");

            migrationBuilder.Sql("DROP INDEX IX_configuracion_sistema_prestamista_id ON configuracion_sistema;");

            migrationBuilder.DropIndex(name: "ix_usuario_correo", table: "usuario");
            migrationBuilder.DropIndex(name: "IX_solicitud_aprobacion_prestamista_id", table: "solicitud_aprobacion");
            migrationBuilder.DropIndex(name: "ix_ruta_codigo", table: "ruta");
            migrationBuilder.DropIndex(name: "IX_producto_credito_prestamista_id", table: "producto_credito");
            migrationBuilder.DropIndex(name: "IX_producto_ahorro_prestamista_id", table: "producto_ahorro");
            migrationBuilder.DropIndex(name: "IX_prestamo_aval_prestamista_id", table: "prestamo_aval");
            migrationBuilder.DropIndex(name: "ix_prestamo_tenant", table: "prestamo");
            migrationBuilder.DropIndex(name: "IX_periodo_amortizacion_prestamista_id", table: "periodo_amortizacion");
            migrationBuilder.DropIndex(name: "IX_pago_detalle_prestamista_id", table: "pago_detalle");
            migrationBuilder.DropIndex(name: "ix_pago_tenant", table: "pago");
            migrationBuilder.DropIndex(name: "IX_notifications_prestamista_id", table: "notifications");
            migrationBuilder.DropIndex(name: "IX_notificacion_agendada_prestamista_id", table: "notificacion_agendada");
            migrationBuilder.DropIndex(name: "IX_movimiento_ahorro_prestamista_id", table: "movimiento_ahorro");
            migrationBuilder.DropIndex(name: "IX_lista_negra_prestamista_id", table: "lista_negra");
            migrationBuilder.DropIndex(name: "IX_grupo_prestamista_id", table: "grupo");
            migrationBuilder.DropIndex(name: "IX_gestion_cobranza_prestamista_id", table: "gestion_cobranza");
            migrationBuilder.DropIndex(name: "ux_gerencia_codigo", table: "gerencia");
            migrationBuilder.DropIndex(name: "IX_gastos_recientes_prestamista_id", table: "gastos_recientes");
            migrationBuilder.DropIndex(name: "IX_formato_documento_prestamista_id", table: "formato_documento");
            migrationBuilder.DropIndex(name: "IX_documento_prestamista_id", table: "documento");
            migrationBuilder.DropIndex(name: "IX_cuenta_ahorro_prestamista_id", table: "cuenta_ahorro");
            migrationBuilder.DropIndex(name: "IX_concepto_sistema_prestamista_id", table: "concepto_sistema");
            migrationBuilder.DropIndex(name: "IX_cliente_anotacion_prestamista_id", table: "cliente_anotacion");
            migrationBuilder.DropIndex(name: "ix_cliente_tenant", table: "cliente");
            migrationBuilder.DropIndex(name: "ux_cliente_clave", table: "cliente");
            migrationBuilder.DropIndex(name: "IX_buro_exclusion_prestamista_id", table: "buro_exclusion");
            migrationBuilder.DropIndex(name: "IX_buro_auto_reporte_prestamista_id", table: "buro_auto_reporte");
            migrationBuilder.DropIndex(name: "IX_ActivityLogs_prestamista_id", table: "ActivityLogs");

            migrationBuilder.Sql("ALTER TABLE configuracion_sistema DROP COLUMN prestamista_id;");

            migrationBuilder.DropColumn(name: "prestamista_id", table: "usuario");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "solicitud_aprobacion");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "ruta");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "producto_credito");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "producto_ahorro");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "prestamo_aval");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "prestamo");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "periodo_amortizacion");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "pago_detalle");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "pago");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "notifications");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "notificacion_agendada");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "movimiento_ahorro");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "lista_negra");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "grupo");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "gestion_cobranza");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "gerencia");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "gastos_recientes");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "formato_documento");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "documento");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "cuenta_ahorro");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "concepto_sistema");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "cliente_anotacion");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "cliente");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "buro_exclusion");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "buro_auto_reporte");
            migrationBuilder.DropColumn(name: "prestamista_id", table: "ActivityLogs");

            migrationBuilder.AlterColumn<string>(
                name: "correo",
                table: "usuario",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "estatus",
                table: "prestamo",
                type: "longtext",
                nullable: false,
                defaultValue: "ACTIVO",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldDefaultValue: "ACTIVO")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "clave_cliente",
                table: "cliente",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // MONEYPINE-MT: no se hace DROP TABLE de las tablas adoptadas
            // (producto_ahorro, cuenta_ahorro, movimiento_ahorro,
            // concepto_sistema, buro_exclusion, configuracion_sistema). El
            // CREATE TABLE IF NOT EXISTS del Up() es un adopt, no una
            // creación que esta migración deba deshacer — igual que el
            // propio Program.cs, es idempotente y no reversible por diseño.
        }

        // MONEYPINE-MT: helper para no repetir 27 veces el patrón
        // AddColumn(DEFAULT 1) -> UPDATE -> DROP DEFAULT.
        private static void AddTenantColumn(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<int>(
                name: "prestamista_id",
                table: table,
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql($"UPDATE `{table}` SET prestamista_id = 1;");
            migrationBuilder.Sql($"ALTER TABLE `{table}` ALTER COLUMN prestamista_id DROP DEFAULT;");
        }

        private static void AddTenantForeignKey(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddForeignKey(
                name: $"FK_{table}_prestamista_prestamista_id",
                table: table,
                column: "prestamista_id",
                principalTable: "prestamista",
                principalColumn: "prestamista_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
