var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Servimos el frontend estático (HTML/JS/CSS) desde wwwroot.
// index.html se entrega como archivo por defecto en la raíz.
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
