using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
        public static IServiceCollection AddJaguaresApi(this IServiceCollection services, IConfiguration configuration)
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
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default en appsettings.");
            services.AddDbContext<AcuaticaContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

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
            // Middleware global de errores (responde JSON).
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
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
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<AcuaticaContext>();

                Console.WriteLine("Verificando base de datos...");
                context.Database.Migrate();

                if (context.Clases.Count() < 3)
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
                    Console.WriteLine("Modalidades iniciales cargadas con éxito.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("---------- ERROR CRÍTICO EN INICIO ----------");
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null) Console.WriteLine(ex.InnerException.Message);
                Console.WriteLine("---------------------------------------------");
            }
        }
    }
}
