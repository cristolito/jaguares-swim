using Jaguares.Api;

var builder = WebApplication.CreateBuilder(args);

// Host independiente de la API (sin frontend). Pensado para publicar el servicio por sí solo
// (p. ej. en somee u otra instancia) en el futuro.
builder.Services.AddJaguaresApi(builder.Environment.IsDevelopment(), builder.Configuration);

var app = builder.Build();

app.UseJaguaresApi(serveFrontend: false);

Console.WriteLine("API Jaguares Swim iniciando...");
app.Run();
