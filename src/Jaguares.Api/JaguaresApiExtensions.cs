using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Jaguares.Api.Data;
using Jaguares.Api.Services;
using Jaguares.Shared.Models;

namespace Jaguares.Api
{
    /// <summary>
    /// Configuración reutilizable de la API de Jaguares Swim.
    /// La usan tanto el host independiente (Jaguares.Api) como el host combinado (Jaguares.Host),
    /// de modo que la API queda empaquetada y se puede extraer a otro servicio en el futuro.
    /// </summary>
    public static class JaguaresApiExtensions
    {
        public const string CorsPolicy = "FrontendPolicy";

        /// <summary>Registra todos los servicios de la API (DB, JWT, CORS, controladores, swagger, negocio).</summary>
        public static IServiceCollection AddJaguaresApi(this IServiceCollection services, bool isDevelopment, IConfiguration configuration)
        {
            // Controladores. AddApplicationPart asegura que los controladores se descubran
            // aunque la API se aloje desde otro proyecto (Jaguares.Host).
            services.AddControllers()
                .AddApplicationPart(typeof(JaguaresApiExtensions).Assembly);

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Servicios de negocio y automatización
            services.AddSingleton<WhatsAppService>();
            services.AddSingleton<TokenService>();
            services.AddHostedService<BillingService>();

            // Base de datos SQL Server (hosting en monsterasp / databaseasp.net).
            var connectionString = isDevelopment
                ? configuration.GetConnectionString("Remote") // LocalDB para desarrollo local (appsettings.Development.json).
                : configuration.GetConnectionString("Default"); // SQL Server remoto para producción (appsettings.json).

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("No se encontró la cadena de conexión a la base de datos. Verifique la configuración en appsettings.json o variables de entorno.");
            }

            // Log de arranque: muestra a qué servidor/BD vamos a conectar (SIN exponer la contraseña).
            try
            {
                var csb = new SqlConnectionStringBuilder(connectionString);
                Console.WriteLine($"[DB] Servidor: {csb.DataSource} | Base: {csb.InitialCatalog} | Connect Timeout: {csb.ConnectTimeout}s | Encrypt: {csb.Encrypt}");
            }
            catch { /* si la cadena no es parseable, lo veremos al conectar */ }

            services.AddDbContext<AcuaticaContext>(options =>
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));
                // Mensajes de error de EF más detallados (útil al depurar la conexión/consultas).
                options.EnableDetailedErrors();
            });

            // Autenticación JWT (Bearer): permite separar frontend y backend en distintos orígenes.
            var jwtSection = configuration.GetSection("Jwt");
            var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Falta la configuración Jwt:Key en appsettings.");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSection["Issuer"],
                        ValidAudience = jwtSection["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            services.AddAuthorization();

            // CORS: orígenes del frontend definidos en configuración (no se usa cuando todo es mismo origen).
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                 ?? new[] { "http://localhost:5000", "https://localhost:5001" };

            services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicy, policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            return services;
        }

        /// <summary>
        /// Configura el pipeline de la API. Si <paramref name="serveFrontend"/> es true, además
        /// sirve los archivos estáticos del frontend (index.html y compañía) desde wwwroot.
        /// </summary>
        public static WebApplication UseJaguaresApi(this WebApplication app, bool serveFrontend)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Jaguares.Api");

            // Middleware de tiempos: registra cada petición a la API con su duración y código de estado.
            // Así puedes ver en la consola cuánto tarda /api/Clases (suele ser la conexión a la BD remota).
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var esApi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
                var cronometro = esApi ? Stopwatch.StartNew() : null;

                if (esApi)
                    logger.LogInformation("--> {Method} {Path}", context.Request.Method, path);

                await next();

                if (esApi && cronometro != null)
                {
                    cronometro.Stop();
                    logger.LogInformation("<-- {Method} {Path} respondió {Status} en {Elapsed} ms",
                        context.Request.Method, path, context.Response.StatusCode, cronometro.ElapsedMilliseconds);
                }
            });

            // Middleware global de errores (responde JSON).
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Ocurrió un error en el servidor de Jaguares",
                        detail = ex.Message,
                        timestamp = DateTime.UtcNow
                    });
                }
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors(CorsPolicy);

            // Servir archivos estáticos desde wwwroot:
            //  - Siempre: comprobantes subidos (uploads/comprobantes).
            //  - Si serveFrontend: además el sitio (index.html, admin.html, etc.).
            // En el host combinado los archivos del sitio se copian a la carpeta de salida,
            // por eso usamos AppContext.BaseDirectory (idéntico en local y publicado).
            // Alineamos WebRootPath para que las subidas se guarden y se sirvan del mismo lugar.
            var wwwroot = serveFrontend
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : Path.Combine(app.Environment.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(Path.Combine(wwwroot, "uploads", "comprobantes"));
            app.Environment.WebRootPath = wwwroot;
            var fileProvider = new PhysicalFileProvider(wwwroot);

            if (serveFrontend)
            {
                app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            }
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            MigrarYSembrar(app);

            return app;
        }

        /// <summary>Aplica migraciones pendientes y siembra las modalidades iniciales.</summary>
        public static void MigrarYSembrar(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Jaguares.Db");
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<AcuaticaContext>();

                // 1) Probar la conexión y medir cuánto tarda (la BD remota gratuita suele ser lenta).
                logger.LogInformation("[DB] Probando conexión a la base de datos...");
                var swConexion = Stopwatch.StartNew();
                var puedeConectar = context.Database.CanConnect();
                swConexion.Stop();
                logger.LogInformation("[DB] CanConnect = {Ok} (tardó {Ms} ms)", puedeConectar, swConexion.ElapsedMilliseconds);

                // 2) Aplicar migraciones pendientes.
                logger.LogInformation("[DB] Aplicando migraciones pendientes...");
                var swMigrar = Stopwatch.StartNew();
                context.Database.Migrate();
                swMigrar.Stop();
                logger.LogInformation("[DB] Migraciones aplicadas en {Ms} ms", swMigrar.ElapsedMilliseconds);

                var totalClases = context.Clases.Count();
                logger.LogInformation("[DB] Modalidades existentes: {Count}", totalClases);

                if (totalClases < 3)
                {
                    var clasesParaAgregar = new List<Clase>
                    {
                        new Clase { Id = 1, Horario = "🔹 Modalidad #1 – 2 clases por semana", Costo = 800m, Nivel = "Principiante", CupoMaximo = 14, Descripcion = "📆 Días: A elegir\n💰 Costo: $800 (8 clases en 4 semanas)" },
                        new Clase { Id = 2, Horario = "🔹 Modalidad #2 – 3 clases por semana", Costo = 900m, Nivel = "Intermedio", CupoMaximo = 12, Descripcion = "📆 Días: A elegir\n💰 Costo: $900 (12 clases en 4 semanas)" },
                        new Clase { Id = 3, Horario = "🔹 Modalidad #3 – Clases de lunes a viernes", Costo = 1160m, Nivel = "Avanzado", CupoMaximo = 10, Descripcion = "📆 Días: Lunes a viernes\n💰 Costo: $1,160 (20 clases en 4 semanas)" }
                    };

                    foreach (var clase in clasesParaAgregar)
                    {
                        if (!context.Clases.Any(c => c.Id == clase.Id))
                        {
                            context.Clases.Add(clase);
                        }
                    }
                    context.SaveChanges();
                    logger.LogInformation("[DB] Modalidades iniciales cargadas con éxito.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DB] ERROR CRÍTICO EN INICIO al conectar/migrar la base de datos");
            }
        }
    }
}
