using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Jaguares.Api.Services;
using Jaguares.Shared.Dtos;

namespace Jaguares.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly TokenService _tokenService;
        private readonly string _adminPassword;

        public AdminController(TokenService tokenService, IConfiguration config)
        {
            _tokenService = tokenService;
            // La contraseña de admin se lee de configuración (appsettings.{env}.json o variables de entorno),
            // nunca se versiona ni se deja un valor por defecto en el código.
            _adminPassword = config["Admin:Password"]
                ?? throw new InvalidOperationException("Falta la configuración Admin:Password.");
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] AdminLoginRequest request)
        {
            if (request.Password == _adminPassword)
            {
                var token = _tokenService.GenerarTokenAdmin();
                return Ok(new { success = true, token });
            }
            return Unauthorized(new { message = "Contraseña incorrecta" });
        }

        // Con JWT el cierre de sesión se hace en el cliente (descartando el token).
        // Mantenemos el endpoint por compatibilidad con el frontend.
        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            return Ok(new { success = true });
        }
    }
}
