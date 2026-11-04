# Sistema web de compra y reserva de pasajes – Aerolínea VIP
Primera Entrega · Grupo 05

## Integrantes
Mateo Alexander Flores Hidalgo · Rashell Estefany Pinos Cordero

# Aerolínea VIP — Guía de uso

Aplicación web académica de compra, reserva y administración de pasajes de una
aerolínea VIP. Desarrollada con ASP.NET Framework 4.8.1, MVC 5, Razor, Bootstrap,
jQuery, ASP.NET Identity 2.x, Entity Framework 6 (Database First) y SQL Server.
Incluye las 21 pantallas del proyecto, mapa de asientos con estados de color,
pago simulado con tarjeta, check-in y módulos administrativos con CRUD.

> **Importante:** es una demostración. No procesa cobros reales, no uses tarjetas
> ni datos personales reales. Las compras y los comprobantes generados por el flujo
> de pago son **registros académicos**; no tienen validez tributaria ni comercial.

---

## 1. Requisitos y puesta en marcha

### Requisitos previos

- Visual Studio 2019 o 2022 con la carga de trabajo **"Desarrollo de ASP.NET y web"**.
- **.NET Framework 4.8.1** (targeting pack instalado).
- **SQL Server 2016 o superior** (Developer, Express o LocalDB) y **SQL Server Management Studio (SSMS)**.
- **NuGet** para restaurar los paquetes del proyecto.

### Pasos para ejecutar localmente

1. Abrir `src\AerolineaVIP.sln` en Visual Studio.
2. En SSMS, abrir `database\Script_BD_AerolineaVIP.sql` y **ejecutarlo completo** (F5)
   contra una instancia local. El script crea dos bases independientes:
   - `AerolineaVIP` → dominio (vuelos, reservas, pagos simulados, comprobantes).
   - `AerolineaVIP_Identity` → usuarios, roles, claims y logins.

   **Advertencia:** la parte de dominio del script **elimina y vuelve a crear** sus
   objetos y datos. No lo vuelvas a ejecutar sobre una base con información que
   quieras conservar. La parte de Identity **conserva** las cuentas existentes.

3. Revisar las cadenas `DefaultConnection` y `AerolineaVIPEntities` en
   `src\AerolineaVIP\Web.config`. Deben apuntar a la instancia SQL local y a las
   bases anteriores. Usar `Integrated Security=True` (autenticación de Windows) o
   credenciales de ejemplo. **No subir contraseñas reales al repositorio.**
4. Restaurar los paquetes NuGet si Visual Studio lo solicita
   (clic derecho en la solución → **Restaurar paquetes NuGet**).
5. Seleccionar el proyecto web `AerolineaVIP` como proyecto de inicio y ejecutar con
   IIS Express (`F5`). La URL inicial abre la página de inicio.

Al arrancar, `Global.asax.cs` ejecuta el método `CrearRolesYUsuarios()`, que crea
**por código** los roles, los usuarios iniciales y sus asignaciones. Si los usuarios
ya existen, no los duplica: solo actualiza los roles faltantes.

---

## 2. Cuentas de prueba e inicio de sesión

En `Global.asax.cs`, al iniciar la aplicación se crean (si aún no existen) estas
cuentas y se asignan sus roles:

| Usuario         | Correo                    | Contraseña local inicial | Acceso                                                  |
|-----------------|---------------------------|--------------------------|---------------------------------------------------------|
| `superusuario`  | superusuario@gmail.com    | `Superusuario123!`       | SuperUsuario + Create, Edit, Details, Delete            |
| `administrador` | administrador@gmail.com   | `Admin123!`              | Administrador + Create, Edit, Details (sin Delete)      |
| `counter`       | counter@gmail.com         | `Counter123!`            | Counter + Details (solo consulta)                       |
| `repinos1`      | repinos1@gmail.com        | `Repinas123!`            | Cliente (vinculado a las reservas de demostración R01, R06, R11) |

En **Iniciar sesión** puedes ingresar el nombre de usuario o el correo junto con la
contraseña. Si la cuenta ya existía en `AerolineaVIP_Identity`, el arranque no
cambia su contraseña: usa la contraseña vigente de esa base.

> **Nota de seguridad:** estas credenciales están escritas en el código para
> desarrollo académico. No las uses en producción ni publiques el sitio con ellas.
> Cámbialas y protege las cadenas de conexión antes de compartir o desplegar el
> proyecto.

### Roles del sistema

El proyecto usa dos grupos de roles:

**Roles de acceso a módulos** (asignados por `CrearRolesYUsuarios()`):

| Rol              | Acceso                                                              |
|------------------|---------------------------------------------------------------------|
| `SuperUsuario`   | Todas las funciones, incluida la gestión de roles y la eliminación  |
| `Administrador`  | Panel administrativo y CRUD de vuelos, pasajeros y reservas         |
| `Counter`        | Consulta de operaciones y asistencia al cliente                     |
| `Cliente`        | Reservas propias, compras, check-in y comprobantes                  |

**Roles de permisos CRUD** (con Id específico: 1, 2, 3, 4):

| Id  | Rol        | Acción permitida           |
|-----|------------|----------------------------|
| `1` | `Create`   | Crear registros            |
| `2` | `Edit`     | Editar registros           |
| `3` | `Details`  | Consultar detalles         |
| `4` | `Delete`   | Eliminar registros (solo SuperUsuario) |

La autorización se valida **en el servidor** con `[Authorize(Roles = "...")]` en cada
controlador. Ocultar botones en la interfaz no se considera suficiente.

### Crear una cuenta nueva

1. Selecciona **Registrarse / Crear cuenta** desde la barra superior o desde la
   pantalla de inicio de sesión.
2. Completa nombre de usuario, correo, contraseña y confirmación.
3. El **registro público** asigna automáticamente el rol `Cliente` e inicia sesión al
   completarse.
4. La contraseña debe cumplir la política de Identity: mínimo 6 caracteres, con
   mayúscula, minúscula, dígito y carácter no alfanumérico.
5. **Solo una cuenta autenticada con rol `SuperUsuario`** puede ver y elegir un rol
   en el formulario de registro. No uses el registro público para otorgarte
   privilegios.

La recuperación/cambio por correo requiere configurar un servicio de envío; el
proyecto no incluye un proveedor real.

---

## 3. Datos demo

El script `database\Script_BD_AerolineaVIP.sql` **inserta datos de demostración** al
ejecutarse. No es un seeder en tiempo de ejecución: los datos ya están en las
tablas cuando la aplicación arranca.

### Datos incluidos por el script

- **Flota:** 3 aeronaves Embraer Phenom 300E (`AV-01`, `AV-02`, `AV-03`) con
  7 asientos cada una (21 asientos en total).
- **Aeropuertos:** Quito (UIO), Guayaquil (GYE), Manta (MEC) y Galápagos (GPS).
- **Rutas y frecuencias confirmadas:** Quito–Guayaquil, Quito–Manta y
  Quito–Galápagos con escala en Guayaquil.
- **Vuelos de demostración:** dos semanas (12–18/10/2026 completados y
  02–08/11/2026 confirmados o programados).
- **24 pasajeros ficticios** con documentos y correos de ejemplo.
- **12 reservas** (R01 a R12) con distintos estados: CONFIRMADA, PENDIENTE,
  CANCELADA, EXPIRADA.
- **62 asignaciones de asiento** en `ReservaPasajeroVuelo`.
- **Compras, pagos simulados y comprobantes académicos** de ejemplo.

### Reservas vinculadas al usuario `repinos1`

El usuario `repinos1` fue creado con un Id fijo (`7c1e4a20-0001-4b6e-9a01-000000000001`)
que coincide con el `UsuarioId` de las reservas **R01**, **R06** y **R11** del
script. Al iniciar sesión con `repinos1`, la pantalla **Mis reservas** mostrará estas
tres operaciones de ejemplo.

| Reserva | Estado en el script | Fecha de viaje | Observación                                    |
|---------|---------------------|----------------|------------------------------------------------|
| R01     | CONFIRMADA          | 18/10/2026     | Vuelo de la semana pasada (ya completado)      |
| R06     | CONFIRMADA          | 02/11/2026     | Vuelo próximo, con compra y pago aprobado      |
| R11     | PENDIENTE           | 07/11/2026     | Reserva sin pago; su fecha de expiración es 23/10/2026 |

> **Nota:** si al iniciar sesión con `repinos1` la reserva **R11** aparece como
> pendiente aunque su fecha de expiración ya pasó, significa que la aplicación no
> tiene un proceso automático de expiración. Es un comportamiento académico
> esperado: la política de retención de reservas es una **decisión pendiente
> (DP-07)** que debe aprobarse antes de implementarla.

### Usuarios demo adicionales

El script **no** crea usuarios de Identity: eso lo hace `Global.asax.cs`. Los
usuarios `superusuario`, `administrador` y `counter` **no tienen reservas asociadas**;
solo acceden a las funciones de sus roles.

---

## 4. Recorrido de las pantallas / wireframes

Las pantallas **públicas** de búsqueda se pueden visitar sin cuenta. Para guardar
una reserva hay que iniciar sesión. La barra superior ofrece:

- **Inicio** (`/Home/Index`) — página pública.
- **Vuelos** (`/Vuelos/Buscar`) — búsqueda de vuelos.
- Cuando ya iniciaste sesión, según tu rol:
  - **Cliente:** Mis reservas, Check-in, Comprobantes.
  - **Administrador / SuperUsuario:** Administración.

### Compra de un vuelo

1. **Pantalla 5 — Buscar vuelos** (`/Vuelos/Buscar`): elige origen, destino, fecha y
   de 1 a 7 pasajeros; pulsa **Buscar**. Los destinos disponibles dependen de las
   rutas activas.
2. **Pantalla 6 — Resultados** (`/Vuelos/Resultados`): compara horario, tarifa,
   estado y asientos disponibles. Puedes revisar disponibilidad o continuar con
   **Seleccionar vuelo**. Si no has iniciado sesión, el sistema te lleva a iniciar
   sesión y luego regresa al vuelo.
3. **Pantalla 7 — Disponibilidad de asientos** (`/Vuelos/Disponibilidad/{id}`):
   consulta el mapa/estado de los asientos sin reservarlos. Los asientos se muestran
   con la semántica obligatoria:
   - 🟢 **Verde** = Disponible
   - 🟣 **Morado** = Reservado (pendiente de pago)
   - 🔴 **Rojo** = Ocupado (compra pagada)
4. **Pantalla 8 — Selección de asientos** (`/Reservas/SeleccionAsiento/{id}`): elige
   un asiento disponible por pasajero y pulsa **Continuar a pasajeros**. La selección
   guardada en la sesión es **temporal**; el servidor vuelve a verificar
   disponibilidad al confirmar la reserva.
5. **Pantalla 9 — Datos de pasajeros** (`/Reservas/Pasajeros`): completa tipo y
   número de documento, nombres y apellidos de cada pasajero; continúa a la revisión.
6. **Pantalla 10 — Revisión y confirmación de reserva** (`/Reservas/Confirmar`):
   revisa vuelo, pasajeros, asientos, importe y condiciones. Acepta las condiciones y
   elige **Confirmar reserva** para guardar una reserva pendiente o
   **Confirmar y pagar** para ir al pago. La reserva pendiente retiene los asientos
   según la política vigente.
7. **Pantalla 11 — Pago simulado** (`/Compras/Pagar/{id}`): selecciona método y
   marca, completa datos ficticios válidos y pulsa **Revisar y pagar**. En el modal,
   pulsa **Pagar** para registrar la compra/pago académico. La aplicación **no**
   envía una autorización bancaria ni guarda el número completo, CVV ni fecha de
   vencimiento de la tarjeta.
8. **Pantalla 12 — Confirmación** (`/Compras/Confirmacion/{id}`): consulta el estado
   de la reserva, la compra y el pago, diferenciados en tres etiquetas separadas.

### Área personal y servicio al pasajero

- **Pantalla 13 — Mis reservas** (`/Reservas/MisReservas`): lista solo operaciones de
  la cuenta autenticada. Las pendientes permiten volver al pago mientras no venzan.
- **Pantalla 14 — Gestionar reserva** (`/Reservas/Gestionar/{id}`): consulta
  itinerario, pasajeros, asientos y estados. Las cancelaciones, cambios y reembolsos
  están sujetos a la política del proyecto (DP-08, pendiente de aprobación).
- **Pantalla 15 — Check-in** (`/CheckIn/Index`): consulta reservas confirmadas. La
  ventana temporal, documentos exigidos y excepciones son decisiones pendientes
  (DP-12).
- **Pantalla 16 — Comprobantes** (`/Comprobantes/Index`): consulta comprobantes
  simulados asociados a compras. Son ficticios y **no** tienen validez tributaria.

### Administración

Los usuarios con rol `Administrador` o `SuperUsuario` acceden a **Administración**
desde la barra o `/Admin/Index`.

- **Pantalla 17 — Panel administrativo** (`/Admin/Index`): página de accesos a los
  módulos autorizados. **El dashboard de indicadores está excluido** de la primera
  entrega.
- **Pantalla 18 — Vuelos y rutas** (`/Admin/Vuelos`): administración de vuelos,
  frecuencias, rutas, itinerarios y aeropuertos.
- **Pantalla 19 — Flota y asientos** (`/Admin/Aeronaves`): administración de
  aeronaves, modelos y asientos.
- **Pantalla 20 — Gestión operativa** (`/Admin/Gestion`): pestañas de Usuarios,
  Pasajeros y Reservas, con CRUD autorizado según el rol.
- **Pantalla 21 — Reportes** (`/Admin/Reportes`): consultas tabulares de ocupación,
  reservas y operaciones comerciales.

Los módulos CRUD tienen permisos propios. `SuperUsuario` tiene los roles
`SuperUsuario`, `Create`, `Edit`, `Details` y `Delete`. `Administrador` tiene
`Administrador`, `Create`, `Edit` y `Details` (sin `Delete`). Algunas operaciones
administrativas sensibles, como eliminar aeronaves, están limitadas a `SuperUsuario`.

---

## 5. Comportamiento del pago de demostración

Al confirmar el pago se persisten una **compra** y un **pago** identificados como
**simulados**, se confirma la reserva y se emite un **comprobante académico**. No es
una transacción real ni acredita el pago de un servicio.

**No introduzcas datos reales de tarjeta.** Para datos de prueba, la validación exige:

- Solo dígitos y la longitud definida para la marca seleccionada
  (Visa/Mastercard 16, American Express 15, Diners Club 14).
- Código de seguridad con la longitud configurada (3 dígitos, 4 para Amex).
- Fecha de vencimiento futura en formato `MM/AA`.

Lo que se guarda en la base:

- Marca de la tarjeta.
- Últimos 4 dígitos ficticios.
- Nombre del titular (puede ser ficticio).
- Código de operación con prefijo `SIM-...`.
- Bandera `EsSimulado = 1` y mensaje de resultado.

Lo que **no** se guarda: número completo, CVV, fecha de vencimiento.

---

## 6. Rutas principales

| Función              | Ruta                                  |
|----------------------|---------------------------------------|
| Inicio               | `/Home/Index`                         |
| Iniciar sesión       | `/Account/Login`                      |
| Registro             | `/Account/Register`                   |
| Recuperar acceso     | `/Account/ForgotPassword`             |
| Buscar vuelos        | `/Vuelos/Buscar`                      |
| Resultados           | `/Vuelos/Resultados`                  |
| Disponibilidad       | `/Vuelos/Disponibilidad/{id}`         |
| Selección de asiento | `/Reservas/SeleccionAsiento/{id}`     |
| Datos de pasajeros   | `/Reservas/Pasajeros`                 |
| Confirmar reserva    | `/Reservas/Confirmar`                 |
| Pago simulado        | `/Compras/Pagar/{id}`                 |
| Confirmación         | `/Compras/Confirmacion/{id}`          |
| Mis reservas         | `/Reservas/MisReservas`               |
| Gestionar reserva    | `/Reservas/Gestionar/{id}`            |
| Check-in             | `/CheckIn/Index`                      |
| Comprobantes         | `/Comprobantes/Index`                 |
| Administración       | `/Admin/Index`                        |
| Admin · Vuelos       | `/Admin/Vuelos`                       |
| Admin · Aeronaves    | `/Admin/Aeronaves`                    |
| Admin · Gestión      | `/Admin/Gestion`                      |
| Admin · Reportes     | `/Admin/Reportes`                     |

Las rutas de detalle y pasos de compra reciben identificadores, por ejemplo
`/Reservas/SeleccionAsiento/123` o `/Compras/Pagar/123`. Usa preferentemente los
botones y enlaces de la aplicación para mantener la reserva seleccionada.

---

## 7. Estado del proyecto

- **Versión:** v1.0-primera-entrega