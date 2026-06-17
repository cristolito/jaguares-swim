using Jaguares.Api;

var builder = WebApplication.CreateBuilder(args);

// Host COMBINADO: aloja la API y sirve el frontend en el mismo origen (una sola instancia).
// La API sigue empaquetada en el proyecto Jaguares.Api, así que en el futuro se puede extraer
// y publicar por separado sin tocar la lógica.
builder.Services.AddJaguaresApi(builder.Environment.IsDevelopment(), builder.Configuration);

var app = builder.Build();

app.UseJaguaresApi(serveFrontend: true);

Console.WriteLine("Jaguares Swim (API + Web) iniciando...");
app.Run();
