using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Services;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    /// <summary>
    /// El conductor revisa la liquidación final que preparó la administración (dinero entregado,
    /// gastos, descuentos y reintegros con su motivo) y firma su conformidad, con trazo y
    /// contraseña. Si no está de acuerdo, la observa y vuelve a la administración. Se accede con
    /// ?id=[idOrdenViaje]; solo el conductor dueño de la orden y solo si está POR_CONFORMAR.
    /// </summary>
    public partial class ConformarLiquidacion : PaginaBase
    {
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

            if (!int.TryParse(Request.QueryString["id"], out int idOrdenViaje) || idOrdenViaje <= 0
                || !EstaPorConformar(idOrdenViaje, SecurityHelper.ObtenerIdUsuario()))
            {
                Response.Redirect(ResolveUrl("~/Views/DashboardConductor.aspx"), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            hfIdOrdenViaje.Value = idOrdenViaje.ToString();
        }

        /// <summary>La orden es del conductor autenticado y espera su conformidad (anti-IDOR).</summary>
        private static bool EstaPorConformar(int idOrdenViaje, int idUsuario)
        {
            object n = DbHelper.EjecutarEscalar(@"
                SELECT COUNT(1)
                FROM OrdenViaje ov
                INNER JOIN Usuarios u ON u.idConductor = ov.idConductor AND u.activo = 1
                WHERE ov.idOrdenViaje = @id AND u.idUsuario = @idUsuario
                  AND ov.estadoAprobacion = 'POR_CONFORMAR' AND ov.idFirmaAdmin IS NOT NULL",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@idUsuario", idUsuario));
            return n != null && n != DBNull.Value && Convert.ToInt32(n) > 0;
        }

        private static string ValidarConductor(int idOrdenViaje)
        {
            if (!SecurityHelper.TieneSesionActiva() || SecurityHelper.ObtenerIdUsuario() == 0)
                return "Sesión no válida. Inicie sesión nuevamente.";
            if (!SecurityHelper.EsConductor())
                return "Solo el conductor puede dar su conformidad.";
            if (idOrdenViaje <= 0 || !EstaPorConformar(idOrdenViaje, SecurityHelper.ObtenerIdUsuario()))
                return "La liquidación no está esperando su conformidad.";
            return null;
        }

        public class ConceptoMonto
        {
            public string Concepto { get; set; }
            public string Detalle { get; set; }
            public decimal Soles { get; set; }
            public decimal Dolares { get; set; }
        }

        public class LiquidacionParaConformidad
        {
            public string NumeroOrdenViaje { get; set; }
            public string NombreConductor { get; set; }
            public string DocumentoConductor { get; set; }
            public string Periodo { get; set; }
            public string Unidades { get; set; }
            public List<ConceptoMonto> Entregado { get; set; }
            public decimal EntregadoSoles { get; set; }
            public decimal EntregadoDolares { get; set; }
            public decimal GastosSoles { get; set; }
            public decimal GastosDolares { get; set; }
            public decimal BalanceSoles { get; set; }
            public decimal BalanceDolares { get; set; }
            public decimal DescuentoSoles { get; set; }
            public decimal DescuentoDolares { get; set; }
            public string DescuentoMotivo { get; set; }
            public decimal ReintegroSoles { get; set; }
            public decimal ReintegroDolares { get; set; }
            public string ReintegroMotivo { get; set; }
            public decimal SaldoSoles { get; set; }
            public decimal SaldoDolares { get; set; }
            public string SaldoSolesEnLetras { get; set; }
            public string SaldoDolaresEnLetras { get; set; }
            public string NotaAdministracion { get; set; }
            public string RevisadoPor { get; set; }
            public string FechaRevision { get; set; }
            /// <summary>Huella de lo que se muestra: la conformidad solo se acepta si no cambió.</summary>
            public string Huella { get; set; }
        }

        [WebMethod(EnableSession = true)]
        public static object ObtenerLiquidacion(int idOrdenViaje)
        {
            try
            {
                string error = ValidarConductor(idOrdenViaje);
                if (error != null) return new { success = false, message = error };

                var d = LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrdenViaje);
                if (d == null) return new { success = false, message = "No se encontró la liquidación." };

                var revision = FirmaService.ObtenerEventosVigentes(idOrdenViaje)
                    .LastOrDefault(ev => ev.Etapa == EtapaFirma.Revision);

                var entregado = new List<ConceptoMonto>
                {
                    new ConceptoMonto { Concepto = "Adelanto para el viaje", Detalle = d.DescDespacho, Soles = d.DespachoSoles, Dolares = d.DespachoDolares },
                    new ConceptoMonto { Concepto = "Mensualidad", Detalle = d.DescMensualidad, Soles = d.MensualidadSoles, Dolares = d.MensualidadDolares },
                    new ConceptoMonto { Concepto = "Préstamo", Detalle = d.DescPrestamo, Soles = d.PrestamoSoles, Dolares = d.PrestamoDolares },
                    new ConceptoMonto { Concepto = "Otros autorizados", Detalle = d.DescOtros, Soles = d.OtrosIngresosSoles, Dolares = d.OtrosIngresosDolares }
                };
                entregado.AddRange((d.DetallesIngresosAdicionales ?? new List<ItemAdicional>())
                    .Select(a => new ConceptoMonto { Concepto = a.Nombre, Detalle = a.Descripcion, Soles = a.Soles, Dolares = a.Dolares }));
                entregado = entregado.Where(c => c.Soles != 0 || c.Dolares != 0).ToList();

                decimal balS = d.TotalIngresosSoles - d.TotalGastosSoles, balD = d.TotalIngresosDolares - d.TotalGastosDolares;
                decimal saldoS = balS - d.DescuentoSoles + d.ReintegroSoles, saldoD = balD - d.DescuentoDolares + d.ReintegroDolares;

                var r = new LiquidacionParaConformidad
                {
                    NumeroOrdenViaje = d.NumeroOrdenViaje,
                    NombreConductor = d.NombreConductor,
                    DocumentoConductor = d.DocumentoConductor,
                    Periodo = d.FechaSalida + " → " + d.FechaLlegada,
                    Unidades = d.PlacaTracto + " / " + d.PlacaCarreta,
                    Entregado = entregado,
                    EntregadoSoles = d.TotalIngresosSoles,
                    EntregadoDolares = d.TotalIngresosDolares,
                    GastosSoles = d.TotalGastosSoles,
                    GastosDolares = d.TotalGastosDolares,
                    BalanceSoles = balS,
                    BalanceDolares = balD,
                    DescuentoSoles = d.DescuentoSoles,
                    DescuentoDolares = d.DescuentoDolares,
                    DescuentoMotivo = d.DescuentoMotivo,
                    ReintegroSoles = d.ReintegroSoles,
                    ReintegroDolares = d.ReintegroDolares,
                    ReintegroMotivo = d.ReintegroMotivo,
                    SaldoSoles = saldoS,
                    SaldoDolares = saldoD,
                    SaldoSolesEnLetras = NumeroALetrasHelper.Convertir(saldoS, "SOLES"),
                    SaldoDolaresEnLetras = saldoD != 0 ? NumeroALetrasHelper.Convertir(saldoD, "DÓLARES AMERICANOS") : null,
                    NotaAdministracion = d.NotaAprobacion,
                    RevisadoPor = revision?.Nombre,
                    FechaRevision = revision?.FechaHora.ToString("dd/MM/yyyy HH:mm"),
                    Huella = HuellaLiquidacion.Calcular(d)
                };
                return new { success = true, datos = r };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar la liquidación para conformidad (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "No se pudo cargar la liquidación: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object RegistrarConformidad(int idOrdenViaje, string firmaPngBase64, string contrasena, string huella)
        {
            try
            {
                string error = ValidarConductor(idOrdenViaje);
                if (error != null) return new { success = false, message = error };

                byte[] png = LiquidacionCalculos.DecodificarPngBase64(firmaPngBase64);
                if (png == null) return new { success = false, message = "La firma recibida es inválida." };

                var ctx = System.Web.HttpContext.Current;
                var contexto = LiquidacionesPendientes.ContextoFirmaActual(ctx);

                string errorClave = ReautenticacionHelper.Verificar(contexto.IdUsuario, contrasena);
                if (errorClave != null)
                    return new { success = false, message = errorClave, contrasenaIncorrecta = true };

                var r = new FirmaService().RegistrarConformidadConductor(idOrdenViaje, contexto, png, huella);
                if (r.Exito)
                {
                    object numero = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);
                    AuditoriaHelper.Registrar("CONFORMIDAD", "OrdenViaje", idOrdenViaje,
                        "Conductor firmó su conformidad con la liquidación final " + numero + " (documento " + r.CodigoVerificacion + "). Viaje completado.");
                }
                return new { success = r.Exito, message = r.Mensaje, codigo = r.CodigoVerificacion, hashPdf = r.HashPdf };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar la conformidad (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "No se pudo registrar su conformidad: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object ObservarLiquidacion(int idOrdenViaje, string observacion)
        {
            try
            {
                string error = ValidarConductor(idOrdenViaje);
                if (error != null) return new { success = false, message = error };

                observacion = LiquidacionCalculos.NormalizarTexto(observacion, ReglasFirma.MaximoMotivo);
                if (string.IsNullOrWhiteSpace(observacion) || observacion.Length < ReglasFirma.MinimoMotivo)
                    return new { success = false, message = "Explique con qué no está de acuerdo (al menos " + ReglasFirma.MinimoMotivo + " caracteres)." };

                var ctx = System.Web.HttpContext.Current;
                var contexto = LiquidacionesPendientes.ContextoFirmaActual(ctx);
                string nombre = SesionHelper.ObtenerNombre(ctx.Session);

                var r = FirmaService.DevolverARevision(idOrdenViaje, "OBSERVACION", contexto, nombre, observacion);
                if (r.Exito)
                {
                    object numero = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);
                    AuditoriaHelper.Registrar("OBSERVAR_LIQUIDACION", "OrdenViaje", idOrdenViaje,
                        "El conductor observó la liquidación final " + numero + ": " + observacion);
                    NotificacionService.NotificarObservacionConductor(Convert.ToString(numero), nombre, observacion);
                }
                return new { success = r.Exito, message = r.Mensaje };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar la observación del conductor (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "No se pudo enviar su observación: " + MensajeErrorHelper.ParaUsuario(ex) };
            }
        }
    }
}
