namespace Jaguares.Shared.Models
{
    public class Asistencia
    {
        public int Id { get; set; }
        public int AlumnoId { get; set; }
        public Alumno? Alumno { get; set; }
        public DateTime Fecha { get; set; }
        public bool Asistio { get; set; }
        public string? Nota { get; set; }
    }
}
