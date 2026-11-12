using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Dinero del viaje registrado al momento (tabla <c>MovimientoDineroViaje</c>, migración 42):
    /// <list type="bullet">
    /// <item><b>ENTREGA</b>: dinero que recibe el conductor (adelanto, segundo despacho, mensualidad,
    /// préstamo…), por efectivo, Yape, transferencia o en sobre. Al aprobar la
    /// liquidación se suman en "Dinero entregado".</item>
    /// <item><b>PAGO_DIRECTO</b>: la empresa paga al proveedor (Yape al del parchado…). Es costo del
    /// viaje, se ve en el PDF, pero no toca el saldo del conductor.</item>
    /// </list>
    /// Lo registran la administración y monitoreo (<see cref="PuedeRegistrar"/>).
    /// </summary>
    public static class MovimientosViajeService
    {
        public const string Entrega = "ENTREGA";
        public const string PagoDirecto = "PAGO_DIRECTO";

        /// <summary>Conceptos de una entrega y a qué fila de "Dinero entregado" suman.</summary>
        public static readonly IReadOnlyDictionary<string, string> ConceptosEntrega = new Dictionary<string, string>
        {
            { "ADELANTO", "Adelanto para el viaje" },
            { "SEGUNDO_DESPACHO", "Segundo despacho (reprogramación)" },
            { "MENSUALIDAD", "Mensualidad" },
            { "PRESTAMO", "Préstamo" },
            { "OTROS", "Otros autorizados" }
        };

        public static readonly IReadOnlyDictionary<string, string> ConceptosPagoDirecto = new Dictionary<string, string>
        {
            { "REPARACION", "Reparación / parchado" },
            { "LLANTAS", "Llantas" },
            { "GRUA", "Grúa / auxilio" },
            { "COMBUSTIBLE", "Combustible" },
            { "PEAJE", "Peaje" },
            { "HOSPEDAJE", "Hospedaje" },
            { "OTROS", "Otros" }
        };

        public static readonly IReadOnlyDictionary<string, string> Medios = new Dictionary<string, string>
        {
            { "EFECTIVO", "Efectivo" },
            { "YAPE", "Yape / Plin" },
            { "TRANSFERENCIA", "Transferencia" },
            // No se registra quién llevó el sobre: lo que cuenta es el monto que el conductor
            // reconoce haber recibido al firmar su conformidad.
            { "SOBRE", "Sobre" },
            { "OTRO", "Otro" }
        };

        public static bool PuedeRegistrar() => SecurityHelper.TieneSesionActiva() && SecurityHelper.EsAdminOSupervisor();

        public static string NombreConcepto(string tipo, string concepto)
        {
            var lista = tipo == PagoDirecto ? ConceptosPagoDirecto : ConceptosEntrega;
            return concepto != null && lista.TryGetValue(concepto, out string n) ? n : concepto;
        }

        public static string NombreMedio(string medio) =>
            medio != null && Medios.TryGetValue(medio, out string n) ? n : medio;

        // =====================================================================
        // Reglas (sin BD)
        // =====================================================================

        /// <summary>Devuelve el error para mostrar o null si el movimiento es válido.</summary>
        public static string Validar(MovimientoViaje m, DateTime ahora)
        {
            if (m == null) return "No se recibieron los datos.";
            if (m.Tipo != Entrega && m.Tipo != PagoDirecto) return "Tipo de movimiento no válido.";
            if (m.IdConductor <= 0) return "Seleccione el conductor.";
            var conceptos = m.Tipo == PagoDirecto ? ConceptosPagoDirecto : ConceptosEntrega;
            if (string.IsNullOrEmpty(m.Concepto) || !conceptos.ContainsKey(m.Concepto)) return "Seleccione el concepto.";
            if (string.IsNullOrEmpty(m.Medio) || !Medios.ContainsKey(m.Medio)) return "Seleccione el medio.";
            if (m.MontoSoles < 0 || m.MontoDolares < 0) return "Los montos no pueden ser negativos.";
            if (m.MontoSoles == 0 && m.MontoDolares == 0) return "Indique el monto en soles o en dólares.";
            if (m.MontoSoles > LiquidacionCalculos.MontoMaximoAjuste || m.MontoDolares > LiquidacionCalculos.MontoMaximoAjuste)
                return "El monto es demasiado alto.";
            if (m.Fecha == default(DateTime)) return "Indique la fecha.";
            if (m.Fecha > ahora.AddHours(1)) return "La fecha no puede estar en el futuro.";
            if (m.Fecha < ahora.AddYears(-1)) return "La fecha es de hace más de un año.";

            if (m.Medio == "SOBRE" && m.Tipo == PagoDirecto) return "Un pago directo al proveedor no va en sobre.";
            if ((m.Medio == "YAPE" || m.Medio == "TRANSFERENCIA") && string.IsNullOrWhiteSpace(m.NumeroOperacion))
                return "Indique el número de operación del " + NombreMedio(m.Medio).ToLowerInvariant() + ".";

            if (m.Tipo == PagoDirecto)
            {
                if (string.IsNullOrWhiteSpace(m.NombreProveedor)) return "Indique a quién se le pagó (proveedor).";
                string ruc = (m.RucProveedor ?? "").Trim();
                if (ruc.Length > 0 && (ruc.Length != 11 || !ruc.All(char.IsDigit))) return "El RUC del proveedor debe tener 11 dígitos.";
            }
            if ((m.Descripcion ?? "").Length > 250) return "La descripción no puede superar 250 caracteres.";
            return null;
        }

        /// <summary>
        /// Suma las entregas por la fila de "Dinero entregado" que les corresponde (adelanto y segundo
        /// despacho van a Adelanto). El detalle de cada fila resume fechas y medios.
        /// </summary>
        public static IngresosEntregados SumarEntregas(IEnumerable<MovimientoViaje> entregas)
        {
            var r = new IngresosEntregados();
            var detalle = new Dictionary<string, List<string>>();
            foreach (var m in (entregas ?? Enumerable.Empty<MovimientoViaje>()).Where(e => e.Activo && e.Tipo == Entrega))
            {
                string fila;
                switch (m.Concepto)
                {
                    case "MENSUALIDAD": r.MensualidadSoles += m.MontoSoles; r.MensualidadDolares += m.MontoDolares; fila = "M"; break;
                    case "PRESTAMO": r.PrestamoSoles += m.MontoSoles; r.PrestamoDolares += m.MontoDolares; fila = "P"; break;
                    case "OTROS": r.OtrosSoles += m.MontoSoles; r.OtrosDolares += m.MontoDolares; fila = "O"; break;
                    default: r.DespachoSoles += m.MontoSoles; r.DespachoDolares += m.MontoDolares; fila = "D"; break;
                }
                if (!detalle.ContainsKey(fila)) detalle[fila] = new List<string>();
                detalle[fila].Add(m.Fecha.ToString("dd/MM") + " " + NombreMedio(m.Medio).ToLowerInvariant());
            }
            string Desc(string f) => detalle.ContainsKey(f) ? Recortar(string.Join("; ", detalle[f]), 250) : null;
            r.DescDespacho = Desc("D");
            r.DescMensualidad = Desc("M");
            r.DescPrestamo = Desc("P");
            r.DescOtros = Desc("O");
            return r;
        }

        private static string Recortar(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        // =====================================================================
        // BD
        // =====================================================================

        public sealed class ResultadoMovimiento
        {
            public bool Exito { get; set; }
            public string Mensaje { get; set; }
            public int IdMovimiento { get; set; }
        }

        public static ResultadoMovimiento Registrar(MovimientoViaje m, int idUsuario)
        {
            string error = Validar(m, FechaHelper.Ahora());
            if (error != null) return new ResultadoMovimiento { Exito = false, Mensaje = error };

            DataTable dt = DbHelper.ConsultarTablaSp("sp_LD_RegistrarMovimientoViaje",
                DbHelper.Param("@tipo", m.Tipo),
                DbHelper.Param("@idConductor", m.IdConductor),
                DbHelper.Param("@fecha", m.Fecha),
                DbHelper.Param("@concepto", m.Concepto),
                DbHelper.Param("@montoSoles", m.MontoSoles),
                DbHelper.Param("@montoDolares", m.MontoDolares),
                DbHelper.Param("@medio", m.Medio),
                DbHelper.Param("@numeroOperacion", m.NumeroOperacion),
                DbHelper.Param("@rucProveedor", m.Tipo == PagoDirecto ? m.RucProveedor : null),
                DbHelper.Param("@nombreProveedor", m.Tipo == PagoDirecto ? m.NombreProveedor : null),
                DbHelper.Param("@comprobante", m.Tipo == PagoDirecto ? m.Comprobante : null),
                DbHelper.Param("@descripcion", m.Descripcion),
                DbHelper.Param("@rutaAdjunto", m.RutaAdjunto),
                DbHelper.Param("@idUsuarioRegistro", idUsuario));
            DataRow r = dt.Rows[0];
            return new ResultadoMovimiento
            {
                Exito = Convert.ToInt32(r["Resultado"]) == 1,
                Mensaje = r["Mensaje"].ToString(),
                IdMovimiento = Convert.ToInt32(r["idMovimiento"])
            };
        }

        public static ResultadoMovimiento Anular(int idMovimiento, int idUsuario, string motivo)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_LD_AnularMovimientoViaje",
                DbHelper.Param("@idMovimiento", idMovimiento),
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@motivo", motivo));
            return new ResultadoMovimiento
            {
                Exito = Convert.ToInt32(dt.Rows[0]["Resultado"]) == 1,
                Mensaje = dt.Rows[0]["Mensaje"].ToString(),
                IdMovimiento = idMovimiento
            };
        }

        private const string SelectMovimientos = @"
            SELECT m.idMovimiento, m.tipo, m.idConductor,
                   LTRIM(RTRIM(c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, ''))) AS nombreConductor,
                   m.numeroOrdenViaje, m.fecha, m.concepto, m.montoSoles, m.montoDolares, m.medio,
                   m.numeroOperacion, m.rucProveedor, m.nombreProveedor, m.comprobante, m.descripcion,
                   m.rutaAdjunto, LTRIM(RTRIM(ISNULL(u.nombre, '') + ' ' + ISNULL(u.apellido, ''))) AS registradoPor,
                   m.activo, m.motivoAnulacion
            FROM MovimientoDineroViaje m
            INNER JOIN Conductor c ON c.idConductor = m.idConductor
            LEFT JOIN Usuarios u ON u.idUsuario = m.idUsuarioRegistro";

        /// <summary>Movimientos de un conductor: los aún no liquidados y (opcional) los anulados recientes.</summary>
        public static List<MovimientoViaje> ObtenerSinLiquidar(int idConductor, bool incluirAnulados = false) =>
            Leer(DbHelper.ConsultarTabla(SelectMovimientos + @"
                WHERE m.idConductor = @id AND m.numeroOrdenViaje IS NULL
                  AND (m.activo = 1 OR (@anulados = 1 AND m.fechaAnulacion >= DATEADD(DAY, -30, dbo.fn_AhoraPeru())))
                ORDER BY m.fecha, m.idMovimiento",
                DbHelper.Param("@id", idConductor), DbHelper.Param("@anulados", incluirAnulados)));

        /// <summary>Consulta (con parámetro @n = número de orden) de los movimientos activos de una liquidación; el detalle la agrega a su lote.</summary>
        internal const string ConsultaDeOrden = SelectMovimientos + " WHERE m.numeroOrdenViaje = @n AND m.activo = 1 ORDER BY m.fecha, m.idMovimiento";

        /// <summary>Movimientos activos unidos a una liquidación.</summary>
        public static List<MovimientoViaje> ObtenerDeOrden(string numeroOrdenViaje) =>
            Leer(DbHelper.ConsultarTabla(SelectMovimientos + @"
                WHERE m.numeroOrdenViaje = @n AND m.activo = 1
                ORDER BY m.fecha, m.idMovimiento",
                DbHelper.Param("@n", numeroOrdenViaje)));

        /// <summary>
        /// Para revisar una liquidación: lo ya unido a ella más lo que el conductor tiene sin liquidar
        /// (registrado hasta que envió su liquidación o después, mientras no esté en otra).
        /// </summary>
        public static List<MovimientoViaje> ObtenerParaRevision(int idOrdenViaje) =>
            Leer(DbHelper.ConsultarTabla(SelectMovimientos + @"
                INNER JOIN OrdenViaje ov ON ov.idOrdenViaje = @id AND ov.idConductor = m.idConductor
                WHERE m.activo = 1
                  AND (m.numeroOrdenViaje = ov.numeroOrdenViaje OR m.numeroOrdenViaje IS NULL)
                ORDER BY m.fecha, m.idMovimiento",
                DbHelper.Param("@id", idOrdenViaje)));

        /// <summary>
        /// Une a la liquidación exactamente los movimientos elegidos en la revisión (dentro de la
        /// transacción de EnviarAConformidad): suelta los que estaban unidos y ya no se eligieron y une
        /// los elegidos que son del mismo conductor, están activos y no están en otra liquidación.
        /// </summary>
        internal static void UnirAOrden(SqlConnection conn, SqlTransaction tran, string numeroOrdenViaje, IEnumerable<int> ids)
        {
            string lista = string.Join(",", (ids ?? Enumerable.Empty<int>()).Where(i => i > 0).Distinct());
            DbHelper.EjecutarNonQuery(conn, tran, @"
                DECLARE @elegidos TABLE (id INT PRIMARY KEY);
                INSERT INTO @elegidos (id) SELECT DISTINCT TRY_CAST(value AS INT) FROM STRING_SPLIT(@ids, ',') WHERE TRY_CAST(value AS INT) IS NOT NULL;

                UPDATE MovimientoDineroViaje SET numeroOrdenViaje = NULL
                WHERE numeroOrdenViaje = @n AND idMovimiento NOT IN (SELECT id FROM @elegidos);

                UPDATE m SET numeroOrdenViaje = @n
                FROM MovimientoDineroViaje m
                INNER JOIN OrdenViaje ov ON ov.numeroOrdenViaje = @n AND ov.idConductor = m.idConductor
                WHERE m.idMovimiento IN (SELECT id FROM @elegidos) AND m.activo = 1
                  AND (m.numeroOrdenViaje IS NULL OR m.numeroOrdenViaje = @n);",
                DbHelper.Param("@n", numeroOrdenViaje),
                DbHelper.Param("@ids", lista));
        }

        internal static List<MovimientoViaje> Leer(DataTable dt)
        {
            var lista = new List<MovimientoViaje>();
            foreach (DataRow r in dt.Rows)
                lista.Add(Leer(r));
            return lista;
        }

        internal static MovimientoViaje Leer(DataRow r) => new MovimientoViaje
        {
            IdMovimiento = Convert.ToInt32(r["idMovimiento"]),
            Tipo = r["tipo"].ToString(),
            IdConductor = Convert.ToInt32(r["idConductor"]),
            NombreConductor = Normalizar(r["nombreConductor"] as string),
            NumeroOrdenViaje = r["numeroOrdenViaje"] as string,
            Fecha = Convert.ToDateTime(r["fecha"]),
            Concepto = r["concepto"].ToString(),
            MontoSoles = Convert.ToDecimal(r["montoSoles"]),
            MontoDolares = Convert.ToDecimal(r["montoDolares"]),
            Medio = r["medio"].ToString(),
            NumeroOperacion = r["numeroOperacion"] as string,
            RucProveedor = r["rucProveedor"] as string,
            NombreProveedor = r["nombreProveedor"] as string,
            Comprobante = r["comprobante"] as string,
            Descripcion = r["descripcion"] as string,
            RutaAdjunto = r["rutaAdjunto"] as string,
            RegistradoPor = (r["registradoPor"] as string)?.Trim(),
            Activo = Convert.ToBoolean(r["activo"]),
            MotivoAnulacion = r["motivoAnulacion"] as string
        };

        private static string Normalizar(string s) =>
            string.IsNullOrWhiteSpace(s) ? null : System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
    }
}
