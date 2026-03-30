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
| `contrasena`    | `VARCHAR`       | Hash PBKDF2 en formato `{iter}.{salt}.{hash}`    |
| `rol`           | `VARCHAR`       | Nombre del rol (texto libre, se compara en UPPER) |
| `activo`        | `BIT`           | 1 = activo, 0 = inactivo                        |
| `idConductor`   | `INT (FK, NULL)`| Solo para rol CONDUCTOR, referencia a tabla Conductores |

> **Nota:** Los roles NO están en una tabla separada. Se almacenan como cadena de texto en la columna `rol` de la tabla `Usuarios`. La validación se hace por comparación de strings en el código C#.

### Formato de Contraseña (PBKDF2)

```
{iteraciones}.{salt_base64}.{hash_base64}
Ejemplo: 10000.VKBVqQUdtJXeuB9LCfs3kw==.xTgbab/WEiGQBTxCOVnE6krT39E5bKsLRfYGENexoPk=
```

- **Iteraciones:** 10,000 (constante en `PasswordHelper.cs`)
- **Salt:** 16 bytes aleatorios (128 bits)
- **Hash:** 32 bytes (256 bits)
- **Soporte legacy:** Si la contraseña está en texto plano, el sistema la migra automáticamente al primer login exitoso.

---

## Roles Existentes

| Rol                        | Constante en `RolesHelper.cs` | Redirección Login         | Menú en `Site.Master`       |
|----------------------------|-------------------------------|---------------------------|-----------------------------|
| `ADMIN`                    | `ROL_ADMIN`                   | `Inicio.aspx`            | Menú completo (Despacho, OV, Registro) |
| `ADMINISTRADOR DE SISTEMA` | `ROL_ADMIN_SISTEMA`           | `Inicio.aspx`            | Menú Admin + Auditoría      |
| `CONDUCTOR`                | `ROL_CONDUCTOR`               | `DashboardConductor.aspx`| Dashboard Conductor         |
| `ADMINISTRADOR DE GRIFO`   | `ROL_ADMIN_GRIFO`             | `DashboardGrifo.aspx`    | Abastecimiento + Conductores |
| `SUPERVISOR`               | `ROL_SUPERVISOR`              | `Inicio.aspx`            | Similar a Admin             |
| `ADMINISTRADOR DE MAQUINARIA` | `ROL_ADMIN_MAQUINARIA`     | `Inicio.aspx`            | Menú básico (configurable)  |
| `OPERADOR`                 | `ROL_OPERADOR`                | `DashboardOperador.aspx` | Dashboard Operador (Parte Diario) |

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

Agregar el nuevo rol a los `case` que correspondan dentro del método `TienePermiso()`:

```csharp
case "SECCION_X":
    return EsAdmin() || EsNuevoRol();
```

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

### Paso 6: Configurar redirección en `Login.aspx.cs`

Archivo: `WebSGV/Views/Login.aspx.cs`

Agregar condición en `btnLogin_Click` (antes del `else` final) y en `Page_Load`:

```csharp
else if (resultado.Rol.ToUpper() == "NOMBRE DEL ROL")
{
    Response.Redirect("~/Views/PaginaDestino.aspx");
}
```

### Paso 7: Actualizar redirección en `RolesHelper.RedirigirSegunRol()`

```csharp
else if (EsNuevoRol())
{
    HttpContext.Current.Response.Redirect("~/Views/PaginaDestino.aspx");
}
```

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
| `WebSGV/Views/Login.aspx.cs`    | Redirección post-login según rol                |
| `WebSGV/Helpers/PasswordHelper.cs` | Hashing PBKDF2 de contraseñas               |
| `WebSGV/Database/Scripts/`      | Scripts SQL para insertar usuarios con el rol   |

---

## Ejemplo Paso a Paso

### Crear rol "ADMINISTRADOR DE MAQUINARIA"

1. **`RolesHelper.cs`** → Agregar `ROL_ADMIN_MAQUINARIA` + `EsAdminMaquinaria()`
2. **`Site.Master.cs`** → Agregar propiedad `EsAdminMaquinaria`
3. **`Site.Master`** → Agregar bloque de menú condicional
4. **`Login.aspx.cs`** → Agregar `else if` para redirigir a `Inicio.aspx`
5. **SQL** → `INSERT INTO Usuarios ... VALUES (..., 'ADMINISTRADOR DE MAQUINARIA', 1)`

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
$saltSize = 16; $hashSize = 32; $iterations = 10000
$salt = New-Object byte[] $saltSize
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($salt)
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations)
$hash = $pbkdf2.GetBytes($hashSize)
"$iterations.$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($hash))"
```
