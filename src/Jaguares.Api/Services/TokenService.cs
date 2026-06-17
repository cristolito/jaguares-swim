using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Jaguares.Shared.Models;

namespace Jaguares.Api.Services
{
    /// <summary>
    /// Genera tokens JWT para alumnos y administradores.
    /// La configuración (clave, emisor, audiencia, expiración) se lee de la sección "Jwt" de appsettings.
    /// </summary>
    public class TokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public const string RoleAdmin = "Admin";
        public const string RoleStudent = "Student";

        public string GenerarTokenAlumno(Alumno alumno)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, alumno.Id.ToString()),
                new Claim(ClaimTypes.Name, alumno.NombreCompleto),
                new Claim("Telefono", alumno.Telefono),
                new Claim(ClaimTypes.Role, RoleStudent)
            };
            return GenerarToken(claims);
        }

        public string GenerarTokenAdmin()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Administrador"),
                new Claim(ClaimTypes.Role, RoleAdmin)
            };
            return GenerarToken(claims);
        }

        private string GenerarToken(IEnumerable<Claim> claims)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var horas = double.TryParse(jwt["ExpireHours"], out var h) ? h : 8;

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(horas),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
