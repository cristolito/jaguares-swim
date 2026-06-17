# Jaguares Swim

Sistema de gestión de la escuela de natación Jaguares Swim, dividido en **frontend** y **backend** independientes.

## Estructura de la solución

```
Jaguares.slnx
src/
  Jaguares.Shared/   -> Modelos y DTOs compartidos (Alumno, Clase, Pago, Asistencia, requests)
  Jaguares.Api/      -> API REST (ASP.NET Core + EF Core + SQL Server). Separable/publicable sola.
  Jaguares.Web/      -> Frontend estático (HTML/JS/CSS). Publicable solo (apunta a la API por config.js).
  Jaguares.Host/     -> Host COMBINADO: aloja la API y sirve el frontend en el MISMO origen.
                        Pensado para una sola instancia (monsterasp). Es lo que publicas hoy.
```

- **Jaguares.Shared**: biblioteca de clases con los modelos de dominio y los DTOs de las peticiones.
  La referencian la API y el host (y queda lista para futuros clientes .NET tipados).
- **Jaguares.Api**: expone los endpoints `api/...`, accede a **SQL Server** mediante Entity Framework Core
  y protege las rutas con **autenticación JWT** (roles `Admin` y `Student`). Toda su configuración está
  empaquetada en métodos de extensión (`AddJaguaresApi` / `UseJaguaresApi`), así que se puede alojar
  desde otro proyecto o extraer a otro servicio sin cambios.
- **Jaguares.Web**: sirve los archivos estáticos del sitio. Se conecta a la API mediante `apiFetch`.
  Útil cuando en el futuro quieras el frontend en una instancia y la API en otra (somee).
- **Jaguares.Host**: reutiliza la API (`AddJaguaresApi`) y además sirve el frontend (los archivos de
  `Jaguares.Web/wwwroot`, copiados en compilación) en el mismo origen. Su `config.js` deja `API_BASE_URL`
  vacío porque la API está en el mismo servidor.

### ¿Cuál publico?

- **Hoy (una sola instancia en monsterasp):** publica **Jaguares.Host**. API + web juntos, sin CORS ni
  dominios cruzados.
- **En el futuro (servicios separados):** publica **Jaguares.Api** por un lado (somee u otra instancia)
  y **Jaguares.Web** por otro, ajustando `Cors:AllowedOrigins` y `config.js`. No hay que tocar la lógica.

## Autenticación (JWT)

Como el frontend y el backend viven en dominios distintos, se usa **JWT (Bearer)** en lugar de cookies:

- El alumno (`POST api/Alumnos/login`) y el administrador (`POST api/Admin/login`) reciben un `token`.
- El frontend guarda el token en `localStorage` (`jaguares_token`) y lo envía en el header
  `Authorization: Bearer <token>` automáticamente a través de `apiFetch` (ver `wwwroot/api.js`).
- Roles: las rutas de administración exigen rol `Admin`; el portal del alumno exige rol `Student`.

## Configuración

### Backend — `src/Jaguares.Api/appsettings.json`
- `Jwt:Key` — **cámbiala** por una clave secreta larga (mínimo 32 caracteres) en producción.
- `Admin:Password` — contraseña del panel de administración.
- `Cors:AllowedOrigins` — agrega aquí la URL pública del frontend en monsterasp.net.
- `ConnectionStrings:Default` — cadena de conexión a SQL Server (BD de monsterasp / databaseasp.net).

### Frontend — `src/Jaguares.Web/wwwroot/config.js`
- `API_BASE_URL` — URL pública de la API en somee (en local: `http://localhost:5105`).

## Ejecutar en local

### Opción A — Host combinado (recomendado para tu caso actual)
```bash
# API + Frontend en http://localhost:5080
dotnet run --project src/Jaguares.Host
```
O ejecuta `Iniciar_Host.bat`.

### Opción B — API y frontend por separado
```bash
# API (http://localhost:5105)
dotnet run --project src/Jaguares.Api

# Frontend (http://localhost:5000)
dotnet run --project src/Jaguares.Web
```
O ejecuta `Iniciar_Jaguares.bat` para levantar ambos a la vez.

## Migraciones de base de datos

```bash
dotnet ef migrations add <Nombre> --project src/Jaguares.Api
dotnet ef database update --project src/Jaguares.Api
```
La API aplica las migraciones y siembra las modalidades automáticamente al iniciar.

## Publicación

### Host combinado (lo que publicas hoy en monsterasp)
```bash
dotnet publish src/Jaguares.Host -c Release
```
Sube el contenido publicado a tu instancia. Incluye la API y el frontend (con su `config.js` de mismo
origen). Configura en el hosting: cadena de conexión SQL Server, `Jwt:Key`, `Admin:Password`.

### Servicios separados (futuro)
- **API (somee u otra instancia):** `dotnet publish src/Jaguares.Api -c Release`
  Configura `Jwt:Key`, `Admin:Password` y agrega el dominio del frontend en `Cors:AllowedOrigins`.
- **Frontend (otra instancia):** `dotnet publish src/Jaguares.Web -c Release`
  Ajusta `wwwroot/config.js` con la URL pública (HTTPS) de la API antes de publicar.
