# GestorShop API — Backend

API REST en **C# / ASP.NET Core 8** que da servicio a la aplicación móvil
**GestorShop Mobile** (.NET MAUI). Gestiona el inventario de productos, las
categorías y las ventas de un pequeño negocio, con autenticación JWT y
autorización por roles.

Proyecto del Componente Práctico-Experimental — Aplicaciones Móviles (UEA).

## 1. Tecnologías

| Componente | Tecnología |
|---|---|
| Framework | ASP.NET Core 8 (Web API con controladores) |
| ORM | Entity Framework Core 8 (lazy loading con proxies) |
| Base de datos | SQLite (`gestorshop.db`, se crea automáticamente) |
| Autenticación | JWT Bearer (rol incluido como *claim*) |
| Documentación | Swagger / OpenAPI (`/swagger`) |

## 2. Estructura

```
GestorShopAPI/
├── Controllers/      # Endpoints: Auth, Productos, Categorias, Ventas
├── Data/             # AppDbContext (relaciones) y SeedData (datos iniciales)
├── Models/           # Entidades: Usuario, Categoria, Producto, Venta, DetalleVenta
├── Services/         # JWT, caché (cache-aside) y cola de tareas en segundo plano
├── Properties/       # launchSettings.json (puerto 5164)
└── Program.cs        # Configuración: BD, JWT, caché, cola, JSON, Swagger
```

## 3. Modelo de datos

- **Categoria** (1) ── (N) **Producto**
- **Venta** (1) ── (N) **DetalleVenta** (N) ── (1) **Producto**
- **Usuario** (rol `Administrador` o `Vendedor`)

Claves primarias `Id` en todas las tablas y claves foráneas `CategoriaId`,
`VentaId` y `ProductoId`. El precio unitario se guarda en `DetalleVenta`
para conservar el valor histórico aunque luego cambie el precio del producto.

## 4. Puesta en marcha

Requisitos: .NET SDK 8 o superior.

1. **Configurar los secretos locales.** Por seguridad, la clave JWT y la
   contraseña del administrador inicial **no están en el repositorio**.
   Copie la plantilla y complete los valores:

   ```bash
   cp appsettings.Development.example.json appsettings.Development.json
   ```

   - `Jwt:Key`: cadena aleatoria de al menos 32 caracteres.
   - `Seed:AdminPassword`: contraseña del usuario `admin` que se crea la primera vez.

   El archivo `appsettings.Development.json` está excluido por `.gitignore`.
   En producción se usan variables de entorno (`Jwt__Key`, `Seed__AdminPassword`).

2. **Ejecutar:**

   ```bash
   dotnet restore
   dotnet run
   ```

   La API queda en `http://localhost:5164` y Swagger en
   `http://localhost:5164/swagger`. En el primer arranque se crea la base
   de datos con 2 categorías, 50 productos y el usuario `admin`.

> Desde el emulador de Android la app accede a la API mediante
> `http://10.0.2.2:5164` (alias del `localhost` del computador anfitrión).

## 5. Endpoints

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/auth/login` | Público | Valida credenciales y devuelve `{ token, rol }` |
| GET | `/api/productos` | Público | Lista de productos con su categoría (con caché) |
| GET | `/api/productos/{id}` | Público | Detalle de un producto |
| POST | `/api/productos` | Administrador | Crea un producto |
| PUT | `/api/productos/{id}` | Administrador | Actualiza nombre, precio, stock y categoría |
| PUT | `/api/productos/{id}/stock` | Administrador | Actualiza solo el stock |
| DELETE | `/api/productos/{id}` | Administrador | Elimina un producto sin ventas registradas |
| GET | `/api/productos/sin-optimizar` | Público | Versión con problema N+1 (solo para comparar) |
| GET | `/api/categorias` | Público | Lista de categorías |
| POST | `/api/categorias` | Administrador | Crea una categoría |
| GET | `/api/ventas` | Usuario autenticado | Historial de ventas con sus detalles |
| POST | `/api/ventas` | Usuario autenticado | Registra una venta y descuenta el stock |

### Validaciones y respuestas

| Código | Cuándo se devuelve |
|---|---|
| `200 OK` / `201 Created` / `204 No Content` | Operación exitosa |
| `400 Bad Request` | Datos inválidos (nombre vacío, precio ≤ 0, stock negativo, stock insuficiente, categoría inexistente) |
| `401 Unauthorized` | Sin token, token inválido o expirado (dura 15 minutos) |
| `403 Forbidden` | El rol no tiene permiso (por ejemplo, un Vendedor que intenta crear productos) |
| `404 Not Found` | El producto solicitado no existe |
| `409 Conflict` | Nombre de producto o categoría duplicado, o producto con ventas que no se puede eliminar |

Los errores devuelven un cuerpo `{ "mensaje": "..." }` que la app móvil
muestra directamente al usuario.

## 6. Optimización aplicada

| Técnica | Dónde | Detalle |
|---|---|---|
| Caché *cache-aside* | `CacheService`, `GET /api/productos` | TTL de 60 s; se invalida al crear, editar o eliminar productos y al registrar ventas |
| Corrección de consultas N+1 | `GET /api/productos` | `Include()` + `Select()`: 1 consulta con JOIN en lugar de 51 (comparar con `/sin-optimizar`) |
| Proyección de campos | Productos, ventas y categorías | Solo se envían los campos que usa la app, no las entidades completas |
| Lazy loading justificado | `Producto.Categoria` (`virtual`) | Solo se carga si se accede a ella (detalle de un producto) |
| Eager loading justificado | `Venta.Detalles` | Una venta siempre se consulta con sus líneas |
| Tarea asíncrona en cola | `VentasController` + `QueuedHostedService` | El comprobante de la venta se procesa en segundo plano sin bloquear la respuesta |
| Autenticación sin consultas redundantes | `JwtService` | El rol viaja en el token: no se consulta la BD en cada petición protegida |

## 7. Notas para producción

- Usar HTTPS y una base de datos de servidor (por ejemplo, PostgreSQL o SQL Server).
- Reemplazar el hash SHA-256 simple por BCrypt o ASP.NET Core Identity.
- Usar Redis (`IDistributedCache`) si la API se ejecuta en varios servidores.
