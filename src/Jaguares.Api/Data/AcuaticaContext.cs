using Microsoft.EntityFrameworkCore;
using Jaguares.Shared.Models;

namespace Jaguares.Api.Data
{
    public class AcuaticaContext : DbContext
    {
        public AcuaticaContext(DbContextOptions<AcuaticaContext> options) : base(options) { }

        // Aquí le decimos que cree las tablas de Alumnos, Clases, Pagos y Asistencias
        public DbSet<Alumno> Alumnos { get; set; }
        public DbSet<Clase> Clases { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Asistencia> Asistencias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precisión explícita para montos en dinero (evita truncamiento en SQL Server)
            modelBuilder.Entity<Clase>().Property(c => c.Costo).HasPrecision(18, 2);
            modelBuilder.Entity<Pago>().Property(p => p.Monto).HasPrecision(18, 2);
        }
    }
}
