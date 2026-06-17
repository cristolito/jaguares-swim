using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Jaguares.Api.Data;
using Jaguares.Api.Services;
using Jaguares.Shared.Models;

namespace Jaguares.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PagosController : ControllerBase
    {
        private readonly AcuaticaContext _context;
        private readonly WhatsAppService _whatsapp;
        private readonly IWebHostEnvironment _env;

        public PagosController(AcuaticaContext context, WhatsAppService whatsapp, IWebHostEnvironment env)
        {
            _context = context;
            _whatsapp = whatsapp;
            _env = env;
        }

        [HttpGet]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<ActionResult<IEnumerable<object>>> GetPagos([FromQuery] string? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var pagosQuery = _context.Pagos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado.Equals("pendiente", StringComparison.OrdinalIgnoreCase))
                    pagosQuery = pagosQuery.Where(p => !p.EstaPagado);
                else if (estado.Equals("pagado", StringComparison.OrdinalIgnoreCase))
                    pagosQuery = pagosQuery.Where(p => p.EstaPagado);
            }

            if (desde.HasValue)
                pagosQuery = pagosQuery.Where(p => p.FechaVencimiento.Date >= desde.Value.Date);

            if (hasta.HasValue)
                pagosQuery = pagosQuery.Where(p => p.FechaVencimiento.Date <= hasta.Value.Date);

            var pagos = await pagosQuery
                .Join(_context.Alumnos,
                    pago => pago.AlumnoId,
                    alumno => alumno.Id,
                    (pago, alumno) => new { pago, alumno })
                .Join(_context.Clases,
                    pa => pa.alumno.ClaseId,
                    clase => clase.Id,
                    (pa, clase) => new
                    {
                        pa.pago.Id,
                        pa.alumno.NombreCompleto,
                        pa.alumno.Telefono,
                        Modalidad = clase.Horario,
                        pa.pago.Monto,
                        pa.pago.FechaVencimiento,
                        pa.pago.EstaPagado,
                        pa.pago.ComprobanteUrl
                    })
                .ToListAsync();

            return Ok(pagos);
        }

        [HttpPost("{id}/subir-comprobante")]
        [Authorize(Roles = TokenService.RoleStudent)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> SubirComprobante(int id, IFormFile file)
        {
            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null) return NotFound("Pago no encontrado.");

            if (file == null || file.Length == 0) return BadRequest("Archivo no válido.");

            // Fallback en caso de que WebRootPath sea nulo en entornos de desarrollo
            var rootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var folderPath = Path.Combine(rootPath, "uploads", "comprobantes");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var fileName = $"pago_{id}_{DateTime.Now.Ticks}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            pago.ComprobanteUrl = $"/uploads/comprobantes/{fileName}";
            pago.FechaPago = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { url = $"/uploads/comprobantes/{fileName}" });
        }

        [HttpPut("{id}/marcar-pagado")]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<ActionResult> MarcarPagado(int id)
        {
            var pago = await _context.Pagos.Include(p => p.Alumno).FirstOrDefaultAsync(p => p.Id == id);
            if (pago == null)
            {
                return NotFound();
            }

            pago.EstaPagado = true;
            pago.FechaPago = DateTime.UtcNow;

            // Al marcar como pagado, el alumno se activa automáticamente
            if (pago.Alumno != null)
            {
                pago.Alumno.Activo = true;
            }

            await _context.SaveChangesAsync();

            if (pago.Alumno != null)
                await _whatsapp.EnviarMensaje(pago.Alumno.Telefono, $"✅ ¡Pago recibido! Hola {pago.Alumno.NombreCompleto}, hemos confirmado tu pago de {pago.Monto:C0}. Tu estatus ahora es: PAGADO.");

            return NoContent();
        }
    }
}
