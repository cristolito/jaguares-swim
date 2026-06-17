using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Jaguares.Api.Data;
using Jaguares.Api.Services;
using Jaguares.Shared.Models;

namespace Jaguares.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClasesController : ControllerBase
    {
        private readonly AcuaticaContext _context;

        public ClasesController(AcuaticaContext context)
        {
            _context = context;
        }

        // GET: api/Clases  (público: usado por la página de inscripción)
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Clase>>> GetClases()
        {
            try
            {
                // Intentar obtener desde la base de datos si existe la tabla
                var clasesDb = await _context.Clases.ToListAsync();
                if (clasesDb != null && clasesDb.Any())
                    return Ok(clasesDb);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener clases de DB: " + ex.Message);
            }

            return Ok(GetSeedClases());
        }

        [HttpPost]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<ActionResult<Clase>> PostClase([FromBody] Clase clase)
        {
            _context.Clases.Add(clase);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetClases), new { id = clase.Id }, clase);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<IActionResult> PutClase(int id, [FromBody] Clase clase)
        {
            clase.Id = id; // Sincronizar ID de la URL

            var dbClase = await _context.Clases.FindAsync(id);
            if (dbClase == null) return NotFound("La modalidad no existe.");

            dbClase.Horario = clase.Horario;
            dbClase.Costo = clase.Costo;
            dbClase.Nivel = clase.Nivel;
            dbClase.CupoMaximo = clase.CupoMaximo;
            dbClase.Descripcion = clase.Descripcion;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = TokenService.RoleAdmin)]
        public async Task<IActionResult> DeleteClase(int id)
        {
            var clase = await _context.Clases.FindAsync(id);
            if (clase == null) return NotFound();

            _context.Clases.Remove(clase);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private List<Clase> GetSeedClases()
        {
            return new List<Clase>
            {
                new Clase {
                    Id = 1,
                    Horario = "🔹 Modalidad #1 – 2 clases por semana",
                    Costo = 800m,
                    Nivel = "Principiante",
                    CupoMaximo = 14,
                    Descripcion = "📆 Días: A elegir\n💰 Costo: $800 (8 clases en 4 semanas)"
                },
                new Clase {
                    Id = 2,
                    Horario = "🔹 Modalidad #2 – 3 clases por semana",
                    Costo = 900m,
                    Nivel = "Intermedio",
                    CupoMaximo = 12,
                    Descripcion = "📆 Días: A elegir\n💰 Costo: $900 (12 clases en 4 semanas)"
                },
                new Clase {
                    Id = 3,
                    Horario = "🔹 Modalidad #3 – Clases de lunes a viernes",
                    Costo = 1160m,
                    Nivel = "Avanzado",
                    CupoMaximo = 10,
                    Descripcion = "📆 Días: Lunes a viernes\n💰 Costo: $1,160 (20 clases en 4 semanas)"
                }
            };
        }
    }
}
