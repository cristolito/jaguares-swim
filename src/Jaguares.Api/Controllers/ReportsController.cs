using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Jaguares.Api.Data;
using Jaguares.Api.Services;
using System.Text;

namespace Jaguares.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = TokenService.RoleAdmin)]
    public class ReportsController : ControllerBase
    {
        private readonly AcuaticaContext _context;

        public ReportsController(AcuaticaContext context)
        {
            _context = context;
        }

        [HttpGet("pagos/excel")]
        public async Task<IActionResult> ExportPagosExcel([FromQuery] string? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var query = _context.Pagos.Include(p => p.Alumno).AsQueryable();

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado.Equals("pendiente", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => !p.EstaPagado);
                else if (estado.Equals("pagado", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.EstaPagado);
            }

            if (desde.HasValue)
                query = query.Where(p => p.FechaVencimiento.Date >= desde.Value.Date);

            if (hasta.HasValue)
                query = query.Where(p => p.FechaVencimiento.Date <= hasta.Value.Date);

            var pagos = await query.OrderByDescending(p => p.FechaVencimiento).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("ID,Alumno,Monto,Vencimiento,Estado");

            foreach (var p in pagos)
            {
                builder.AppendLine($"{p.Id},{p.Alumno?.NombreCompleto},{p.Monto},{p.FechaVencimiento:yyyy-MM-dd},{(p.EstaPagado ? "Pagado" : "Pendiente")}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "reporte_pagos.csv");
        }

        [HttpGet("asistencias/excel")]
        public async Task<IActionResult> ExportAsistenciasExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var query = _context.Asistencias.Include(a => a.Alumno).AsQueryable();

            if (desde.HasValue)
                query = query.Where(a => a.Fecha.Date >= desde.Value.Date);

            if (hasta.HasValue)
                query = query.Where(a => a.Fecha.Date <= hasta.Value.Date);

            var asistencias = await query.OrderByDescending(a => a.Fecha).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Fecha,Alumno,Asistio,Nota");

            foreach (var a in asistencias)
            {
                builder.AppendLine($"{a.Fecha:yyyy-MM-dd},{a.Alumno?.NombreCompleto},{(a.Asistio ? "SI" : "NO")},{a.Nota}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "reporte_asistencias.csv");
        }
    }
}
