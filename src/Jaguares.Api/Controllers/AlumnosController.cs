using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class AlumnosController : ControllerBase
    {
        private readonly AcuaticaContext _context;
        private readonly WhatsAppService _whatsapp;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher<Alumno> _passwordHasher = new();

        public AlumnosController(AcuaticaContext context, WhatsAppService whatsapp, TokenService tokenService)
        {
            _context = context;
            _whatsapp = whatsapp;
            _tokenService = tokenService;
        }

        // 1. OBTENER ALUMNOS (Para el panel privado 'admin.html')
        [HttpGet]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<ActionResult<IEnumerable<object>>> GetAlumnos()
        {
            var alumnos = await _context.Alumnos.Include(a => a.Clase).OrderBy(a => a.NombreCompleto).ToListAsync();
            var pagos = await _context.Pagos.ToListAsync();
            var hoy = DateTime.UtcNow.Date;
            var fechaLimiteInactividad = hoy.AddDays(-12);

            var resultado = alumnos.Select(a => {
                var pagosAlumno = pagos.Where(p => p.AlumnoId == a.Id).ToList();

                // Calculamos el estado en tiempo real basado en los pagos
                // Un alumno es INACTIVO (Rojo) si tiene algún pago con más de 12 días de retraso
                bool tieneMoraCritica = pagosAlumno.Any(p => !p.EstaPagado && p.FechaVencimiento.Date <= fechaLimiteInactividad);
                bool esInactivo = !a.Activo || tieneMoraCritica;

                // Un alumno tiene pagos PENDIENTES (Naranja) si tiene cualquier recibo sin pagar
                bool tienePagosPendientes = pagosAlumno.Any(p => !p.EstaPagado);

                return new {
                    id = a.Id,
                    nombreCompleto = a.NombreCompleto,
                    telefono = a.Telefono,
                    claseId = a.ClaseId,
                    activo = !esInactivo, // Para la UI: true = Verde o Naranja, false = Rojo
                    modalidadElegida = a.Clase?.Horario ?? "Sin modalidad",
                    precioMensual = a.Clase?.Costo ?? 0,
                    ultimoPagoPagado = !tienePagosPendientes // true = Al día (Verde), false = Pendiente (Naranja)
                };
            });

            return Ok(resultado);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<IActionResult> DeleteAlumno(int id)
        {
            try
            {
                var alumno = await _context.Alumnos.FindAsync(id);
                if (alumno == null) return NotFound("El alumno no existe.");

                // 1. Eliminar registros relacionados (Pagos y Asistencias) de forma explícita
                var pagosRelacionados = await _context.Pagos.Where(p => p.AlumnoId == id).ToListAsync();
                if (pagosRelacionados.Any()) _context.Pagos.RemoveRange(pagosRelacionados);

                var asistenciasRelacionadas = await _context.Asistencias.Where(a => a.AlumnoId == id).ToListAsync();
                if (asistenciasRelacionadas.Any()) _context.Asistencias.RemoveRange(asistenciasRelacionadas);

                // 2. Eliminar al alumno
                _context.Alumnos.Remove(alumno);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al eliminar de la base de datos: {ex.Message}");
            }
        }

        // 2. REGISTRAR UN NUEVO ALUMNO (Desde el formulario 'index.html')  -- público
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<object>> PostAlumno([FromBody] AlumnoCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return BadRequest("La contraseña debe tener al menos 6 caracteres.");
            }

            if (await _context.Alumnos.AnyAsync(a => a.Telefono == request.Telefono))
            {
                return BadRequest("Ya existe un alumno con este teléfono.");
            }

            var clase = await _context.Clases.FirstOrDefaultAsync(c => c.Id == request.ClaseId);
            if (clase == null)
            {
                return BadRequest("La modalidad seleccionada no es válida.");
            }

            var alumno = new Alumno
            {
                NombreCompleto = request.NombreCompleto,
                Telefono = request.Telefono,
                NivelNado = request.NivelNado ?? "General",
                ClaseId = request.ClaseId
            };

            alumno.PasswordHash = _passwordHasher.HashPassword(alumno, request.Password);
            _context.Alumnos.Add(alumno);
            await _context.SaveChangesAsync();

            var pago = new Pago
            {
                AlumnoId = alumno.Id,
                Monto = clase.Costo,
                FechaVencimiento = DateTime.UtcNow.AddDays(30),
                EstaPagado = false
            };
            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            // Envío automático de mensaje de bienvenida y datos de inscripción
            await _whatsapp.EnviarMensaje(alumno.Telefono, $"¡Bienvenido a Jaguares Swim 🐆, {alumno.NombreCompleto}! Tu inscripción ha sido exitosa en la {clase.Horario}. Puedes acceder a tu portal con tu teléfono y contraseña.");

            return Ok(new { alumno.Id, alumno.NombreCompleto, alumno.Telefono, alumno.ClaseId });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login([FromBody] AlumnoLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Telefono) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Teléfono y contraseña son obligatorios.");
            }

            var alumno = await _context.Alumnos.Include(a => a.Clase)
                .FirstOrDefaultAsync(a => a.Telefono == request.Telefono);
            if (alumno == null)
            {
                return Unauthorized("Credenciales inválidas.");
            }

            var verification = _passwordHasher.VerifyHashedPassword(alumno, alumno.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Credenciales inválidas.");
            }

            var token = _tokenService.GenerarTokenAlumno(alumno);
            return Ok(new { success = true, token, nombreCompleto = alumno.NombreCompleto });
        }

        // Con JWT el cierre de sesión se hace en el cliente (descartando el token).
        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            return Ok(new { success = true });
        }

        [HttpGet("me")]
        [Authorize(Roles = TokenService.RoleStudent)]
        public async Task<ActionResult<object>> Me()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var alumno = await _context.Alumnos.Include(a => a.Clase)
                .FirstOrDefaultAsync(a => a.Id == userId);
            if (alumno == null)
            {
                return Unauthorized("Usuario no encontrado.");
            }

            return Ok(await ConstruirPerfilAsync(alumno));
        }

        [HttpGet("consulta")]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<ActionResult<object>> GetAlumnoPorTelefono([FromQuery] string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
            {
                return BadRequest("El teléfono es obligatorio.");
            }

            var alumno = await _context.Alumnos
                .Include(a => a.Clase)
                .FirstOrDefaultAsync(a => a.Telefono == telefono);

            if (alumno == null)
            {
                return NotFound("No se encontró un alumno con ese teléfono.");
            }

            return Ok(await ConstruirPerfilAsync(alumno));
        }

        private async Task<object> ConstruirPerfilAsync(Alumno alumno)
        {
            var ultimoPago = await _context.Pagos
                .Where(p => p.AlumnoId == alumno.Id)
                .OrderByDescending(p => p.FechaVencimiento)
                .FirstOrDefaultAsync();

            var asistencias = await _context.Asistencias
                .Where(a => a.AlumnoId == alumno.Id)
                .OrderByDescending(a => a.Fecha)
                .Take(10)
                .Select(a => new { a.Fecha, a.Asistio, a.Nota })
                .ToListAsync();

            return new
            {
                alumno.Id,
                alumno.NombreCompleto,
                alumno.Telefono,
                Modalidad = alumno.Clase?.Horario,
                PrecioMensual = alumno.Clase?.Costo,
                Pago = ultimoPago == null ? null : new
                {
                    ultimoPago.Id,
                    ultimoPago.Monto,
                    ultimoPago.FechaVencimiento,
                    ultimoPago.EstaPagado
                },
                Asistencias = asistencias
            };
        }
    }
}
