using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jaguares.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedClasesIniciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Modalidades base del club (mismas que sembraba el proyecto original).
            // Al incluir la columna Id (Identity), EF emite SET IDENTITY_INSERT ON/OFF
            // automáticamente, dejando los Ids 1/2/3 estables para las relaciones.
            migrationBuilder.InsertData(
                table: "Clases",
                columns: new[] { "Id", "Horario", "Costo", "Nivel", "CupoMaximo", "Descripcion" },
                values: new object[,]
                {
                    { 1, "🔹 Modalidad #1 – 2 clases por semana", 800m, "Principiante", 14, "📆 Días: A elegir\n💰 Costo: $800 (8 clases en 4 semanas)" },
                    { 2, "🔹 Modalidad #2 – 3 clases por semana", 900m, "Intermedio", 12, "📆 Días: A elegir\n💰 Costo: $900 (12 clases en 4 semanas)" },
                    { 3, "🔹 Modalidad #3 – Clases de lunes a viernes", 1160m, "Avanzado", 10, "📆 Días: Lunes a viernes\n💰 Costo: $1,160 (20 clases en 4 semanas)" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Clases", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "Clases", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "Clases", keyColumn: "Id", keyValue: 3);
        }
    }
}
