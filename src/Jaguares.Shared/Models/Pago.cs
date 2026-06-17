namespace Jaguares.Shared.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public int AlumnoId { get; set; }
        public Alumno? Alumno { get; set; }
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public bool EstaPagado { get; set; }
        public string? ComprobanteUrl { get; set; }
        public DateTime? FechaPago { get; set; }
    }
}
