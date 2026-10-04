# GestorShop Mobile — cliente .NET MAUI

Aplicacion movil que consume GestorShop API (proyecto de la Semana 8). Cubre
en un solo proyecto los tres talleres: configuracion del entorno y
conectividad (Semana 9), autenticacion/navegacion/estado/formularios, y
capacidades nativas con manejo de permisos (Semana 14).

## 1. Justificacion del framework

**.NET MAUI**, por continuidad directa con el backend (mismo lenguaje C#,
mismo tooling: Visual Studio / `dotnet` CLI), y porque permite compartir
modelos (DTOs) conceptualmente identicos a los del backend sin cambiar de
ecosistema. Reemplaza la implementacion previa en Flutter/Dart del mismo
taller (decision documentada en las notas del curso).

## 2. Instalacion y verificacion del entorno

```bash
# 1. Instalar el SDK de .NET 8
dotnet --version        # debe reportar 8.0.x

# 2. Instalar el workload de MAUI
dotnet workload install maui

# 3. Diagnostico -- debe salir sin hallazgos pendientes para Android
dotnet workload list
dotnet build -t:Run -f net8.0-android   # compila y despliega al destino configurado
```

Editor: Visual Studio 2022 (17.8+) con la carga de trabajo ".NET
Multi-platform App UI development", o VS Code + extension ".NET MAUI".

## 3. Destino de ejecucion

Se usa el **emulador Android** (API 34, Pixel 6) por defecto -- no requiere
hardware adicional y es reproducible en cualquier equipo del curso. Si el
equipo no alcanza los requisitos del emulador (RAM/virtualizacion), se
documenta como alternativa justificada el uso de un dispositivo fisico con
depuracion USB habilitada.

## 4. Conectividad con el backend propio

- La URL base se resuelve en `Services/AppConfig.cs`, en este orden:
  1. Variable de entorno `GESTORSHOP_API_BASE_URL` (definida antes de
     `dotnet build`, util en CI o para apuntar a un backend ya desplegado).
  2. Valor por defecto segun el destino: `http://10.0.2.2:5164` en el
     emulador Android (alias del "localhost" de la maquina anfitriona).
  3. Override en tiempo de ejecucion desde **Perfil -> URL del backend**,
     necesario en un dispositivo fisico porque una variable de entorno no
     tiene sentido dentro de un binario ya compilado -- ahi se ingresa la IP
     LAN real del equipo que corre GestorShopAPI (ej. `192.168.1.100`).
- El trafico HTTP sin cifrar esta autorizado **solo** hacia esos hosts de
  desarrollo (`Platforms/Android/Resources/xml/network_security_config.xml`)
  -- no hay `cleartextTrafficPermitted` global. Ese archivo debe eliminarse o
  restringirse a HTTPS antes de cualquier distribucion.
- Prueba de conectividad: `ProductosPage` llama a `GET /api/productos` en
  `OnAppearing`; si el backend no esta corriendo, se muestra la alerta con el
  mensaje de error de conexion (ver `ApiService.ObtenerProductosAsync`).

**Antes de grabar el video:** levantar `GestorShopAPI` con `dotnet run` y
confirmar que responde en `http://localhost:5164/swagger`.

## 5. Autenticacion, navegacion, estado y formularios

| Requisito | Donde |
|---|---|
| Login | `Views/LoginPage.xaml(.cs)` -> `ApiService.LoginAsync` -> `POST /api/auth/login` |
| Validacion de formulario | `LoginPage.ValidarFormulario()`, `RegistrarVentaPage.ValidarFormulario()` -- campos obligatorios, formato, mensajes por campo |
| Registro de usuario | **No implementado, deliberadamente**: GestorShopAPI no expone un endpoint de autorregistro (los usuarios los crea un administrador). Se declara asi en la propia pantalla de login y debe mencionarse en el video como decision justificada del backend |
| Navegacion | `AppShell.xaml` (Shell con `TabBar`): Productos <-> Nueva venta <-> Perfil, mas la ruta publica `login` |
| Proteccion de vistas | Cada pagina protegida verifica `AuthService.IsAuthenticated` en `OnAppearing` y redirige a `//login` si no hay sesion |
| Manejo de estado | `Services/AuthService.cs`, registrado como **singleton** -- unica fuente de verdad del usuario/token/rol para toda la app |
| Persistencia durante la navegacion | El estado vive en el singleton mientras la app esta abierta; no se pierde al cambiar de pestana |
| Cierre de sesion | `PerfilPage.OnCerrarSesionClicked` -> `AuthService.CerrarSesion()` limpia `SecureStorage` y redirige a login |
| Persistencia entre reinicios | `AuthService.RestaurarSesionAsync()`, llamado desde `AppShell` al arrancar, recupera el token de `SecureStorage` (almacenamiento cifrado del dispositivo) |

## 6. Capacidades nativas (Semana 14)

Ambas se piden en el momento de uso, dentro de `RegistrarVentaPage`, nunca al
iniciar la app.

| Capacidad | Esencial/Opcional | Plugin/API | Justificacion |
|---|---|---|---|
| Camara | Esencial para este flujo | `MediaPicker.Default.CapturePhotoAsync()` (incluido en .NET MAUI Essentials, sin plugin de terceros) | Evidencia fotografica de la entrega -- sin foto, ese paso del flujo no tiene sentido |
| Geolocalizacion | Opcional | `Geolocation` (.NET MAUI Essentials) | Registrar el punto de entrega mejora la trazabilidad, pero la venta puede registrarse sin coordenadas |

### Tabla de permisos declarados

| Permiso | Plataforma | Proposito declarado |
|---|---|---|
| `CAMERA` | Android (`AndroidManifest.xml`) | Adjuntar foto de evidencia de entrega |
| `ACCESS_FINE_LOCATION` / `ACCESS_COARSE_LOCATION` | Android | Registrar el punto de entrega de la venta |
| `NSCameraUsageDescription` | iOS (`Info.plist`) | Mismo proposito que en Android |
| `NSLocationWhenInUseUsageDescription` | iOS (`Info.plist`) | Mismo proposito que en Android |

Deliberadamente **no** se declara `READ_MEDIA_IMAGES`/acceso a galeria: se usa
el selector del sistema, que no lo requiere.

### Matriz de degradacion (`Services/PermissionService.cs`)

| Estado del permiso | Camara | Ubicacion |
|---|---|---|
| Concedido | Se abre la camara y se guarda la foto en `FileSystem.AppDataDirectory` (persistencia local via `Preferences`) | Se capturan las coordenadas y se anexan a la venta |
| Denegado (temporal) | Mensaje + boton para reintentar | Mensaje + venta se registra sin coordenadas |
| Denegado permanentemente ("no volver a preguntar") | Mensaje + boton **"Abrir ajustes de la app"** (`AppInfo.Current.ShowSettingsUI()`) | Igual que camara |
| Servicio del dispositivo apagado (GPS) | -- | Se detecta por separado de la denegacion de permiso (`FeatureNotEnabledException`) y se informa como caso distinto |

### Nivel de API objetivo

`TargetPlatformVersion` = **34** (Android 14), con `SupportedOSPlatformVersion`
= 24, revisado segun la exigencia de Google Play de apuntar a Android 16 desde
el 31 de agosto de 2026 (`GestorShopMobile.csproj`) -- pendiente de subir a 35+
cuando el SDK correspondiente este disponible en el entorno del curso.

## 7. Casos de prueba sugeridos para el video

1. Login con credenciales incorrectas -> mensaje de error, sin navegar.
2. Login correcto (`admin` / `admin123`) -> entra a Productos.
3. Navegar entre las 3 pestanas sin perder la sesion.
4. Nueva venta: capturar foto -> conceder permiso -> ver la vista previa.
5. Nueva venta: capturar ubicacion -> conceder permiso -> ver coordenadas.
6. Denegar el permiso de camara y marcar "no volver a preguntar" -> ver el boton de ajustes.
7. Apagar el GPS del emulador y capturar ubicacion -> ver el mensaje de "servicio apagado".
8. Cerrar sesion -> intentar volver a Productos manualmente -> la app redirige a login.
