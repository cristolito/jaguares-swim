using Microsoft.EntityFrameworkCore;
using Jaguares.Api.Data;
using Jaguares.Shared.Models;

namespace Jaguares.Api.Services
{
    public class BillingService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public BillingService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Esperamos un poco al arranque para no competir con la migración inicial de la base de datos.
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                using (var scope = _services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AcuaticaContext>();
                    var whatsapp = scope.ServiceProvider.GetRequiredService<WhatsAppService>();

                    var hoy = DateTime.UtcNow.Date;
                    var alumnos = await context.Alumnos.Where(a => a.Activo).Include(a => a.Clase).ToListAsync();

                    // 1. Procesar Morosidad Crítica (Inactividad automática tras 12 días)
                    var fechaLimiteInactividad = hoy.AddDays(-12);
                    var pagosMuyVencidos = await context.Pagos
                        .Include(p => p.Alumno)
                        .Where(p => !p.EstaPagado && p.FechaVencimiento.Date <= fechaLimiteInactividad)
                        .ToListAsync();

                    foreach (var p in pagosMuyVencidos)
                    {
                        if (p.Alumno != null && p.Alumno.Activo)
                        {
                            p.Alumno.Activo = false; // Pasa a Inactivo automáticamente
                        }
                    }

                    // 2. Notificar Pagos Vencidos (Recordatorio normal)
                    var pagosVencidos = await context.Pagos
                        .Include(p => p.Alumno)
                        .Where(p => !p.EstaPagado && p.FechaVencimiento.Date < hoy)
                        .ToListAsync();

                    foreach (var pago in pagosVencidos)
                    {
                        if (pago.Alumno != null)
                        {
                            await whatsapp.EnviarMensaje(pago.Alumno.Telefono, $"⚠️ *Aviso de Pago Vencido* 🐆\nHola {pago.Alumno.NombreCompleto}, notamos que tu mensualidad de {pago.Monto:C0} ha vencido. Por favor, regulariza tu situación en el portal para evitar recargos.");
                        }
                    }

                    // 2. Generar cargos automáticos (3 días antes del vencimiento)
                    foreach (var alumno in alumnos)
                    {
                        var ultimoPago = await context.Pagos
                            .Where(p => p.AlumnoId == alumno.Id)
                            .OrderByDescending(p => p.FechaVencimiento)
                            .FirstOrDefaultAsync();

                        if (ultimoPago != null && ultimoPago.FechaVencimiento.Date <= hoy.AddDays(3))
                        {
                            // Evitar duplicados para el mismo mes
                            var yaExisteProximo = await context.Pagos.AnyAsync(p => p.AlumnoId == alumno.Id && p.FechaVencimiento > ultimoPago.FechaVencimiento);

                            if (!yaExisteProximo)
                            {
                                var nuevoPago = new Pago {
                                    AlumnoId = alumno.Id,
                                    Monto = alumno.Clase?.Costo ?? 0,
                                    FechaVencimiento = ultimoPago.FechaVencimiento.AddMonths(1),
                                    EstaPagado = false
                                };
                                context.Pagos.Add(nuevoPago);
                                await whatsapp.EnviarMensaje(alumno.Telefono, $"Hola {alumno.NombreCompleto}, se ha generado tu nueva mensualidad de Jaguares Swim. ¡No olvides realizar tu pago!");
                            }
                        }
                    }
                    await context.SaveChangesAsync();
                }
                }
                catch (Exception ex)
                {
                    // Un fallo en la facturación (p. ej. la BD no responde) NO debe tumbar la API.
                    Console.WriteLine($"[BillingService] Error en el ciclo de facturación: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); } // Ejecutar una vez al día
                catch (TaskCanceledException) { return; }
            }
        }
    }
}
