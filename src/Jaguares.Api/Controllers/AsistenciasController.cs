using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Jaguares.Api.Data;
using Jaguares.Api.Services;
using Jaguares.Shared.Models;
using Jaguares.Shared.Dtos;

namespace Jaguares.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = TokenService.RoleAdmin)]
    public class AsistenciasController : ControllerBase
    {
        private readonly AcuaticaContext _context;
        private readonly WhatsAppService _whatsapp;

        public AsistenciasController(AcuaticaContext context, WhatsAppService whatsapp)
        {
            _context = context;
            _whatsapp = whatsapp;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAsistencias()
        {
            return await _context.Asistencias
                .Include(a => a.Alumno)
                .OrderByDescending(a => a.Fecha)
                .Select(a => new {
                    id = a.Id,
                    a.Fecha,
                    NombreCompleto = a.Alumno != null ? a.Alumno.NombreCompleto : "Desconocido",
                    Telefono = a.Alumno != null ? a.Alumno.Telefono : string.Empty,
                    a.Asistio,
                    a.Nota
                })
                .Take(50)
                .ToListAsync();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsistencia(int id)
        {
            var asistencia = await _context.Asistencias.FirstOrDefaultAsync(a => a.Id == id);
            if (asistencia == null) return NotFound();

            _context.Asistencias.Remove(asistencia);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<object>> PostAsistencia([FromBody] AsistenciaCreateRequest request)
        {
            if (request == null || request.AlumnoId <= 0)
            {
                return BadRequest("AlumnoId es requerido y debe ser válido.");
            }

            var alumno = await _context.Alumnos.FindAsync(request.AlumnoId);
            if (alumno == null)
            {
                return NotFound("Alumno no encontrado.");
            }

            var asistencia = new Asistencia
            {
                AlumnoId = request.AlumnoId,
                Fecha = request.Fecha == default ? DateTime.UtcNow : request.Fecha.ToUniversalTime(),
                Asistio = request.Asistio,
                Nota = request.Nota ?? string.Empty
            };

            _context.Asistencias.Add(asistencia);
            await _context.SaveChangesAsync();

            // Envío opcional de WhatsApp (sin bloquear)
            if (asistencia.Asistio)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _whatsapp.EnviarMensaje(alumno.Telefono,
                            $"Hola {alumno.NombreCompleto}, se ha registrado tu asistencia el {asistencia.Fecha:dd/MM/yyyy}. ¡A darle con todo! 🐆");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error enviando WhatsApp: {ex.Message}");
                    }
                });
            }

            return Ok(new { asistencia.Id, asistencia.Fecha, asistencia.Asistio });
        }
    }
}
