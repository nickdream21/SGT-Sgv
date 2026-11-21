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

        /// <summary>CHECK de dominio y de montos de la migración 54 (E3-6).</summary>
        private static readonly string[] ChecksMigracion54 =
        {
            "CK_Despachos_Estado", "CK_Despachos_TipoOperacion", "CK_Despachos_AnuladoInactivo",
            "CK_ViajesEnProgreso_Estado", "CK_OrdenViaje_EstadoAprobacion", "CK_OrdenViaje_EstadoViaje",
            "CK_SeguimientoExportacion_Estado", "CK_SeguimientoExportacion_Origen",
            "CK_FirmaDigital_Etapa", "CK_FirmaDigital_EstadoFirma", "CK_Usuarios_Rol", "CK_FacturaFlete_Moneda",
            "CK_DetalleAlimentacion_Montos", "CK_DetalleApoyoSeguridad_Montos", "CK_DetalleCombustible_Montos",
            "CK_DetalleEncapada_Montos", "CK_DetalleHospedaje_Montos", "CK_DetalleMovilidad_Montos",
            "CK_DetallePeajes_Montos", "CK_DetalleReparacionesVarios_Montos", "CK_Egresos_Montos", "CK_Ingresos_Montos",
            "CK_IngresosAdicionales_Montos", "CK_CategoriasAdicionales_Montos", "CK_DescuentosReintegros_Montos",
            "CK_Factura_ValorTotal", "CK_CPIC_Montos", "CK_FacturaFlete_Montos", "CK_FacturaFleteItem_Montos",
            "CK_DespachoCarga_Pesos",
        };

        /// <summary>CHECK anteriores a la Etapa 3 (y el de la migración 53).</summary>
        private static readonly string[] ChecksAnteriores =
        {
            "CK_Cliente_Moneda", "CK_Correlativo_ultimo", "CK_DespachoCarga_Cantidad", "CK_DocumentosGuia_Tipo",
            "CK_DocumentosManifiesto_Tipo", "CK_Factura_Moneda", "CK_MovimientoDineroViaje_Medio",
            "CK_MovimientoDineroViaje_Montos", "CK_MovimientoDineroViaje_Tipo", "CK_OnwayAuthCache_UnaFila",
            "CK_PagoSaldoConductor_Medio",
        };

        /// <summary>Las 10 FK de la migración 55 y las dos de documentos rehechas sin cascada.</summary>
        private static readonly string[] FksMigracion55 =
        {
            "FK_FirmaDigital_UsuarioFirmante", "FK_MovimientoDineroViaje_ViajeProgreso",
            "FK_MovimientoDineroViaje_UsuarioRegistro", "FK_MovimientoDineroViaje_UsuarioAnulacion",
            "FK_OrdenViajeAjuste_UsuarioAdmin", "FK_SeguimientoExportacion_DespachoOrigen",
            "FK_SeguimientoExportacion_DespachoDestino", "FK_SeguimientoExportacion_UsuarioRegistro",
            "FK_SeguimientoExportacion_UsuarioModificacion", "FK_SeguimientoHito_UsuarioCorreccion",
            "FK_DocumentosFactura_Factura", "FK_DocumentosCPIC_CPIC",
        };

        [FactBD]
        public void Los_CHECK_son_exactamente_los_esperados()
        {
            var enBd = Nombres("SELECT name FROM sys.check_constraints");
            var esperados = ChecksMigracion54.Concat(ChecksAnteriores).ToList();
            var faltan = esperados.Where(e => !enBd.Contains(e, StringComparer.OrdinalIgnoreCase)).ToList();
            var sobran = enBd.Where(e => !esperados.Contains(e, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(faltan.Count == 0 && sobran.Count == 0,
                "CHECK que faltan: " + string.Join(", ", faltan) + ". CHECK no listados (añadirlos aquí a propósito): " + string.Join(", ", sobran));
        }

        [FactBD]
        public void Las_FK_de_la_migracion_55_existen_sin_cascada()
        {
            var sinAccion = Nombres(@"SELECT name FROM sys.foreign_keys
                                      WHERE delete_referential_action = 0 AND update_referential_action = 0");
            var faltan = FksMigracion55.Where(f => !sinAccion.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(faltan.Count == 0, "FK ausentes o con cascada: " + string.Join(", ", faltan));

            var cascadaDocumentos = Nombres(@"SELECT name FROM sys.foreign_keys
                                              WHERE parent_object_id IN (OBJECT_ID('dbo.DocumentosFactura'), OBJECT_ID('dbo.DocumentosCPIC'))
                                                AND delete_referential_action <> 0");
            Assert.True(cascadaDocumentos.Count == 0, "Documentos con borrado en cascada: " + string.Join(", ", cascadaDocumentos));
        }

        [FactBD]
        public void No_hay_FK_repetidas()
        {
            var repetidas = Nombres(@"
                WITH f AS (
                    SELECT fk.object_id, fk.name, fk.parent_object_id, fk.referenced_object_id,
                           fk.delete_referential_action AS d, fk.update_referential_action AS u,
                           (SELECT CONCAT(fc.parent_column_id, '>', fc.referenced_column_id, ',')
                            FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id = fk.object_id
                            ORDER BY fc.constraint_column_id FOR XML PATH('')) AS cols
                    FROM sys.foreign_keys fk)
                SELECT OBJECT_NAME(a.parent_object_id) + '.' + a.name FROM f a
                WHERE EXISTS (SELECT 1 FROM f b WHERE b.object_id < a.object_id AND b.parent_object_id = a.parent_object_id
                                AND b.referenced_object_id = a.referenced_object_id AND b.cols = a.cols AND b.d = a.d AND b.u = a.u)");
            Assert.True(repetidas.Count == 0, "FK repetidas: " + string.Join(", ", repetidas));
        }

        [FactBD]
        public void No_hay_indices_repetidos()
        {
            // Mismas columnas clave (en orden y sentido), incluidas y filtro en la misma tabla.
            var repetidos = Nombres(@"
                WITH i AS (
                    SELECT ix.object_id, ix.index_id, ix.name, ISNULL(ix.filter_definition, '') AS filtro,
                           (SELECT CONCAT(ic.column_id, CASE WHEN ic.is_descending_key = 1 THEN 'D' ELSE 'A' END, ',')
                            FROM sys.index_columns ic WHERE ic.object_id = ix.object_id AND ic.index_id = ix.index_id AND ic.is_included_column = 0
                            ORDER BY ic.key_ordinal FOR XML PATH('')) AS claves,
                           (SELECT CONCAT(ic.column_id, ',')
                            FROM sys.index_columns ic WHERE ic.object_id = ix.object_id AND ic.index_id = ix.index_id AND ic.is_included_column = 1
                            ORDER BY ic.column_id FOR XML PATH('')) AS incluidas
                    FROM sys.indexes ix JOIN sys.tables t ON t.object_id = ix.object_id
                    WHERE ix.type > 0 AND t.is_ms_shipped = 0)
                SELECT OBJECT_NAME(a.object_id) + '.' + a.name FROM i a
                WHERE EXISTS (SELECT 1 FROM i b WHERE b.object_id = a.object_id AND b.index_id < a.index_id
                                AND b.claves = a.claves AND ISNULL(b.incluidas, '') = ISNULL(a.incluidas, '') AND b.filtro = a.filtro)");
            Assert.True(repetidos.Count == 0, "Índices repetidos: " + string.Join(", ", repetidos));

            var cubiertos = Nombres(@"SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos')
                                      AND name IN ('IX_Despachos_Fecha', 'IX_Despachos_FechaDespacho', 'IX_Despachos_Conductor')");
            Assert.True(cubiertos.Count == 0, "Índices de Despachos que la migración 55 quita: " + string.Join(", ", cubiertos));
        }

        [FactBD]
        public void OrdenViaje_estadoAprobacion_es_NOT_NULL_con_DEFAULT_PENDIENTE()
        {
            DataTable t = DbHelper.ConsultarTabla(@"
                SELECT c.is_nullable, dc.definition
                FROM sys.columns c
                LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE c.object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoAprobacion'");
            Assert.Equal(1, t.Rows.Count);
            Assert.False(Convert.ToBoolean(t.Rows[0]["is_nullable"]));
            Assert.Equal("('PENDIENTE')", Convert.ToString(t.Rows[0]["definition"]));   // M-050: ya no 'APROBADO'
        }

        [FactBD]
        public void QueryStore_tiene_tope_de_100_MB_y_esta_en_READ_WRITE()
        {
            // Migración 56 (E3-7, M-053): con 1000 MB de tope podía comerse los 500 MB de datos del plan de somee.
            DataTable t = DbHelper.ConsultarTabla(@"
                SELECT actual_state_desc, desired_state_desc, max_storage_size_mb, query_capture_mode_desc, size_based_cleanup_mode_desc
                FROM sys.database_query_store_options");
            Assert.Equal(1, t.Rows.Count);
            DataRow r = t.Rows[0];
            Assert.Equal(100L, Convert.ToInt64(r["max_storage_size_mb"]));
            Assert.Equal("READ_WRITE", Convert.ToString(r["desired_state_desc"]));
            Assert.Equal("READ_WRITE", Convert.ToString(r["actual_state_desc"]));
            Assert.Equal("AUTO", Convert.ToString(r["query_capture_mode_desc"]));
            Assert.Equal("AUTO", Convert.ToString(r["size_based_cleanup_mode_desc"]));
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
