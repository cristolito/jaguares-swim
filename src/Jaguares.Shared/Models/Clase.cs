namespace Jaguares.Shared.Models
{
    public class Clase
    {
        public int Id { get; set; }
        public string Horario { get; set; } = string.Empty; // texto que describe la modalidad/horario
        public decimal Costo { get; set; } // precio mensual
        public string Nivel { get; set; } = "Intermedio"; // Principiante / Intermedio / Avanzado
        public int CupoMaximo { get; set; } = 12;
        public string? Descripcion { get; set; }
    }
}
