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
    /// transacción (<see cref="LoteSql"/>): abrir, iniciar transacción, el lote y confirmar.
    ///
    /// El servicio no depende de <c>System.Web</c>: recibe un <see cref="LiquidacionConductorInput"/>
    /// con todos los valores ya parseados/validados y lanza la excepción para que el
    /// code-behind muestre el mensaje (el rollback ya se ejecutó).
    /// </summary>
    public static class LiquidacionConductorService
    {
        /// <summary>
        /// Ejecuta la transacción completa (orden + financieros + cierre de viajes) y
        /// devuelve el <c>idOrdenViaje</c> guardado. Si algo falla hace rollback y
        /// re-propaga la excepción original.
        /// </summary>
        public static int EnviarLiquidacion(LiquidacionConductorInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

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

        // ------------------------------------------------------------------
        // Armado del lote, en el mismo orden en que antes se ejecutaban los SP
        // ------------------------------------------------------------------

        private static LoteSql ConstruirLote(LiquidacionConductorInput input)
        {
            var lote = new LoteSql();

            if (input.EsReliquidacion)
            {
                lote.Sp("sp_DC_EliminarDatosFinancierosOrden", "@numeroOrdenViaje", input.NumeroOrdenViaje);
                AgregarActualizarOrdenViajeConductor(lote, input);
                // Es el primer (y único) conjunto de resultados de la orden: el id.
                lote.Crudo("SELECT idOrdenViaje FROM OrdenViaje WHERE numeroOrdenViaje = <<0>>", input.NumeroOrdenViaje);
            }
            else
            {
                AgregarInsertarOrdenViajeConductor(lote, input);
            }

            // La llegada que vio el GPS queda también como "verificada por GPS" para la revisión.
            if (!string.IsNullOrEmpty(input.HoraLlegadaGps))
                lote.Crudo("UPDATE OrdenViaje SET horaLlegadaGps = <<0>> WHERE numeroOrdenViaje = <<1>>",
                    input.HoraLlegadaGps, input.NumeroOrdenViaje);

            AgregarIngresosPrincipales(lote, input);
            AgregarGastosPrincipales(lote, input);
            AgregarIngresosAdicionales(lote, input);
            AgregarGastosAdicionales(lote, input);
            AgregarDescuentosReintegros(lote, input);

            if (input.GastosFinancieros != null && input.GastosFinancieros.Count > 0)
                AgregarGastosFinancierosDetallados(lote, input.NumeroOrdenViaje, input.GastosFinancieros);

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

        private static void AgregarInsertarOrdenViajeConductor(LoteSql lote, LiquidacionConductorInput input)
        {
            // Devuelve el id de la orden como su primer conjunto de resultados.
            lote.Sp("sp_DC_InsertarOrdenViajeConductor",
                "@numeroOrdenViaje", input.NumeroOrdenViaje,
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

        private static void AgregarIngresosPrincipales(LoteSql lote, LiquidacionConductorInput input)
        {
            if (input.DespachoSoles > 0 || input.DespachoDolares > 0 || input.PrestamoSoles > 0 || input.PrestamoDolares > 0 ||
                input.MensualidadSoles > 0 || input.MensualidadDolares > 0 || input.OtrosSoles > 0 || input.OtrosDolares > 0)
            {
                lote.Sp("sp_DC_InsertarIngresos",
                    "@numeroOrdenViaje", input.NumeroOrdenViaje,
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

        private static void AgregarGastosPrincipales(LoteSql lote, LiquidacionConductorInput input)
        {
            lote.Sp("sp_DC_InsertarEgresos",
                "@numeroOrdenViaje", input.NumeroOrdenViaje,
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

        private static void AgregarIngresosAdicionales(LoteSql lote, LiquidacionConductorInput input)
        {
            if (input.IngresosAdicionales == null) return;

            foreach (var ingreso in input.IngresosAdicionales)
            {
                lote.Sp("sp_DC_InsertarIngresoAdicional",
                    "@numeroOrdenViaje", input.NumeroOrdenViaje,
                    "@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "",
                    "@soles", ValidarMonto(ingreso.Soles, "soles de ingreso adicional"),
                    "@dolares", ValidarMonto(ingreso.Dolares, "dólares de ingreso adicional"),
                    "@descripcion", ingreso.Descripcion ?? "");
            }
        }

        private static void AgregarGastosAdicionales(LoteSql lote, LiquidacionConductorInput input)
        {
            if (input.GastosAdicionales == null) return;

            foreach (var gasto in input.GastosAdicionales)
            {
                lote.Sp("sp_DC_InsertarGastoAdicional",
                    "@numeroOrdenViaje", input.NumeroOrdenViaje,
                    "@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "",
                    "@soles", ValidarMonto(gasto.Soles, "soles de gasto adicional"),
                    "@dolares", ValidarMonto(gasto.Dolares, "dólares de gasto adicional"),
                    "@descripcion", gasto.Descripcion ?? "");
            }
        }

        private static void AgregarDescuentosReintegros(LoteSql lote, LiquidacionConductorInput input)
        {
            if (input.DescuentoSoles > 0 || input.DescuentoDolares > 0 || input.ReintegroSoles > 0 || input.ReintegroDolares > 0)
            {
                lote.Sp("sp_DC_InsertarDescuentosReintegros",
                    "@numeroOrdenViaje", input.NumeroOrdenViaje,
                    "@descuentoSoles", input.DescuentoSoles,
                    "@descuentoDolares", input.DescuentoDolares,
                    "@reintegroSoles", input.ReintegroSoles,
                    "@reintegroDolares", input.ReintegroDolares);
            }
        }

        private static void AgregarGastosFinancierosDetallados(LoteSql lote, string numeroOrdenViaje, List<GastoFinanciero> gastos)
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
        private static void AgregarDetalleComprobante(LoteSql lote, string sp, string numeroOrdenViaje, GastoFinanciero gasto, string nombre)
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

            public sealed class Resultado
            {
                public int IdOrden;
                public int FilasViajesCerrados;
            }

            private sealed class Instruccion
            {
                public string Plantilla;
                public object[] Valores;
            }

            private readonly List<Instruccion> _instrucciones = new List<Instruccion>();

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
                        int p = 0;
                        foreach (Instruccion ins in envio)
                        {
                            string texto = ins.Plantilla;
                            for (int i = 0; i < ins.Valores.Length; i++)
                            {
                                string nombre = "@p" + (p++);
                                cmd.Parameters.AddWithValue(nombre, ins.Valores[i] ?? DBNull.Value);
                                texto = texto.Replace("<<" + i + ">>", nombre);
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
                }
                if (actual.Count > 0) yield return actual;
            }
        }
    }
}
