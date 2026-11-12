using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Common;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    /// <summary>
    /// Detalle de una liquidación para el conductor ("Ver" en su historial).
    /// Lee el mismo <see cref="DetalleLiquidacion"/> y usa los mismos cálculos
    /// (<see cref="LiquidacionCalculos"/>) que el PDF y la pantalla de conformidad, para que el
    /// saldo que ve aquí sea siempre el del documento firmado: entregado − gastos + descuentos −
    /// reintegros, con el motivo de cada ajuste.
    /// </summary>
    public partial class DetalleOrdenViaje : PaginaBase
    {
        private static readonly CultureInfo CulturaPe = CultureInfo.GetCultureInfo("es-PE");

        /// <summary>Renglón de las listas (concepto, detalle en letra chica y monto ya formateado).</summary>
        public class Renglon
        {
            public string Concepto { get; set; }
            public string Detalle { get; set; }
            public string Monto { get; set; }
            public string Clase { get; set; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();
            if (IsPostBack) return;

            if (!SecurityHelper.EsConductor())
            {
                RolesHelper.RedirigirSegunRol();
                return;
            }

            try
            {
                if (!int.TryParse(Request.QueryString["id"], out int idOrdenViaje) || idOrdenViaje <= 0)
                    throw new ErrorNegocioException("No se indicó una liquidación válida.");
                if (!EsDelConductor(idOrdenViaje))
                    throw new ErrorNegocioException("No tienes permiso para ver esta liquidación.");

                DetalleLiquidacion d = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
                if (d == null)
                    throw new ErrorNegocioException("No se encontró la liquidación.");

                Mostrar(idOrdenViaje, d);
                pnlContenido.Visible = true;
            }
            catch (Exception ex)
            {
                if (!(ex is ErrorNegocioException))
                    LogSGV.Error(ex, "Error al cargar el detalle de la liquidación del conductor");
                litError.Text = MensajeErrorHelper.ParaUsuario(ex);
                pnlError.Visible = true;
            }
        }

        /// <summary>La orden es del conductor de la sesión (anti-IDOR).</summary>
        private bool EsDelConductor(int idOrdenViaje)
        {
            int idConductor = Session["IdConductor"] != null ? Convert.ToInt32(Session["IdConductor"]) : 0;
            if (idConductor <= 0) return false;
            object n = DbHelper.EjecutarEscalar(
                "SELECT COUNT(1) FROM OrdenViaje WHERE idOrdenViaje = @id AND idConductor = @idConductor",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@idConductor", idConductor));
            return n != null && n != DBNull.Value && Convert.ToInt32(n) > 0;
        }

        private void Mostrar(int idOrdenViaje, DetalleLiquidacion d)
        {
            string estado = (d.EstadoAprobacion ?? "").Trim().ToUpperInvariant();
            bool porConformar = estado == "POR_CONFORMAR";
            bool aprobada = estado == "APROBADO" || estado == "COMPLETADO";
            bool rechazada = estado == "REABIERTO" || estado == "RECHAZADO";

            litNumero.Text = d.NumeroOrdenViaje;
            litPeriodo.Text = d.FechaSalida + " → " + d.FechaLlegada;
            lblEstado.Text = aprobada ? "Aprobada" : porConformar ? "Firma tu conformidad" : rechazada ? "Rechazada" : "En revisión";
            lblEstado.CssClass = "dc-pill dc-pill-" + (aprobada ? "completado" : porConformar ? "en_proceso" : rechazada ? "cancelado" : "programado");

            pnlPorConformar.Visible = porConformar;
            lnkConformar.NavigateUrl = "~/Views/ConformarLiquidacion.aspx?id=" + idOrdenViaje;

            litConductor.Text = d.NombreConductor;
            litUnidades.Text = d.PlacaTracto + " / " + d.PlacaCarreta;
            litSalida.Text = Unir(" · ", d.FechaSalida, d.HoraSalida);
            litLlegada.Text = Unir(" · ", d.FechaLlegada,
                !string.IsNullOrEmpty(d.HoraLlegadaGps) ? d.HoraLlegadaGps + " (GPS)"
                : !string.IsNullOrEmpty(d.HoraLlegadaDeclarada) ? d.HoraLlegadaDeclarada : d.HoraLlegadaSistema);

            // Gastos declarados
            var gastos = new List<Renglon>();
            void Gasto(string concepto, decimal s, decimal us, string detalle)
            {
                if (s != 0 || us != 0) gastos.Add(new Renglon { Concepto = concepto, Detalle = detalle, Monto = Montos(s, us) });
            }
            Gasto("Combustible", d.GastosCombustibleSoles, d.GastosCombustibleDolares, d.DescCombustible);
            Gasto("Alimentación", d.GastosAlimentacionSoles, d.GastosAlimentacionDolares, d.DescAlimentacion);
            Gasto("Peajes", d.GastosPeajesSoles, d.GastosPeajesDolares, d.DescPeajes);
            Gasto("Hospedaje", d.GastosHospedajeSoles, d.GastosHospedajeDolares, d.DescHospedaje);
            Gasto("Movilidad", d.GastosMovilidadSoles, d.GastosMovilidadDolares, d.DescMovilidad);
            Gasto("Encarpada / desencarpada", d.GastosEncarpadaSoles, d.GastosEncarpadaDolares, d.DescEncarpada);
            Gasto("Reparaciones y varios", d.GastosReparacionesSoles, d.GastosReparacionesDolares, d.DescReparaciones);
            Gasto("Apoyo / seguridad", d.GastosApoyoSeguridadSoles, d.GastosApoyoSeguridadDolares, d.DescApoyoSeguridad);
            foreach (var a in d.DetallesGastosAdicionales ?? new List<ItemAdicional>())
                Gasto(a.Nombre, a.Soles, a.Dolares, a.Descripcion);
            rptGastos.DataSource = gastos;
            rptGastos.DataBind();
            phSinGastos.Visible = gastos.Count == 0;
            litTotalGastos.Text = Montos(d.TotalGastosSoles, d.TotalGastosDolares, siempre: true);

            // La liquidación final solo existe cuando la administración ya la revisó.
            bool revisada = porConformar || aprobada;
            pnlSinRevision.Visible = !revisada;
            pnlLiquidacion.Visible = revisada;
            if (revisada)
            {
                decimal saldoS = LiquidacionCalculos.SaldoFinal(d.TotalIngresosSoles, d.TotalGastosSoles, d.DescuentoSoles, d.ReintegroSoles);
                decimal saldoD = LiquidacionCalculos.SaldoFinal(d.TotalIngresosDolares, d.TotalGastosDolares, d.DescuentoDolares, d.ReintegroDolares);

                var filas = new List<Renglon>
                {
                    new Renglon { Concepto = "Dinero que te entregaron", Monto = Montos(d.TotalIngresosSoles, d.TotalIngresosDolares, siempre: true) },
                    new Renglon { Concepto = "(−) Tus gastos declarados", Monto = Montos(d.TotalGastosSoles, d.TotalGastosDolares, siempre: true) }
                };
                if (d.DescuentoSoles != 0 || d.DescuentoDolares != 0)
                    filas.Add(new Renglon { Concepto = "(+) Descuento", Detalle = "Motivo: " + d.DescuentoMotivo, Monto = Montos(d.DescuentoSoles, d.DescuentoDolares), Clase = "dv-ajuste" });
                if (d.ReintegroSoles != 0 || d.ReintegroDolares != 0)
                    filas.Add(new Renglon { Concepto = "(−) Reintegro", Detalle = "Motivo: " + d.ReintegroMotivo, Monto = Montos(d.ReintegroSoles, d.ReintegroDolares), Clase = "dv-ajuste" });
                filas.Add(new Renglon { Concepto = "Saldo final", Monto = Montos(saldoS, saldoD, siempre: true), Clase = "dc-resumen-total" });
                rptLiquidacion.DataSource = filas;
                rptLiquidacion.DataBind();
                litSaldoTexto.Text = LiquidacionCalculos.DescribirSaldo(saldoS, saldoD);

                var entregas = (d.Entregas ?? new List<MovimientoViaje>()).Select(m => new Renglon
                {
                    Concepto = m.Fecha.ToString("dd/MM/yyyy HH:mm") + " · " + MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto),
                    Detalle = Unir(" · ", MovimientosViajeService.NombreMedio(m.Medio),
                        string.IsNullOrEmpty(m.NumeroOperacion) ? null : "op. " + m.NumeroOperacion, m.Descripcion),
                    Monto = Montos(m.MontoSoles, m.MontoDolares)
                }).ToList();
                rptEntregas.DataSource = entregas;
                rptEntregas.DataBind();
                pnlEntregas.Visible = entregas.Count > 0;

                var pagos = (d.PagosDirectos ?? new List<MovimientoViaje>()).Select(m => new Renglon
                {
                    Concepto = m.Fecha.ToString("dd/MM/yyyy") + " · " + MovimientosViajeService.NombreConcepto(m.Tipo, m.Concepto),
                    Detalle = Unir(" · ", m.NombreProveedor, m.Comprobante, m.Descripcion),
                    Monto = Montos(m.MontoSoles, m.MontoDolares)
                }).ToList();
                rptPagosDirectos.DataSource = pagos;
                rptPagosDirectos.DataBind();
                pnlPagosDirectos.Visible = pagos.Count > 0;

                litNota.Text = d.NotaAprobacion;
                pnlNota.Visible = !string.IsNullOrWhiteSpace(d.NotaAprobacion);
            }

            string observaciones = LiquidacionCalculos.ObservacionesDelConductor(d.Observaciones);
            litObservaciones.Text = observaciones;
            pnlObservaciones.Visible = !string.IsNullOrWhiteSpace(observaciones);

            // El PDF existe desde la primera firma; se sirve el documento firmado vigente.
            lnkPdf.Visible = FirmaService.ObtenerEventosVigentes(idOrdenViaje).Any();
            lnkPdf.NavigateUrl = "~/Views/DescargarPdfOrdenViaje.aspx?id=" + idOrdenViaje;
        }

        /// <summary>"S/ 641.90 · US$ 27.25"; un monto en cero se omite (si ambos lo son, "S/ 0.00" o "—").</summary>
        private static string Montos(decimal soles, decimal dolares, bool siempre = false)
        {
            var partes = new List<string>();
            if (soles != 0 || (siempre && dolares == 0)) partes.Add("S/ " + soles.ToString("N2", CulturaPe));
            if (dolares != 0) partes.Add("US$ " + dolares.ToString("N2", CulturaPe));
            return partes.Count > 0 ? string.Join(" · ", partes) : "—";
        }

        private static string Unir(string separador, params string[] partes) =>
            string.Join(separador, partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
    }
}
