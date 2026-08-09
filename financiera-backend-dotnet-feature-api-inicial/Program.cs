using ApiEjemplo.Data;
using ApiEjemplo.Middleware;
using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

// =======================
// CORS (AGREGADO)
// =======================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            // MONEYPINE-FIX: permite localhost, Railway, Vercel y dominio producción
            var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? new[] {
                    "http://localhost:3000",
                    "http://localhost:5173",
                    "https://moneypine-frontend.vercel.app",
                    "https://moneypine.com.mx",
                    "https://www.moneypine.com.mx"
                };

            policy
                .WithOrigins(allowedOrigins)
                // MONEYPINE-FIX: en desarrollo se acepta cualquier puerto de
                // localhost. Vite salta a 5174/5175 cuando el 5173 esta ocupado
                // y el navegador bloqueaba el login sin explicar por que: el
                // usuario solo veia "no se pudo conectar con el servidor".
                // La lista fija sigue siendo la unica valida en produccion.
                .SetIsOriginAllowed(origin =>
                {
                    if (allowedOrigins.Contains(origin)) return true;
                    if (!builder.Environment.IsDevelopment()) return false;
                    return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                        && (uri.Host == "localhost" || uri.Host == "127.0.0.1");
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Controllers (MVC)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// MySQL + Entity Framework
var connectionString = Environment.GetEnvironmentVariable("MySqlConnection") 
    ?? builder.Configuration.GetConnectionString("MySqlConnection");

// MONEYPINE-FIX: asegurar utf8mb4 para caracteres españoles (Ñ, acentos, etc.)
if (!string.IsNullOrEmpty(connectionString) &&
    !connectionString.Contains("charset=", StringComparison.OrdinalIgnoreCase))
    connectionString += ";charset=utf8mb4";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 32))
    )
);

// =======================
// JWT AUTHENTICATION
// =======================
var jwtKey = builder.Configuration["Jwt:Key"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey!))
    };
});

// Servicio de Notificaciones
builder.Services.AddScoped<NotificationService>();

builder.Services.AddAuthorization();

// MONEYPINE-FIX: los 403 salian con el cuerpo vacio y el usuario solo veia
// "Error 403". Este handler los responde con una explicacion en espanol y,
// cuando la accion se puede pedir, indica que existe el camino de solicitud.
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    ApiEjemplo.Security.RespuestaPermisoHandler>();

// Servicio para registrar actividades del sistema (pagos, créditos, atrasos)
builder.Services.AddScoped<ActivityService>();

// Subida de Documentos
builder.Services.AddScoped<DocumentService>();

// Servicios de negocio
builder.Services.AddScoped<MoraService>();
builder.Services.AddScoped<InversionService>();
builder.Services.AddScoped<CreditosOtorgadosService>();
builder.Services.AddScoped<PromedioCreditosPagadosService>();
builder.Services.AddScoped<TotalCreditosPagadosService>();
builder.Services.AddScoped<TotalGanadoPorPeriodoService>();
builder.Services.AddScoped<AplicacionPagoService>();
builder.Services.AddScoped<MotorRecalculoPrestamoService>();
builder.Services.AddScoped<ListaNegraService>();

// Peticiones de roles no-ADMIN para ejecutar acciones sensibles
builder.Services.AddScoped<SolicitudAprobacionService>();

// MONEYPINE-FIX: cron job diario — reporta automáticamente a buró los créditos con mora >= 90 días
builder.Services.AddHostedService<BuroAutoReporteService>();

// MONEYPINE-FIX: cron job diario — recalcula el estatus (ACTIVO/ATRASADO/LIQUIDADO)
// de todos los préstamos ACTIVO, para que un vencimiento no se quede "invisible"
// en la BD si nadie toca ese crédito el mismo día que vence.
builder.Services.AddHostedService<EstatusPrestamoSweepService>();

// MONEYPINE-FIX: cron job diario — sincroniza la lista negra. Antes dependia de
// que alguien pulsara el boton, y llevaba tanto sin correr que faltaban 158
// altas y los dias de mora estaban a anos de la realidad.
builder.Services.AddHostedService<ListaNegraSyncService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT así: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Pipeline

// MONEYPINE-FIX: exception handler ANTES de CORS para que los 500 incluyan el header CORS
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Error interno del servidor",
            message = ex?.Error?.Message,
            inner = ex?.Error?.InnerException?.Message
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//app.UseHttpsRedirection();

// Permitir servir archivos estáticos (uploads, imágenes, etc.)
app.UseStaticFiles();

// =======================
// CORS (DEBE IR AQUÍ)
// =======================
app.UseCors("AllowFrontend");

// ORDEN IMPORTANTE
app.UseAuthentication();
app.UseAuthorization();

// MONEYPINE-FIX: registra la actividad del usuario autenticado (Reportes/Trabajadores conectados)
app.UseMiddleware<PresenceTrackingMiddleware>();

// Habilitar Controllers
app.MapControllers();

// =======================
// MIGRACIONES AUTOMÁTICAS
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// =======================
// MONEYPINE-FIX: recalcular saldo_actual de todos los préstamos desde sus periodos pendientes
// Corrige saldos desincronizados causados por pagos previos con lógica errónea
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var prestamos = await db.Prestamos.ToListAsync();
    foreach (var p in prestamos)
    {
        // abono_capital = capital que aporta ese periodo; sumar = capital total pendiente de pagar
        var saldoPendiente = await db.PeriodosAmortizacion
            .Where(pa => pa.prestamo_id == p.prestamo_id && pa.estado_pago == 1)
            .SumAsync(pa => (decimal?)pa.abono_capital) ?? 0;

        if (saldoPendiente > 0)
            p.saldo_actual = saldoPendiente;
    }
    await db.SaveChangesAsync();
}

// =======================
// MONEYPINE-FIX: crear tablas del módulo de ahorro si no existen
// MySQL 9.4 en Railway soporta CREATE TABLE IF NOT EXISTS (idempotente)
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.ExecuteSqlRawAsync(@"
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

    await db.Database.ExecuteSqlRawAsync(@"
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

    await db.Database.ExecuteSqlRawAsync(@"
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

    // Seed: insertar productos de ahorro solo si la tabla está vacía
    var count = await db.ProductosAhorro.CountAsync();
    if (count == 0)
    {
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO producto_ahorro (nombre, tasa_anual, plazo_dias, descripcion) VALUES
              ('AHORRO ORDINARIO',    0.00,  365, 'Ahorro sin rendimiento, plazo anual'),
              ('AHORRO CRECEMAX',    10.00,  180, 'Rendimiento del 10% anual, plazo 180 dias'),
              ('Inversion 60',       60.00,   60, 'Rendimiento del 60% anual, plazo 60 dias'),
              ('Ahorro CreceMax 120',120.00, 120, 'Rendimiento del 120% anual, plazo 120 dias');
        ");
    }
}

// =======================
// MONEYPINE-FIX: crear tabla concepto_sistema si no existe + seed de 8 gastos base
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS concepto_sistema (
            id         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
            nombre     VARCHAR(100) NOT NULL,
            tipo       VARCHAR(20)  NOT NULL DEFAULT 'GASTO',
            activo     TINYINT(1)   NOT NULL DEFAULT 1,
            orden      INT          NOT NULL DEFAULT 0,
            created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
    ");

    var count = await db.ConceptosSistema.CountAsync();
    if (count == 0)
    {
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO concepto_sistema (nombre, tipo, orden) VALUES
              ('Alimento',          'GASTO', 1),
              ('Luz',               'GASTO', 2),
              ('Teléfono',          'GASTO', 3),
              ('Transporte',        'GASTO', 4),
              ('Renta',             'GASTO', 5),
              ('Inversión negocio', 'GASTO', 6),
              ('Créditos',          'GASTO', 7),
              ('Otros',             'GASTO', 8);
        ");
    }
}

// =======================
// MONEYPINE-FIX: tabla buro_exclusion — persiste qué clientes fueron quitados del buró
// Permite que un admin quite a un cliente y el cambio se refleje en todos los admins
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS buro_exclusion (
            cliente_id   INT          NOT NULL PRIMARY KEY,
            excluido_por INT          NULL,
            fecha        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            motivo       VARCHAR(300) NOT NULL DEFAULT 'Excluido por administrador'
        );
    ");
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.Run();