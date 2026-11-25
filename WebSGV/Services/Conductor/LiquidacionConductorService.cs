using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Transacción de envío de liquidación del conductor (antes en
    /// <c>DashboardConductor.aspx.cs &gt; btnEnviarLiquidacion_Click</c> y sus helpers
    /// <c>Insertar*</c>/<c>Cerrar*</c>). El code-behind conserva la sesión, validación,
    /// el parseo de <c>Request.Form</c>/hidden fields, la auditoría, la notificación y el
    /// redirect a la firma; este servicio sólo orquesta el SQL/SPs dentro de una única
    /// transacción. Los stored procedures son los mismos de siempre.
    ///
    /// Rendimiento: cada SP era un viaje a la BD (con la BD remota, ~100 ms cada uno desde la
    /// oficina; una liquidación con adicionales y peajes sumaba una decena). Ahora todas las
    /// llamadas se arman primero y viajan juntas en un solo lote dentro de la misma
    /// transacción (<see cref="LoteSql"/>): abrir, iniciar transacción, el lote y confirmar. En una
    /// orden nueva el lote va en dos envíos: bloqueo de viajes + número de orden, y el resto con ese número.
    ///
    /// El servicio no depende de <c>System.Web</c>: recibe un <see cref="LiquidacionConductorInput"/>
    /// con todos los valores ya parseados/validados y lanza la excepción para que el
    /// code-behind muestre el mensaje (el rollback ya se ejecutó).
    /// </summary>
    public static class LiquidacionConductorService
    {
        /// <summary>
        /// Ejecuta la transacción completa (bloqueo de viajes + número + orden + financieros +
        /// cierre de viajes) y devuelve el <c>idOrdenViaje</c> guardado. En una orden nueva sin
        /// <see cref="LiquidacionConductorInput.NumeroOrdenViaje"/> el número se genera dentro de la
        /// transacción y queda en ese mismo campo al confirmar. Si algo falla hace rollback y
        /// re-propaga la excepción original.
        /// </summary>
        public static int EnviarLiquidacion(LiquidacionConductorInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidarConductorYViajes(input);

            // Se arma todo antes de tocar la BD: una validación de montos que falle no abre
            // conexión ni transacción.
            LoteSql lote = ConstruirLote(input);
            int idOrdenGuardada;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        LoteSql.Resultado resultado = lote.Ejecutar(conn, transaction);

                        if (!input.EsReliquidacion && resultado.IdOrden <= 0)
                            throw new Exception("No se pudo insertar la orden de viaje");
                        idOrdenGuardada = resultado.IdOrden;

                        if (resultado.FilasViajesCerrados == 0)
                            throw new ErrorNegocioException($"No se pudo cerrar ninguno de los viajes: {string.Join(", ", input.IdsViajesActivos)}");

                        transaction.Commit();

                        // El número generado dentro de la transacción vuelve al llamador (auditoría, mensajes).
                        if (resultado.NumeroOrden != null)
                            input.NumeroOrdenViaje = resultado.NumeroOrden;
                    }
                    catch
                    {
                        // No enmascarar la excepción original si el rollback también falla.
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            return idOrdenGuardada;
        }

        /// <summary>
        /// La orden siempre queda a nombre del conductor de la sesión y su viaje principal debe ser
        /// uno de los viajes activos que se van a cerrar (que sp_DC_CerrarViajesProgreso valida
        /// contra ese mismo conductor). Antes el viaje principal no se validaba y el conductor de la
        /// orden salía de un despacho cualquiera de ese viaje: se podía liquidar a nombre de otro.
        /// </summary>
        internal static void ValidarConductorYViajes(LiquidacionConductorInput input)
        {
            if (input.IdConductorActual <= 0 || input.IdConductor != input.IdConductorActual)
                throw new ErrorNegocioException("La liquidación no corresponde al conductor de la sesión.");

            if (input.IdsViajesActivos == null || input.IdsViajesActivos.Count == 0 ||
                !input.IdsViajesActivos.Contains(input.IdViajeProgreso))
                throw new ErrorNegocioException("El viaje que se liquida no está entre sus viajes activos. Recargue la página.");
        }

        // ------------------------------------------------------------------
        // Armado del lote, en el mismo orden en que antes se ejecutaban los SP
        // ------------------------------------------------------------------

        /// <summary>Texto de cada instrucción del lote, en orden (para las pruebas).</summary>
        internal static IReadOnlyList<string> InstruccionesDelLote(LiquidacionConductorInput input) =>
            ConstruirLote(input).Plantillas();

        /// <summary>
        /// Orden nueva sin número: se genera dentro de la transacción (ver <see cref="ConstruirLote"/>).
        /// La página ya no lo pide antes; las pruebas pueden traer uno propio.
        /// </summary>
        private static bool GeneraNumero(LiquidacionConductorInput input) =>
            !input.EsReliquidacion && string.IsNullOrWhiteSpace(input.NumeroOrdenViaje);

        private static LoteSql ConstruirLote(LiquidacionConductorInput input)
        {
            // Un texto más largo que su columna se cortaba sin avisar (y el PDF firmado quedaba incompleto).
            ReglasLiquidacionConductor.ValidarTextos(input);

            var lote = new LoteSql();

            // 1. Antes que nada, los viajes quedan bloqueados hasta el commit y se comprueba que siguen
            //    ABIERTOS, del conductor y con despachos activos (50012 si no). "Anular lote"
            //    (sp_LD_AnularLote) toma el mismo bloqueo: ya no puede anular despachos de un viaje
            //    mientras su liquidación se está guardando. Vale para la orden nueva y para la
            //    reliquidación de una rechazada (sus viajes volvieron a ABIERTO y se cierran otra vez).
            lote.Sp("sp_DC_BloquearViajesParaLiquidar",
                "@idsViajes", string.Join(",", input.IdsViajesActivos),
                "@idConductor", input.IdConductorActual);

            // 2. Orden nueva: el número se genera aquí, dentro de la misma transacción. El TABLOCKX de
            //    sp_DC_GenerarNumeroOrden sobre OrdenViaje dura hasta el commit, así que dos envíos a la
            //    vez ya no reciben el mismo OV (antes se generaba antes, en otra conexión, y el segundo
            //    INSERT chocaba con la UQ). Las liquidaciones quedan en fila, una tras otra.
            object numero;
            if (GeneraNumero(input))
            {
                lote.GenerarNumeroOrden();
                numero = LoteSql.NumeroGenerado;
            }
            else
            {
                numero = input.NumeroOrdenViaje;
            }

            if (input.EsReliquidacion)
            {
                lote.Sp("sp_DC_EliminarDatosFinancierosOrden", "@numeroOrdenViaje", input.NumeroOrdenViaje);
                AgregarActualizarOrdenViajeConductor(lote, input);
                // Es el primer (y único) conjunto de resultados de la orden: el id.
                lote.Crudo("SELECT idOrdenViaje FROM OrdenViaje WHERE numeroOrdenViaje = <<0>>", input.NumeroOrdenViaje);
            }
            else
            {
                AgregarInsertarOrdenViajeConductor(lote, input, numero);
            }

            // La llegada que vio el GPS queda también como "verificada por GPS" para la revisión.
            if (!string.IsNullOrEmpty(input.HoraLlegadaGps))
                lote.Crudo("UPDATE OrdenViaje SET horaLlegadaGps = <<0>> WHERE numeroOrdenViaje = <<1>>",
                    input.HoraLlegadaGps, numero);

            AgregarIngresosPrincipales(lote, input, numero);
            AgregarGastosPrincipales(lote, input, numero);
            AgregarIngresosAdicionales(lote, input, numero);
            AgregarGastosAdicionales(lote, input, numero);
            AgregarDescuentosReintegros(lote, input, numero);

            if (input.GastosFinancieros != null && input.GastosFinancieros.Count > 0)
                AgregarGastosFinancierosDetallados(lote, numero, input.GastosFinancieros);

            AgregarCerrarViajesProgreso(lote, input.IdsViajesActivos, input.IdConductorActual);
            return lote;
        }

        private static object Nulo(string valor) => string.IsNullOrEmpty(valor) ? (object)DBNull.Value : valor;

        private static void AgregarActualizarOrdenViajeConductor(LoteSql lote, LiquidacionConductorInput input)
        {
            lote.Sp("sp_DC_ActualizarOrdenViajeConductor",
                "@numeroOrdenViaje", input.NumeroOrdenViaje,
                "@fechaSalida", FechaSeguraSQL(input.FechaSalida),
                "@horaSalida", Nulo(input.HoraSalida),
                "@fechaLlegada", FechaSeguraSQL(input.FechaLlegada),
                "@horaLlegada", Nulo(input.HoraLlegada),
                "@horaLlegadaDeclarada", Nulo(input.HoraLlegadaDeclarada),
                "@observaciones", Nulo(input.Observaciones),
                "@idUsuarioRegistro", input.IdUsuarioRegistro > 0 ? (object)input.IdUsuarioRegistro : DBNull.Value);
        }

        private static void AgregarInsertarOrdenViajeConductor(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            // Devuelve el id de la orden como su primer conjunto de resultados.
            lote.Sp("sp_DC_InsertarOrdenViajeConductor",
                "@numeroOrdenViaje", numero,
                "@fechaSalida", FechaSeguraSQL(input.FechaSalida),
                "@horaSalida", Nulo(input.HoraSalida),
                "@fechaLlegada", FechaSeguraSQL(input.FechaLlegada),
                "@horaLlegada", Nulo(input.HoraLlegada),
                "@horaLlegadaDeclarada", Nulo(input.HoraLlegadaDeclarada),
                "@idConductor", input.IdConductor,
                "@idTracto", input.IdTracto,
                "@idCarreta", input.IdCarreta,
                "@observaciones", Nulo(input.Observaciones),
                "@idViajeProgreso", input.IdViajeProgreso,
                "@idUsuarioRegistro", input.IdUsuarioRegistro > 0 ? (object)input.IdUsuarioRegistro : DBNull.Value);
        }

        private static void AgregarIngresosPrincipales(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            if (input.DespachoSoles > 0 || input.DespachoDolares > 0 || input.PrestamoSoles > 0 || input.PrestamoDolares > 0 ||
                input.MensualidadSoles > 0 || input.MensualidadDolares > 0 || input.OtrosSoles > 0 || input.OtrosDolares > 0)
            {
                lote.Sp("sp_DC_InsertarIngresos",
                    "@numeroOrdenViaje", numero,
                    "@despachoSoles", input.DespachoSoles,
                    "@despachoDolares", input.DespachoDolares,
                    "@prestamoSoles", input.PrestamoSoles,
                    "@prestamoDolares", input.PrestamoDolares,
                    "@mensualidadSoles", input.MensualidadSoles,
                    "@mensualidadDolares", input.MensualidadDolares,
                    "@otrosSoles", input.OtrosSoles,
                    "@otrosDolares", input.OtrosDolares,
                    "@descDespacho", Nulo(input.DescDespacho),
                    "@descMensualidad", Nulo(input.DescMensualidad),
                    "@descOtros", Nulo(input.DescOtros),
                    "@descPrestamo", Nulo(input.DescPrestamo));
            }
        }

        private static void AgregarGastosPrincipales(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            lote.Sp("sp_DC_InsertarEgresos",
                "@numeroOrdenViaje", numero,
                "@peajesSoles", input.PeajesSoles,
                "@peajesDolares", input.PeajesDolares,
                "@descPeajes", Nulo(input.DescPeajes),
                "@alimentacionSoles", input.AlimentacionSoles,
                "@alimentacionDolares", input.AlimentacionDolares,
                "@descAlimentacion", Nulo(input.DescAlimentacion),
                "@apoyoSeguridadSoles", input.ApoyoSeguridadSoles,
                "@apoyoSeguridadDolares", input.ApoyoSeguridadDolares,
                "@descApoyoSeguridad", Nulo(input.DescApoyoSeguridad),
                "@reparacionesSoles", input.ReparacionesSoles,
                "@reparacionesDolares", input.ReparacionesDolares,
                "@descReparaciones", Nulo(input.DescReparaciones),
                "@movilidadSoles", input.MovilidadSoles,
                "@movilidadDolares", input.MovilidadDolares,
                "@descMovilidad", Nulo(input.DescMovilidad),
                "@encapadaSoles", input.EncapadaSoles,
                "@encapadaDolares", input.EncapadaDolares,
                "@descEncapada", Nulo(input.DescEncapada),
                "@hospedajeSoles", input.HospedajeSoles,
                "@hospedajeDolares", input.HospedajeDolares,
                "@descHospedaje", Nulo(input.DescHospedaje),
                "@combustibleSoles", input.CombustibleSoles,
                "@combustibleDolares", input.CombustibleDolares,
                "@descCombustible", Nulo(input.DescCombustible));
        }

        private static void AgregarIngresosAdicionales(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            if (input.IngresosAdicionales == null) return;

            foreach (var ingreso in input.IngresosAdicionales)
            {
                lote.Sp("sp_DC_InsertarIngresoAdicional",
                    "@numeroOrdenViaje", numero,
                    "@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "",
                    "@soles", ValidarMonto(ingreso.Soles, "soles de ingreso adicional"),
                    "@dolares", ValidarMonto(ingreso.Dolares, "dólares de ingreso adicional"),
                    "@descripcion", ingreso.Descripcion ?? "");
            }
        }

        private static void AgregarGastosAdicionales(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            if (input.GastosAdicionales == null) return;

            foreach (var gasto in input.GastosAdicionales)
            {
                lote.Sp("sp_DC_InsertarGastoAdicional",
                    "@numeroOrdenViaje", numero,
                    "@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "",
                    "@soles", ValidarMonto(gasto.Soles, "soles de gasto adicional"),
                    "@dolares", ValidarMonto(gasto.Dolares, "dólares de gasto adicional"),
                    "@descripcion", gasto.Descripcion ?? "");
            }
        }

        private static void AgregarDescuentosReintegros(LoteSql lote, LiquidacionConductorInput input, object numero)
        {
            if (input.DescuentoSoles > 0 || input.DescuentoDolares > 0 || input.ReintegroSoles > 0 || input.ReintegroDolares > 0)
            {
                lote.Sp("sp_DC_InsertarDescuentosReintegros",
                    "@numeroOrdenViaje", numero,
                    "@descuentoSoles", input.DescuentoSoles,
                    "@descuentoDolares", input.DescuentoDolares,
                    "@reintegroSoles", input.ReintegroSoles,
                    "@reintegroDolares", input.ReintegroDolares,
                    // Motivos de la administración: al reenviar una orden rechazada se conservan.
                    "@observacionesDescuento", Nulo(input.MotivoDescuento),
                    "@observacionesReintegro", Nulo(input.MotivoReintegro));
            }
        }

        private static void AgregarGastosFinancierosDetallados(LoteSql lote, object numeroOrdenViaje, List<GastoFinanciero> gastos)
        {
            foreach (var gasto in gastos)
            {
                string categoria = gasto.Categoria?.ToLower() ?? "";

                switch (categoria)
                {
                    case "peajes":
                        lote.Sp("sp_DC_InsertarDetallePeaje",
                            "@numeroOrdenViaje", numeroOrdenViaje,
                            "@estacion", gasto.Estacion ?? gasto.Lugar ?? "",
                            "@fecha", FechaSeguraSQL(gasto.Fecha),
                            "@numeroComprobante", gasto.Comprobante ?? "",
                            "@montoSoles", ValidarMonto(gasto.Soles, "soles de peaje"),
                            "@montoDolares", ValidarMonto(gasto.Dolares, "dólares de peaje"),
                            "@observaciones", gasto.Observaciones ?? "");
                        break;
                    case "reparaciones":
                        AgregarDetalleComprobante(lote, "sp_DC_InsertarDetalleReparacion", numeroOrdenViaje, gasto, "reparación");
                        break;
                    case "hospedaje":
                        AgregarDetalleComprobante(lote, "sp_DC_InsertarDetalleHospedaje", numeroOrdenViaje, gasto, "hospedaje");
                        break;
                    case "combustible":
                        AgregarDetalleComprobante(lote, "sp_DC_InsertarDetalleCombustible", numeroOrdenViaje, gasto, "combustible");
                        break;
                }
            }
        }

        /// <summary>Reparaciones, hospedaje y combustible comparten los mismos parámetros.</summary>
        private static void AgregarDetalleComprobante(LoteSql lote, string sp, object numeroOrdenViaje, GastoFinanciero gasto, string nombre)
        {
            lote.Sp(sp,
                "@numeroOrdenViaje", numeroOrdenViaje,
                "@fechaComprobante", FechaSeguraSQL(gasto.Fecha),
                "@numeroComprobante", gasto.Comprobante ?? "",
                "@montoSoles", ValidarMonto(gasto.Soles, "soles de " + nombre),
                "@montoDolares", ValidarMonto(gasto.Dolares, "dólares de " + nombre),
                "@observaciones", gasto.Observaciones ?? "");
        }

        private static void AgregarCerrarViajesProgreso(LoteSql lote, List<int> idsViajes, int idConductor)
        {
            // IDOR guard: el SP valida que los viajes pertenecen a este conductor. Va siempre al final
            // (tiene transacción propia). Sus salidas se devuelven como el último conjunto de resultados.
            lote.Crudo(
                "DECLARE @filasViajes INT, @filasDespachos INT; " +
                "EXEC sp_DC_CerrarViajesProgreso @idsViajes = <<0>>, @idConductor = <<1>>, " +
                "@filasViajesCerrados = @filasViajes OUTPUT, @filasDespachosCerrados = @filasDespachos OUTPUT; " +
                "SELECT @filasViajes AS " + LoteSql.ColumnaFilasViajesCerrados,
                string.Join(",", idsViajes), idConductor);
        }

        // ------------------------------------------------------------------
        // Utilidades locales (sin System.Web)
        // ------------------------------------------------------------------

        private static object FechaSeguraSQL(DateTime fecha) =>
            OrdenViajeValidaciones.EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;

        private static decimal ValidarMonto(decimal? monto, string campo) => MontoHelper.ValidarMonto(monto, campo);
        private static decimal ValidarMonto(decimal monto, string campo) => MontoHelper.ValidarMonto(monto, campo);

        /// <summary>
        /// Instrucciones SQL (EXEC de SP y alguna consulta) que se mandan a la BD juntas, en el
        /// orden en que se agregaron y dentro de la transacción que recibe <see cref="Ejecutar"/>.
        /// Cada valor viaja como parámetro (nunca concatenado). Se parte en varios envíos solo si
        /// hubiera tantos renglones que se acercara al límite de parámetros de SQL Server (2100).
        /// </summary>
        private sealed class LoteSql
        {
            public const string ColumnaFilasViajesCerrados = "__filasViajesCerrados";
            private const int MaxParametrosPorEnvio = 1800;

            private const string ParametroNumeroGenerado = "@numeroOrdenGenerado";

            /// <summary>
            /// Valor que ocupa el lugar del número de orden hasta que <see cref="GenerarNumeroOrden"/>
            /// lo genera; <see cref="Ejecutar"/> lo cambia por el número real.
            /// </summary>
            public static readonly object NumeroGenerado = new object();

            public sealed class Resultado
            {
                public int IdOrden;
                public int FilasViajesCerrados;
                /// <summary>El número generado por <see cref="GenerarNumeroOrden"/>, o null.</summary>
                public string NumeroOrden;
            }

            private sealed class Instruccion
            {
                public string Plantilla;
                public object[] Valores;
                public bool GeneraNumero;
            }

            private readonly List<Instruccion> _instrucciones = new List<Instruccion>();

            public IReadOnlyList<string> Plantillas() => _instrucciones.Select(i => i.Plantilla).ToList();

            /// <summary>
            /// sp_DC_GenerarNumeroOrden dentro de la transacción. El envío termina en esta instrucción
            /// para leer el número (parámetro de salida) antes de mandar las que lo usan.
            /// </summary>
            public void GenerarNumeroOrden() =>
                _instrucciones.Add(new Instruccion
                {
                    Plantilla = "EXEC sp_DC_GenerarNumeroOrden @numeroGenerado = " + ParametroNumeroGenerado + " OUTPUT",
                    Valores = new object[0],
                    GeneraNumero = true
                });

            /// <summary>EXEC de un stored procedure con pares nombre/valor (<c>"@x", valor, "@y", valor…</c>).</summary>
            public void Sp(string procedimiento, params object[] pares)
            {
                int n = pares.Length / 2;
                var args = new List<string>(n);
                var valores = new object[n];
                for (int i = 0; i < n; i++)
                {
                    args.Add(pares[2 * i] + " = <<" + i + ">>");
                    valores[i] = pares[2 * i + 1];
                }
                _instrucciones.Add(new Instruccion { Plantilla = "EXEC " + procedimiento + " " + string.Join(", ", args), Valores = valores });
            }

            /// <summary>SQL propio; los valores se citan en el texto como <c>&lt;&lt;0&gt;&gt;</c>, <c>&lt;&lt;1&gt;&gt;</c>…</summary>
            public void Crudo(string plantilla, params object[] valores) =>
                _instrucciones.Add(new Instruccion { Plantilla = plantilla, Valores = valores });

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
                Justification = "El lote une plantillas fijas del servicio (nombres de SP y SQL literal de Sp/Crudo); cada valor se reemplaza por un parámetro @pN.")]
            public Resultado Ejecutar(SqlConnection conn, SqlTransaction transaction)
            {
                var resultado = new Resultado();
                bool idLeido = false;

                foreach (List<Instruccion> envio in Partir())
                {
                    var sql = new StringBuilder();
                    // Ante cualquier error en tiempo de ejecución el lote se detiene y se deshace todo,
                    // como cuando cada SP se ejecutaba por separado y el primer error cortaba el flujo.
                    sql.Append("SET XACT_ABORT ON;\n");

                    using (SqlCommand cmd = new SqlCommand { Connection = conn, Transaction = transaction })
                    {
                        SqlParameter pNumero = null;
                        int p = 0;
                        foreach (Instruccion ins in envio)
                        {
                            string texto = ins.Plantilla;
                            for (int i = 0; i < ins.Valores.Length; i++)
                            {
                                string nombre = "@p" + (p++);
                                object valor = ins.Valores[i];
                                if (ReferenceEquals(valor, NumeroGenerado))
                                {
                                    if (string.IsNullOrEmpty(resultado.NumeroOrden))
                                        throw new InvalidOperationException("El número de orden se usa antes de generarse.");
                                    valor = resultado.NumeroOrden;
                                }
                                cmd.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
                                texto = texto.Replace("<<" + i + ">>", nombre);
                            }
                            if (ins.GeneraNumero)
                            {
                                pNumero = new SqlParameter(ParametroNumeroGenerado, SqlDbType.VarChar, 50) { Direction = ParameterDirection.Output };
                                cmd.Parameters.Add(pNumero);
                            }
                            sql.Append(texto).Append(";\n");
                        }
                        cmd.CommandText = sql.ToString();

                        using (SqlDataReader rd = cmd.ExecuteReader())
                        {
                            do
                            {
                                bool esFilas = rd.FieldCount == 1 && rd.GetName(0) == ColumnaFilasViajesCerrados;
                                if (esFilas)
                                {
                                    if (rd.Read() && !rd.IsDBNull(0)) resultado.FilasViajesCerrados = Convert.ToInt32(rd[0]);
                                }
                                else if (!idLeido && rd.Read())
                                {
                                    // Primer conjunto de resultados del lote: el id de la orden
                                    // (lo mismo que leía el ExecuteScalar de antes).
                                    if (!rd.IsDBNull(0)) resultado.IdOrden = Convert.ToInt32(rd[0]);
                                    idLeido = true;
                                }
                            } while (rd.NextResult());
                        }

                        // El parámetro de salida se lee con el lector ya cerrado.
                        if (pNumero != null)
                        {
                            string numero = pNumero.Value == DBNull.Value ? null : Convert.ToString(pNumero.Value);
                            if (string.IsNullOrEmpty(numero))
                                throw new ErrorNegocioException("No se pudo generar el número de la orden. Inténtalo de nuevo en unos segundos.");
                            resultado.NumeroOrden = numero;
                        }
                    }
                }

                return resultado;
            }

            private IEnumerable<List<Instruccion>> Partir()
            {
                var actual = new List<Instruccion>();
                int parametros = 0;
                foreach (Instruccion ins in _instrucciones)
                {
                    if (actual.Count > 0 && parametros + ins.Valores.Length > MaxParametrosPorEnvio)
                    {
                        yield return actual;
                        actual = new List<Instruccion>();
                        parametros = 0;
                    }
                    actual.Add(ins);
                    parametros += ins.Valores.Length;

                    // Las instrucciones siguientes llevan el número generado: van en otro envío.
                    if (ins.GeneraNumero)
                    {
                        yield return actual;
                        actual = new List<Instruccion>();
                        parametros = 0;
                    }
                }
                if (actual.Count > 0) yield return actual;
            }
        }
    }
}
