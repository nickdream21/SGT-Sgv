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
