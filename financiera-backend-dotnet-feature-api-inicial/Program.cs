using ApiEjemplo.Data;
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

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.Run();