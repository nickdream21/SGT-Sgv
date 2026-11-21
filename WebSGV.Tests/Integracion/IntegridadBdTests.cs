using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Integridad del esquema (Etapa 3, E3-1), SOLO LECTURA. Corre contra sgvActualizada y contra un
    /// ensayo local (herramientas/respaldo/ensayar-migracion.ps1). Las listas de excepciones son
    /// explícitas: al retirar algo en E3-8 o al poner una PK, se quita de aquí.
    /// </summary>
    public class IntegridadBdTests
    {
        public IntegridadBdTests() => BaseDePruebas.Preparar();

        /// <summary>Tablas sin PK aceptadas por ahora (vacías; E3-6 o E3-8 deciden).</summary>
        private static readonly string[] TablasSinPkPermitidas = { "CPIC_Productos" };

        /// <summary>
        /// Objetos de la BD sin archivo en el repo que se borran en E3-8 (migración 57; Grifo y
        /// Maquinaria, M-048 y M-049). No añadir nada nuevo: un objeto nuevo va con su archivo.
        /// </summary>
        private static readonly string[] PorRetirarEnE3_8 =
        {
            // 17 procedimientos
            "sp_InsertarAbastecimientoCombustible", "sp_InsertarDespachoObra", "sp_InsertarIndicador",
            "sp_InsertarIngresoEcuador", "sp_MQ_GenerarNumeroParte", "sp_MQ_InsertarParteDiario",
            "sp_MQ_ObtenerAsignacionActiva", "sp_MQ_ObtenerDatosOperador", "sp_MQ_ObtenerHistorialPartes",
            "sp_SE_BuscarDespachosDisponibles", "sp_SE_Dashboard_Graficos", "sp_SE_Dashboard_KPIs",
            "sp_SE_Insertar", "sp_SE_Listar", "sp_SE_ListarEnCurso", "sp_SE_ObtenerDespachoPorId",
            "sp_SE_ObtenerPorId",
            // 5 vistas y 1 función
            "vw_DespachosCompletos", "vw_Auditoria", "vw_IndicadoresExcelCompatible",
            "VW_OperacionesConPlantas", "vw_SegmentosCompletos", "fn_ValidarDisponibilidadRecursos",
            // Trigger de la tabla de Grifo AbastecimientoCombustible (fuera de la migración 52 a propósito)
            "TR_AbastecimientoCombustible_Auditoria",
        };

        private static readonly Regex CreacionDeObjeto = new Regex(
            @"\b(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+(?:PROC|PROCEDURE|VIEW|FUNCTION|TRIGGER)\s+(?:\[?dbo\]?\s*\.\s*)?\[?([A-Za-z0-9_]+)\]?",
            RegexOptions.IgnoreCase);

        /// <summary>Nombres creados en Database/StoredProcedures o Database/Schema.</summary>
        private static HashSet<string> ObjetosConArchivo()
        {
            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string dirDatabase = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Database");
            foreach (string carpeta in new[] { "StoredProcedures", "Schema" })
                foreach (string archivo in Directory.GetFiles(Path.Combine(dirDatabase, carpeta), "*.sql"))
                    foreach (Match m in CreacionDeObjeto.Matches(File.ReadAllText(archivo)))
                        nombres.Add(m.Groups[1].Value);
            return nombres;
        }

        private static List<string> Nombres(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0])).ToList();

        [FactBD]
        public void TodaTablaDeUsuario_TienePk()
        {
            var sinPk = Nombres(@"SELECT t.name FROM sys.tables t
                                  WHERE t.is_ms_shipped = 0
                                    AND NOT EXISTS (SELECT 1 FROM sys.key_constraints k
                                                    WHERE k.parent_object_id = t.object_id AND k.type = 'PK')")
                        .Where(t => !TablasSinPkPermitidas.Contains(t, StringComparer.OrdinalIgnoreCase))
                        .ToList();

            Assert.True(sinPk.Count == 0, "Tablas sin clave primaria: " + string.Join(", ", sinPk));
        }

        [FactBD]
        public void TodoProcedimientoVistaFuncionYTrigger_TieneArchivoOEstaPorRetirar()
        {
            var conArchivo = ObjetosConArchivo();
            Assert.True(conArchivo.Count > 100, "Se esperaban más de 100 objetos creados en Database/.");

            var enBd = DbHelper.ConsultarTabla(@"SELECT o.name, o.type_desc FROM sys.objects o
                                                 WHERE o.is_ms_shipped = 0
                                                   AND o.type IN ('P', 'V', 'FN', 'IF', 'TF', 'TR')")
                               .Rows.Cast<DataRow>()
                               .Select(r => new { Nombre = (string)r["name"], Tipo = (string)r["type_desc"] })
                               .ToList();

            var sinArchivo = enBd
                .Where(o => !conArchivo.Contains(o.Nombre) && !PorRetirarEnE3_8.Contains(o.Nombre, StringComparer.OrdinalIgnoreCase))
                .Select(o => $"{o.Nombre} ({o.Tipo})")
                .OrderBy(x => x)
                .ToList();

            Assert.True(sinArchivo.Count == 0,
                "Objetos de la BD sin archivo en Database/StoredProcedures ni Database/Schema: " + string.Join(", ", sinArchivo));
        }

        [FactBD]
        public void ListaPorRetirar_NoTieneObjetosConArchivo()
        {
            // Si uno de la lista recibe su archivo, deja de estar "por retirar": se quita de la lista.
            var conArchivo = ObjetosConArchivo();
            var sobran = PorRetirarEnE3_8.Where(conArchivo.Contains).ToList();
            Assert.True(sobran.Count == 0, "Objetos de PorRetirarEnE3_8 que ya tienen archivo: " + string.Join(", ", sobran));
        }

        [FactBD]
        public void FirmaDigital_TieneSusTriggersDeSoloInsercionHabilitados()
        {
            var habilitados = Nombres(@"SELECT name FROM sys.triggers
                                        WHERE parent_id = OBJECT_ID('dbo.FirmaDigital') AND is_disabled = 0");

            var faltan = new[] { "trg_FirmaDigital_NoUpdate", "trg_FirmaDigital_NoDelete" }
                .Where(t => !habilitados.Contains(t, StringComparer.OrdinalIgnoreCase))
                .ToList();
            Assert.True(faltan.Count == 0, "Triggers de FirmaDigital ausentes o deshabilitados: " + string.Join(", ", faltan));
        }

        [FactBD]
        public void TriggersDeAuditoriaHeredados_ExistenYEstanHabilitados()
        {
            // Migración 52: llenan dbo.Auditoria; sp_DC_GenerarNumeroOrden la lee para no repetir números.
            var habilitados = Nombres("SELECT name FROM sys.triggers WHERE parent_class = 1 AND is_disabled = 0");
            var esperados = new[]
            {
                "TR_Carreta_Auditoria", "TR_Conductor_Auditoria", "TR_CPIC_Auditoria", "TR_CPIC_Productos_Auditoria",
                "TR_Factura_Auditoria", "TR_GuiasTransportista_Auditoria", "TR_Indicadores_Auditoria",
                "TR_Liquidaciones_Auditoria", "TR_OrdenViaje_Auditoria", "TR_Tracto_Auditoria", "TR_UploadHistory_Auditoria",
            };
            var faltan = esperados.Where(t => !habilitados.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(faltan.Count == 0, "Triggers de auditoría heredados ausentes o deshabilitados: " + string.Join(", ", faltan));
        }

        [FactBD]
        public void NingunaFkNiCheck_EstaSinValidarODeshabilitada()
        {
            var mal = Nombres(@"SELECT 'FK ' + name FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1
                                UNION ALL
                                SELECT 'CHECK ' + name FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1");

            Assert.True(mal.Count == 0, "Restricciones sin validar (is_not_trusted) o deshabilitadas: " + string.Join(", ", mal));
        }
    }
}
