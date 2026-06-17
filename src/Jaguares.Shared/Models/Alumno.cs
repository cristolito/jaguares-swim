using System.Text.Json.Serialization;

namespace Jaguares.Shared.Models
{
    public class Alumno
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;

        // No exponemos el hash de la contraseña al serializar a JSON
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        public string NivelNado { get; set; } = "Principiante";
        public int ClaseId { get; set; }
        public Clase? Clase { get; set; }
        public DateTime FechaInscripcion { get; set; } = DateTime.UtcNow;
        public bool Activo { get; set; } = true;
    }
}
