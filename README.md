# GestorShop — Aplicación móvil multiplataforma

Proyecto final del Componente Práctico-Experimental de **Aplicaciones Móviles**
(Universidad Estatal Amazónica).

**Autor:** Willian Ocampo

GestorShop permite a un pequeño negocio gestionar su inventario y registrar
ventas desde el celular, con el stock actualizado al instante.

## Estructura del repositorio

| Carpeta | Contenido |
|---|---|
| [`GestorShopMobile/`](GestorShopMobile/GestorShopMobile) | **Frontend**: aplicación móvil en .NET MAUI (C#) para Android |
| [`GestorShopAPI/`](GestorShopAPI/GestorShopAPI) | **Backend**: API REST en ASP.NET Core 8 con Entity Framework Core y SQLite |

Cada proyecto tiene su propio README con la explicación detallada.

## Arquitectura

```
App móvil (.NET MAUI)  ──HTTP/JSON + JWT──▶  API REST (ASP.NET Core)  ──EF Core──▶  SQLite
```

## Funcionalidades

- Inicio de sesión con JWT y autorización por roles (Administrador / Vendedor).
- Catálogo de productos con búsqueda y actualización.
- CRUD completo de productos y creación de categorías (solo Administrador).
- Registro de ventas con validación de stock, foto de evidencia y ubicación.
- Historial de ventas con resumen de totales.
- Optimización: caché, corrección de consultas N+1, proyección de campos,
  lazy/eager loading y procesamiento en segundo plano.

## Cómo ejecutar

1. **Backend:** seguir los pasos de
   [GestorShopAPI/GestorShopAPI/README.md](GestorShopAPI/GestorShopAPI/README.md)
   (configurar `appsettings.Development.json` y ejecutar `dotnet run`).
   La API queda en `http://localhost:5164`.
2. **App móvil:** con un emulador de Android abierto, en la carpeta
   `GestorShopMobile/GestorShopMobile` ejecutar:

   ```bash
   dotnet build -t:Run -f net10.0-android
   ```

   Desde el emulador, la app se conecta a la API mediante `http://10.0.2.2:5164`.
