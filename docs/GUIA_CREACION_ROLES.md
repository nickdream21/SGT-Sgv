# Guía de Creación de Roles - Sistema SGV

## Índice
1. [Estructura de la Base de Datos](#estructura-de-la-base-de-datos)
2. [Roles Existentes](#roles-existentes)
3. [Flujo Completo para Crear un Nuevo Rol](#flujo-completo-para-crear-un-nuevo-rol)
4. [Archivos Involucrados](#archivos-involucrados)
5. [Ejemplo Paso a Paso](#ejemplo-paso-a-paso)
6. [Notas Importantes](#notas-importantes)

---

## Estructura de la Base de Datos

### Tabla `Usuarios`

| Columna         | Tipo           | Descripción                                      |
|-----------------|----------------|--------------------------------------------------|
| `idUsuario`     | `INT (PK, AI)` | Identificador único auto-incremental             |
| `nombreUsuario` | `VARCHAR`       | Login del usuario (único)                        |
| `nombre`        | `VARCHAR`       | Nombre completo para mostrar en la interfaz      |
| `contrasena`    | `VARCHAR`       | Hash PBKDF2 en formato `sha256.{iter}.{salt}.{hash}` |
| `rol`           | `VARCHAR`       | Nombre del rol (texto libre, se compara en UPPER) |
| `activo`        | `BIT`           | 1 = activo, 0 = inactivo                        |
| `idConductor`   | `INT (FK, NULL)`| Solo para rol CONDUCTOR, referencia a tabla Conductores |

> **Nota:** Los roles NO están en una tabla separada. Se almacenan como cadena de texto en la columna `rol` de la tabla `Usuarios`. La validación se hace por comparación de strings en el código C#.

### Formato de Contraseña (PBKDF2)

```
sha256.{iteraciones}.{salt_base64}.{hash_base64}
Ejemplo: sha256.310000.{salt de 16 bytes en base64, 24 caracteres}.{hash de 32 bytes en base64, 44 caracteres}
```

- **Algoritmo:** PBKDF2-HMAC-SHA256.
- **Iteraciones:** 310,000 por defecto (appSettings `Seguridad.IteracionesContrasena` en `Web.config`, mínimo 100,000).
- **Salt:** 16 bytes aleatorios (128 bits)
- **Hash:** 32 bytes (256 bits)
- **Formato anterior:** `{iteraciones}.{salt}.{hash}` (PBKDF2-HMAC-SHA1, 10,000 iteraciones). Se sigue aceptando y se convierte al formato nuevo en el siguiente inicio de sesión correcto (`Seguridad.ConvertirHashAlIngresar`).
- **Texto plano:** ya no se acepta.

---

## Roles Existentes

| Rol                        | Constante en `RolesHelper.cs` | Página de inicio (`UrlInicioSegunRol`) | Alcance |
|----------------------------|-------------------------------|----------------------------------------|---------|
| `ADMIN` (también `ADMINISTRADOR`) | `ROL_ADMIN`            | `Inicio.aspx`                          | Operación completa (Despacho, OV, Registro, liquidaciones, reportes) |
| `ADMINISTRADOR DE TRANSPORTE` | `ROL_ADMIN_TRANSPORTE`     | `Inicio.aspx`                          | Igual que `ADMIN` (`EsRolAdmin`) |
| `ADMINISTRADOR DE SISTEMA` | `ROL_ADMIN_SISTEMA`           | `DashboardAdminSistema.aspx`           | Solo gestión de usuarios y auditoría; no opera |
| `SUPERVISOR`               | `ROL_SUPERVISOR`              | `Inicio.aspx`                          | Similar a Admin (sin dashboard de conductor) |
| `CONDUCTOR` (también `CHOFER`) | `ROL_CONDUCTOR`           | `DashboardConductor.aspx`              | Dashboard Conductor |
| `CONTABILIDAD`             | `ROL_CONTABILIDAD`            | `LiquidacionesAprobadasContabilidad.aspx` | Consulta de liquidaciones aprobadas |

La matriz completa de permisos por sección está en `docs/FLUJO_DE_TRABAJO_SGV.md`.

Roles retirados (2026-11): `ADMINISTRADOR DE GRIFO`, `ADMINISTRADOR DE MAQUINARIA` y `OPERADOR`. Un usuario antiguo con uno de ellos no tiene página de inicio ni permisos: al entrar vuelve al login. Gestión de Usuarios ya no los ofrece (`RolesHelper.CombinarRolesDisponibles` solo agrega de la BD roles vigentes), pero al editar a ese usuario muestra su rol actual para cambiarlo.

----------------------------|-------------------------------|---------------------------|-----------------------------|
| `ADMIN`                    | `ROL_ADMIN`                   | `Inicio.aspx`            | Menú completo (Despacho, OV, Registro) |
| `ADMINISTRADOR DE SISTEMA` | `ROL_ADMIN_SISTEMA`           | `Inicio.aspx`            | Menú Admin + Auditoría      |
| `CONDUCTOR`                | `ROL_CONDUCTOR`               | `DashboardConductor.aspx`| Dashboard Conductor         |
| `SUPERVISOR`               | `ROL_SUPERVISOR`              | `Inicio.aspx`            | Similar a Admin             |

---

## Flujo Completo para Crear un Nuevo Rol

### Paso 1: Definir constante en `RolesHelper.cs`

Archivo: `WebSGV/Views/RolesHelper.cs`

```csharp
public const string ROL_NUEVO = "NOMBRE DEL ROL";
```

### Paso 2: Agregar método verificador en `RolesHelper.cs`

```csharp
public static bool EsNuevoRol()
{
    string rolActual = ObtenerRolActual();
    return rolActual == ROL_NUEVO;
}
```

### Paso 3: Configurar permisos en `TienePermiso()` de `RolesHelper.cs`

Agregar el nuevo rol a los `case` que correspondan dentro de `TienePermiso(seccion, rol)` (la matriz pura; `TienePermiso(seccion)` solo le pasa el rol de la sesión):

```csharp
case "SECCION_X":
    return admin || r == ROL_NUEVO;
```

Luego agregar el rol en `WebSGV.Tests/RolesHelperTests.cs`: a `RolesProbados` y a las secciones de `MatrizEsperada` que le correspondan, y actualizar la matriz de `docs/FLUJO_DE_TRABAJO_SGV.md`. La prueba `ConstantesDeRol_EstanEnRolesProbados` falla si una constante `ROL_*` no está en `RolesProbados`.

### Paso 4: Agregar propiedad en `Site.Master.cs`

Archivo: `WebSGV/Site.Master.cs`

1. Agregar propiedad pública:
```csharp
public bool EsNuevoRol { get; set; }
```

2. Inicializar en `InicializarPropiedadesVacias()`:
```csharp
EsNuevoRol = false;
```

3. Cargar en `CargarInformacionUsuario()`:
```csharp
EsNuevoRol = RolesHelper.EsNuevoRol();
```

### Paso 5: Agregar sección de menú en `Site.Master`

Archivo: `WebSGV/Site.Master`

Agregar un bloque condicional entre los bloques de menú existentes:

```aspx
<!-- ==================== MENÚ PARA NUEVO ROL ==================== -->
<% if (EsNuevoRol) { %>
    <li class="nav-item">
        <a class="nav-link" href="PaginaInicio.aspx">
            <i class="fas fa-home"></i>Inicio
        </a>
    </li>
    <!-- Agregar más items de menú según sea necesario -->
<% } %>
```

### Paso 6: Configurar la página de inicio en `RolesHelper.UrlInicioSegunRol()`

Es la única fuente de la página de inicio por rol (login, `RedirigirSegunRol()`, guardias de `SecurityHelper` y logo del menú). Agregar el `case`:

```csharp
case ROL_NUEVO: return "~/Views/PaginaDestino.aspx";
```

Y agregar el caso a `UrlInicioSegunRol_RolConocido_SuPagina` en `RolesHelperTests`.

### Paso 7: (ya no aplica)

`Login.aspx.cs` y `RolesHelper.RedirigirSegunRol()` usan `UrlInicioSegunRol`: no se agregan `else if` por rol.

### Paso 8: Crear script SQL para insertar usuario

```sql
INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
VALUES ('login_usuario', 'Nombre Completo', '<hash_pbkdf2>', 'NOMBRE DEL ROL', 1);
```

---

## Archivos Involucrados

| Archivo                          | Propósito                                      |
|----------------------------------|------------------------------------------------|
| `WebSGV/Views/RolesHelper.cs`   | Constantes de rol, verificadores, permisos     |
| `WebSGV/Site.Master.cs`         | Propiedades booleanas del rol para la vista     |
| `WebSGV/Site.Master`            | Menú de navegación condicional por rol          |
| `WebSGV.Tests/RolesHelperTests.cs` | Matriz de permisos y páginas de inicio (pruebas) |
| `WebSGV/Helpers/PasswordHelper.cs` | Hashing PBKDF2 de contraseñas               |
| `WebSGV/Database/Scripts/`      | Scripts SQL para insertar usuarios con el rol   |

---

## Ejemplo Paso a Paso

### Así se creó el rol "CONTABILIDAD"

1. **`RolesHelper.cs`** → `ROL_CONTABILIDAD = "CONTABILIDAD"`, su etiqueta "Contabilidad" en `EtiquetasRol` y `EsContabilidad()`
2. **`RolesHelper.TienePermiso`** → sección `LIQUIDACIONES_CONTABILIDAD` (`r == ROL_CONTABILIDAD || admin || supervisor`); en `RolesHelperTests`, el rol en `RolesProbados` y la fila de `MatrizEsperada`
3. **`SecurityHelper.cs`** → `EsContabilidad()` y `PuedeConsultarLiquidacionesGlobales()` (admin, supervisor o contabilidad)
4. **`Site.Master.cs`** → propiedad `EsContabilidad` (en `InicializarPropiedadesVacias` y `CargarInformacionUsuario`)
5. **`Site.Master`** → bloque `<% if (EsContabilidad) { %>` con su menú
6. **`RolesHelper.UrlInicioSegunRol`** → `case ROL_CONTABILIDAD: return "~/Views/LiquidacionesAprobadasContabilidad.aspx";`
7. **SQL** → `Database/Scripts/script_AgregarRolContabilidad.sql` (alta idempotente de un usuario con rol `'CONTABILIDAD'`)

---

## Notas Importantes

1. **Los roles son case-insensitive** en la práctica: el código siempre usa `.ToUpper()` para comparar.
2. **No hay tabla de roles**: Los roles se definen como strings en la columna `rol` de `Usuarios`. No existe una tabla `Roles` separada.
3. **La contraseña se auto-migra**: Si insertas la contraseña en texto plano, al primer login exitoso se convertirá automáticamente a hash PBKDF2. Sin embargo, **se recomienda insertar siempre con hash PBKDF2** por seguridad.
4. **`idConductor`** solo aplica para el rol `CONDUCTOR`. Para otros roles debe ser `NULL`.
5. **Verificación de roles existentes**: `SELECT DISTINCT rol FROM Usuarios WHERE activo = 1;`
6. **Convención de nombres**: Usar `ROL_NOMBRE_ROL` para constantes y `EsNombreRol()` para métodos verificadores.

### Generar Hash PBKDF2 desde PowerShell

```powershell
$password = "MiContrasena123"
$saltSize = 16; $hashSize = 32; $iterations = 310000
$salt = New-Object byte[] $saltSize
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$hash = $pbkdf2.GetBytes($hashSize)
"sha256.$iterations.$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($hash))"
```
