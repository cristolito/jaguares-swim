namespace Jaguares.Shared.Dtos
{
    public class AlumnoCreateRequest
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string NivelNado { get; set; } = string.Empty;
        public int ClaseId { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class AlumnoUpdateRequest
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public int ClaseId { get; set; }
        public bool Activo { get; set; }
    }

    public class AlumnoLoginRequest
    {
        public string Telefono { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
