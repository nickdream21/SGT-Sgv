using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Tests de integración de SOLO LECTURA contra la BD de pruebas (sgvActualizada).
    /// Cubren regresiones concretas encontradas en la auditoría: SPs que el código llama y no
    /// existen, getdate() que vuelve a aparecer, migraciones sin aplicar, contraseñas en texto
    /// plano y columnas que esperan las grillas.
    /// </summary>
    public class BaseDeDatosTests
    {
        public BaseDeDatosTests() => BaseDePruebas.Preparar();

        private static IEnumerable<string> ArchivosCs() =>
            Directory.EnumerateFiles(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));

        [FactBD]
        public void TodosLosProcedimientosQueLlamaElCodigo_ExistenEnLaBD()
        {
            var llamados = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string archivo in ArchivosCs())
                foreach (Match m in Regex.Matches(File.ReadAllText(archivo), "\"(sp_[A-Za-z0-9_]+)\""))
                    llamados.Add(m.Groups[1].Value);

            var existentes = new HashSet<string>(
                DbHelper.ConsultarTabla("SELECT name FROM sys.procedures").Rows.Cast<DataRow>().Select(r => (string)r["name"]),
                StringComparer.OrdinalIgnoreCase);

            // "sp_getapplock"/"sp_releaseapplock" son procedimientos del sistema.
            var faltantes = llamados.Where(sp => !existentes.Contains(sp) && !sp.StartsWith("sp_getapplock") && !sp.StartsWith("sp_releaseapplock")).ToList();
            Assert.True(llamados.Count > 50, "Se esperaban más de 50 procedimientos referenciados en el código.");
            Assert.True(faltantes.Count == 0, "Procedimientos llamados desde C# que no existen en la BD: " + string.Join(", ", faltantes));
        }

        [FactBD]
        public void NingunObjetoUsaLaHoraDelServidor()
        {
            int modulos = Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM sys.sql_modules WHERE LOWER(definition) LIKE '%getdate()%' AND object_id <> OBJECT_ID('dbo.fn_AhoraPeru')"));
            int defaults = Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM sys.default_constraints WHERE LOWER(definition) LIKE '%getdate()%'"));

            Assert.Equal(0, modulos);
            Assert.Equal(0, defaults);
        }

        [FactBD]
        public void FnAhoraPeru_CoincideConFechaHelper()
        {
            var bd = Convert.ToDateTime(DbHelper.EjecutarEscalar("SELECT dbo.fn_AhoraPeru()"));
            Assert.InRange((bd - FechaHelper.Ahora()).TotalMinutes, -2, 2);
        }

        [FactBD]
        public void TodasLasMigracionesDelRepo_EstanRegistradas()
        {
            var registradas = new HashSet<string>(
                DbHelper.ConsultarTabla("SELECT archivo FROM dbo.SchemaVersion").Rows.Cast<DataRow>().Select(r => (string)r["archivo"]),
                StringComparer.OrdinalIgnoreCase);

            var pendientes = Directory.GetFiles(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Database", "Schema"), "*.sql")
                .Select(Path.GetFileName)
                .Where(a => Regex.IsMatch(a, @"^\d+_") && !a.StartsWith("00_") && !registradas.Contains(a))
                .ToList();

            Assert.True(pendientes.Count == 0,
                "Migraciones sin aplicar en pruebas (correr Database/aplicar-migraciones.ps1): " + string.Join(", ", pendientes));
        }

        /// <summary>
        /// E6-19: los catálogos ya no cuentan antes de escribir; el duplicado lo frenan estos índices únicos (la
        /// migración 64 los crea si faltan) y los servicios lo reconocen por su nombre.
        /// </summary>
        [FactBD]
        public void IndicesUnicosDeLosCatalogos_ExistenConSusColumnas()
        {
            var esperados = new[]
            {
                (Tabla: "dbo.Planta", Indice: WebSGV.Services.Catalogos.PlantaService.IndiceNombreAmbito, Columnas: "nombre,esInternacional", Filtro: (string)null),
                (Tabla: "dbo.Cliente", Indice: WebSGV.Services.Clientes.ClienteService.IndiceRuc, Columnas: "ruc", Filtro: "([ruc] IS NOT NULL)"),
                (Tabla: "dbo.EstacionesPeaje", Indice: WebSGV.Services.Catalogos.EstacionPeajeService.IndiceNombre, Columnas: "nombre", Filtro: (string)null),
            };
            foreach (var e in esperados)
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT c.name, i.filter_definition, i.is_disabled FROM sys.indexes i
                      JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                      JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE i.object_id = OBJECT_ID(@t) AND i.name = @i AND i.is_unique = 1
                      ORDER BY ic.key_ordinal",
                    DbHelper.Param("@t", e.Tabla), DbHelper.Param("@i", e.Indice));
                Assert.True(dt.Rows.Count > 0, $"Falta el índice único {e.Indice} en {e.Tabla} (migración 64).");
                Assert.Equal(e.Columnas, string.Join(",", dt.Rows.Cast<DataRow>().Select(r => (string)r["name"])));
                Assert.False(Convert.ToBoolean(dt.Rows[0]["is_disabled"]), e.Indice + " está deshabilitado.");
                Assert.Equal(e.Filtro, dt.Rows[0]["filter_definition"] as string);
            }

            // Exactamente esos tres únicos (sin contar las PK): otro único con otro nombre no lo reconocería
            // EsClaveDuplicada y su violación saldría como error técnico.
            var unicos = DbHelper.ConsultarTabla(
                    @"SELECT i.name FROM sys.indexes i
                      WHERE i.object_id IN (OBJECT_ID('dbo.Planta'), OBJECT_ID('dbo.Cliente'), OBJECT_ID('dbo.EstacionesPeaje'))
                        AND i.is_unique = 1 AND i.is_primary_key = 0
                      ORDER BY i.name")
                .Rows.Cast<DataRow>().Select(r => (string)r["name"]).ToArray();
            Assert.Equal(esperados.Select(e => e.Indice).OrderBy(n => n, StringComparer.Ordinal).ToArray(), unicos);
        }

        [FactBD]
        public void ColumnasDeTextosDeLaLiquidacion_AlcanzanLoQueValidaElServidor()
        {
            // Si una columna es más corta que lo que deja pasar ReglasLiquidacionConductor, el texto se
            // corta (o falla) al guardar y el PDF firmado queda incompleto (migración 45).
            int Largo(string tabla, string columna) => Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c",
                DbHelper.Param("@t", tabla), DbHelper.Param("@c", columna)));

            Assert.True(Largo("CategoriasAdicionales", "nombreCategoria") >= Services.Conductor.ReglasLiquidacionConductor.LargoConcepto);
            Assert.True(Largo("CategoriasAdicionales", "descripcion") >= Services.Conductor.ReglasLiquidacionConductor.LargoDetalle);
            Assert.True(Largo("IngresosAdicionales", "nombreCategoria") >= Services.Conductor.ReglasLiquidacionConductor.LargoConcepto);
            Assert.True(Largo("IngresosAdicionales", "descripcion") >= Services.Conductor.ReglasLiquidacionConductor.LargoDetalle);
            Assert.True(Largo("DetallePeajes", "estacion") >= Services.Conductor.ReglasLiquidacionConductor.LargoEstacion);
            Assert.True(Largo("DetallePeajes", "numeroComprobante") >= Services.Conductor.ReglasLiquidacionConductor.LargoComprobante);
            Assert.True(Largo("Egresos", "descPeajes") >= Services.Conductor.ReglasLiquidacionConductor.LargoDescPeajes);
            Assert.True(Largo("Egresos", "descAlimentacion") >= Services.Conductor.ReglasLiquidacionConductor.LargoDetalle);
        }

        [FactBD]
        public void NingunaColumnaNiParametro_UsaFloatOReal()
        {
            // Migración 51: el dinero de la liquidación era FLOAT (12.35 se guardaba como 12.3499999...).
            // Ninguna tabla ni SP debe volver a usar un tipo binario aproximado (float = 62, real = 59).
            var columnas = DbHelper.ConsultarTabla(@"
                SELECT t.name + '.' + c.name AS columna
                FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
                WHERE c.system_type_id IN (59, 62)").Rows.Cast<DataRow>().Select(r => (string)r["columna"]).ToList();
            Assert.True(columnas.Count == 0, "Columnas float/real: " + string.Join(", ", columnas));

            var parametros = DbHelper.ConsultarTabla(@"
                SELECT OBJECT_NAME(p.object_id) + ' ' + p.name AS parametro
                FROM sys.parameters p
                WHERE p.system_type_id IN (59, 62)").Rows.Cast<DataRow>().Select(r => (string)r["parametro"]).ToList();
            Assert.True(parametros.Count == 0, "Parámetros float/real: " + string.Join(", ", parametros));
        }

        [FactBD]
        public void MontosDeLaLiquidacion_SonDecimal12_2()
        {
            // Migración 51: las 30 columnas de dinero de Ingresos, Egresos, IngresosAdicionales y
            // CategoriasAdicionales, con 2 decimales como valida MontoHelper.
            var esperadas = new Dictionary<string, string[]>
            {
                ["Ingresos"] = new[] { "despachoSoles", "despachoDolares", "prestamoSoles", "prestamosDolares",
                    "mensualidadSoles", "mensualidadDolares", "otrosSoles", "otrosDolares", "totalSoles", "totalDolares" },
                ["Egresos"] = new[] { "peajesSoles", "peajesDolares", "alimentacionSoles", "alimentacionDolares",
                    "apoyoseguridadSoles", "apoyoseguridadDolares", "reparacionesVariosSoles", "repacionesVariosDolares",
                    "movilidadSoles", "movilidadDolares", "encarpada_desencarpadaSoles", "encarpada_desencarpadaDolares",
                    "hospedajeSoles", "hospedajeDolares", "combustibleSoles", "combustibleDolares" },
                ["IngresosAdicionales"] = new[] { "soles", "dolares" },
                ["CategoriasAdicionales"] = new[] { "soles", "dolares" }
            };
            Assert.Equal(30, esperadas.Sum(e => e.Value.Length));

            var tipos = DbHelper.ConsultarTabla(@"
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('Ingresos', 'Egresos', 'IngresosAdicionales', 'CategoriasAdicionales')")
                .Rows.Cast<DataRow>()
                .ToDictionary(r => (string)r["TABLE_NAME"] + "." + (string)r["COLUMN_NAME"],
                              r => r["DATA_TYPE"] + "(" + r["NUMERIC_PRECISION"] + "," + r["NUMERIC_SCALE"] + ")",
                              StringComparer.OrdinalIgnoreCase);

            var mal = esperadas.SelectMany(e => e.Value.Select(c => e.Key + "." + c))
                .Where(c => !tipos.TryGetValue(c, out string tipo) || tipo != "decimal(12,2)")
                .Select(c => c + " = " + (tipos.TryGetValue(c, out string tipo) ? tipo : "no existe"))
                .ToList();
            Assert.True(mal.Count == 0, "Montos que no son decimal(12,2): " + string.Join(", ", mal));
        }

        [FactBD]
        public void TablasDeAuditoria_TienenSusTriggersDeSoloInsercionHabilitados()
        {
            // Migración 47: si alguien los deshabilita o borra, esta prueba lo delata.
            var habilitados = new HashSet<string>(
                DbHelper.ConsultarTabla(@"SELECT name FROM sys.triggers
                                          WHERE parent_id IN (OBJECT_ID('dbo.AuditoriaLog'), OBJECT_ID('dbo.Auditoria'))
                                            AND is_instead_of_trigger = 1 AND is_disabled = 0")
                       .Rows.Cast<DataRow>().Select(r => (string)r["name"]),
                StringComparer.OrdinalIgnoreCase);

            var esperados = new[] { "trg_AuditoriaLog_NoUpdate", "trg_AuditoriaLog_NoDelete", "trg_Auditoria_NoUpdate", "trg_Auditoria_NoDelete" };
            var faltan = esperados.Where(t => !habilitados.Contains(t)).ToList();
            Assert.True(faltan.Count == 0, "Triggers de auditoría ausentes o deshabilitados: " + string.Join(", ", faltan));
        }

        [FactBD]
        public void DatosFinancieros_TienenUnaSolaFilaPorOrden()
        {
            // Migración 48: una segunda fila de Ingresos/Egresos/DescuentosReintegros duplicaría los montos en los LEFT JOIN.
            var unicos = new HashSet<string>(
                DbHelper.ConsultarTabla(@"SELECT name FROM sys.indexes
                                          WHERE is_unique = 1 AND object_id IN (OBJECT_ID('dbo.Ingresos'), OBJECT_ID('dbo.Egresos'), OBJECT_ID('dbo.DescuentosReintegros'))")
                       .Rows.Cast<DataRow>().Select(r => (string)r["name"]),
                StringComparer.OrdinalIgnoreCase);

            var esperados = new[] { "UX_Ingresos_Orden", "UX_Egresos_Orden", "UX_DescuentosReintegros_Orden" };
            var faltan = esperados.Where(i => !unicos.Contains(i)).ToList();
            Assert.True(faltan.Count == 0, "Índices únicos por orden ausentes: " + string.Join(", ", faltan));
        }

        [FactBD]
        public void NingunUsuarioTieneContrasenaEnTextoPlano()
        {
            var sinHash = DbHelper.ConsultarTabla("SELECT idUsuario, contrasena FROM Usuarios").Rows.Cast<DataRow>()
                .Where(r => !string.IsNullOrEmpty(r["contrasena"] as string) && PasswordHelper.NeedsMigration((string)r["contrasena"]))
                .Select(r => Convert.ToInt32(r["idUsuario"]))
                .ToList();

            Assert.True(sinHash.Count == 0, "Usuarios con contraseña sin hash: " + string.Join(", ", sinHash));
        }

        [FactBD]
        public void HistorialViajesConductor_DevuelveLasColumnasDeLaGrilla()
        {
            DataTable dt = RegistroDespachoService.ObtenerHistorialViajesConductor(-1);

            foreach (string col in new[] { "IdViajeProgreso", "NumeroViajeProgreso", "FechaInicio", "FechaCierre",
                                           "CantidadDespachos", "TipoViaje", "EstadoViaje" })
                Assert.True(dt.Columns.Contains(col), "Falta la columna " + col);
        }

        [FactBD]
        public void FiltrosDeListaDespachos_DevuelvenLasColumnasEsperadas()
        {
            DataTable conductores = ListaDespachosService.ObtenerConductoresConViajes();
            DataTable clientes = ListaDespachosService.ObtenerClientesRecientes();

            Assert.True(conductores.Columns.Contains("NombreCompleto") && conductores.Columns.Contains("idConductor"));
            Assert.True(clientes.Columns.Contains("nombre") && clientes.Columns.Contains("idCliente"));
        }

        [FactBD]
        public void ReabrirViajeInexistente_LlegaComoMensajeDeNegocio()
        {
            // RAISERROR del SP (número 50000) → MensajeError lo muestra tal cual. No modifica datos.
            var ex = Assert.Throws<SqlException>(() => RegistroDespachoService.ReabrirViajeProgreso(-1, "test"));
            Assert.Equal("El viaje no existe o está inactivo.", MensajeError.ExtraerMensajeDeNegocio(ex));
        }
    }
}
