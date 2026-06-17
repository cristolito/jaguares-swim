namespace Jaguares.Shared.Dtos
{
    public class AsistenciaCreateRequest
    {
        public int AlumnoId { get; set; }
        public DateTime Fecha { get; set; }
        public bool Asistio { get; set; }
        public string? Nota { get; set; }
    }
}
