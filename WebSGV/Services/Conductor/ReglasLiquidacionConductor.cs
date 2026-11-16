using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;

namespace WebSGV.Services.Conductor
{
    /// <summary>Qué se hace con los peajes y los dólares de la liquidación al enviarla.</summary>
    public enum ResultadoAmbito
    {
        Internacional,
        NacionalAplicarCeros,
        Rechazar
    }

    /// <summary>Resultado de <see cref="ReglasLiquidacionConductor.DecidirAmbito"/>; con mensaje solo si se rechaza.</summary>
    public sealed class DecisionAmbito
    {
        public DecisionAmbito(ResultadoAmbito resultado, string mensaje)
        {
            Resultado = resultado;
            Mensaje = mensaje;
        }

        public ResultadoAmbito Resultado { get; }
        public string Mensaje { get; }
    }

    /// <summary>
    /// Reglas puras (sin System.Web ni BD) que el servidor aplica a la liquidación del conductor
    /// antes de guardarla, sin fiarse de lo que el navegador oculte o recorte.
    /// </summary>
    public static class ReglasLiquidacionConductor
    {
        /// <summary>Concepto de un gasto/ingreso adicional (columna <c>nombreCategoria</c>, VARCHAR(100)).</summary>
        public const int LargoConcepto = 100;

        /// <summary>Detalle de un gasto (los <c>maxlength="250"</c> del formulario y las columnas de 250).</summary>
        public const int LargoDetalle = 250;

        /// <summary>Descripción combinada de peajes (<c>Egresos.descPeajes</c>, VARCHAR(1000)).</summary>
        public const int LargoDescPeajes = 1000;

        /// <summary>Estación de peaje (<c>DetallePeajes.estacion</c>, VARCHAR(100)).</summary>
        public const int LargoEstacion = 100;

        /// <summary>Número de comprobante (columnas <c>numeroComprobante</c>, VARCHAR(50)).</summary>
        public const int LargoComprobante = 50;

        /// <summary>
        /// Un viaje es internacional si alguno de los viajes que se liquidan está marcado así en la BD
        /// (<c>ViajesEnProgreso.esInternacional</c>, que toma el ámbito de sus despachos) o si el GPS vio
        /// el tracto cruzar a Ecuador. Es la misma regla con la que la página llena <c>hfEsInternacional</c>
        /// (MostrarViajesActivos + HabilitarLiquidacion), pero calculada en el servidor al enviar.
        /// </summary>
        public static bool EsInternacional(IEnumerable<bool> viajesInternacionales, bool gpsCruzaFrontera) =>
            gpsCruzaFrontera || (viajesInternacionales != null && viajesInternacionales.Any(v => v));

        public const string MensajeGpsSinRespuesta =
            "No se pudo confirmar el recorrido del viaje con el GPS; inténtalo de nuevo en unos minutos.";

        public const string MensajeAmbitoCambio =
            "Cambió el ámbito del viaje; recarga la página y revisa la liquidación.";

        /// <summary>
        /// Decide qué hacer con los peajes y los montos en dólares al enviar:
        /// <list type="bullet">
        /// <item>Internacional según el servidor (BD o GPS): se guardan.</item>
        /// <item>Nacional según el servidor, pero el GPS no respondió y hay peajes/dólares, o la página
        ///   mostraba un viaje internacional y el GPS no respondió: se rechaza con
        ///   <see cref="MensajeGpsSinRespuesta"/> (el GPS puede ser la única fuente del cruce a Ecuador,
        ///   p. ej. terceros CRISAR/AFRECHO/NOVOPAN), sin borrar nada.</item>
        /// <item>Nacional según el servidor, pero la página mostraba un viaje internacional: el ámbito
        ///   cambió entre la carga y el envío; se rechaza con <see cref="MensajeAmbitoCambio"/>.</item>
        /// <item>Página y servidor coinciden en nacional: se ponen en 0 (los campos están ocultos; si
        ///   llegan con valor es manipulación).</item>
        /// </list>
        /// </summary>
        public static DecisionAmbito DecidirAmbito(IEnumerable<bool> viajesInternacionales, bool gpsCruzaFrontera,
            bool gpsConsultaFallida, bool paginaDijoInternacional, bool hayPeajesODolares)
        {
            if (EsInternacional(viajesInternacionales, gpsCruzaFrontera))
                return new DecisionAmbito(ResultadoAmbito.Internacional, null);

            if (gpsConsultaFallida && (hayPeajesODolares || paginaDijoInternacional))
                return new DecisionAmbito(ResultadoAmbito.Rechazar, MensajeGpsSinRespuesta);

            if (paginaDijoInternacional)
                return new DecisionAmbito(ResultadoAmbito.Rechazar, MensajeAmbitoCambio);

            return new DecisionAmbito(ResultadoAmbito.NacionalAplicarCeros, null);
        }

        /// <summary>Hay algún peaje o algún monto en dólares de los que registra el conductor.</summary>
        public static bool TienePeajesODolares(LiquidacionConductorInput input)
        {
            if (input == null) return false;
            if (input.PeajesSoles > 0m || input.PeajesDolares > 0m ||
                input.AlimentacionDolares > 0m || input.ApoyoSeguridadDolares > 0m || input.ReparacionesDolares > 0m ||
                input.MovilidadDolares > 0m || input.EncapadaDolares > 0m || input.HospedajeDolares > 0m ||
                input.CombustibleDolares > 0m)
                return true;
            if (input.GastosAdicionales != null && input.GastosAdicionales.Any(g => g != null && (g.Dolares ?? 0m) > 0m))
                return true;
            return input.GastosFinancieros != null && input.GastosFinancieros.Any(g => g != null &&
                (g.Dolares > 0m || (string.Equals(g.Categoria?.Trim(), "peajes", System.StringComparison.OrdinalIgnoreCase)
                                    && g.Soles > 0m)));
        }

        /// <summary>
        /// En un viaje nacional el conductor no ve peajes ni montos en dólares (la empresa paga los
        /// peajes a la concesionaria y no hay gastos en Ecuador): el formulario los oculta y los vacía.
        /// Si aun así llegan con valor (campo oculto o formulario manipulado) se ponen en 0 aquí, igual
        /// que hace el JS al preparar el envío; los otros gastos que quedan sin monto se descartan.
        /// Los ingresos y ajustes de la administración no se tocan.
        /// </summary>
        public static void AplicarViajeNacional(LiquidacionConductorInput input)
        {
            if (input == null) return;

            input.PeajesSoles = 0m;
            input.PeajesDolares = 0m;
            input.DescPeajes = "";

            input.AlimentacionDolares = 0m;
            input.ApoyoSeguridadDolares = 0m;
            input.ReparacionesDolares = 0m;
            input.MovilidadDolares = 0m;
            input.EncapadaDolares = 0m;
            input.HospedajeDolares = 0m;
            input.CombustibleDolares = 0m;

            if (input.GastosAdicionales != null)
            {
                foreach (var g in input.GastosAdicionales) g.Dolares = 0m;
                input.GastosAdicionales.RemoveAll(g => g == null || (g.Soles ?? 0m) <= 0m);
            }

            if (input.GastosFinancieros != null)
            {
                input.GastosFinancieros.RemoveAll(g => g == null ||
                    string.Equals(g.Categoria?.Trim(), "peajes", System.StringComparison.OrdinalIgnoreCase));
                foreach (var g in input.GastosFinancieros) g.Dolares = 0m;
            }
        }

        /// <summary>
        /// Lanza <see cref="ErrorNegocioException"/> si el texto supera <paramref name="maximo"/>
        /// caracteres. Antes el parámetro del SP lo cortaba sin avisar y el PDF firmado mostraba el
        /// texto incompleto.
        /// </summary>
        public static string ValidarLargo(string valor, int maximo, string campo)
        {
            if (valor != null && valor.Length > maximo)
                throw new ErrorNegocioException(
                    $"El texto de «{campo}» tiene {valor.Length} caracteres y el máximo es {maximo}. Acórtalo e intenta de nuevo.");
            return valor;
        }

        /// <summary>Revisa el largo de todos los textos que se guardan con la liquidación.</summary>
        public static void ValidarTextos(LiquidacionConductorInput input)
        {
            if (input == null) return;

            ValidarLargo(input.DescPeajes, LargoDescPeajes, "Detalle de peajes");
            ValidarLargo(input.DescAlimentacion, LargoDetalle, "Detalle de alimentación");
            ValidarLargo(input.DescApoyoSeguridad, LargoDetalle, "Detalle de apoyo de seguridad");
            ValidarLargo(input.DescReparaciones, LargoDetalle, "Detalle de reparaciones");
            ValidarLargo(input.DescMovilidad, LargoDetalle, "Detalle de movilidad");
            ValidarLargo(input.DescEncapada, LargoDetalle, "Detalle de encarpada");
            ValidarLargo(input.DescHospedaje, LargoDetalle, "Detalle de hospedaje");
            ValidarLargo(input.DescCombustible, LargoDetalle, "Detalle de combustible");

            ValidarLargo(input.DescDespacho, LargoDetalle, "Detalle del dinero de despacho");
            ValidarLargo(input.DescMensualidad, LargoDetalle, "Detalle de mensualidad");
            ValidarLargo(input.DescOtros, LargoDetalle, "Detalle de otros ingresos");
            ValidarLargo(input.DescPrestamo, LargoDetalle, "Detalle del préstamo");

            if (input.GastosAdicionales != null)
                foreach (var g in input.GastosAdicionales.Where(x => x != null))
                {
                    ValidarLargo(g.Categoria ?? g.NombreCategoria, LargoConcepto, "Concepto de otro gasto");
                    ValidarLargo(g.Descripcion, LargoDetalle, "Detalle de otro gasto");
                }

            if (input.IngresosAdicionales != null)
                foreach (var i in input.IngresosAdicionales.Where(x => x != null))
                {
                    ValidarLargo(i.Categoria ?? i.NombreCategoria, LargoConcepto, "Concepto de ingreso adicional");
                    ValidarLargo(i.Descripcion, LargoDetalle, "Detalle de ingreso adicional");
                }

            if (input.GastosFinancieros != null)
                foreach (var f in input.GastosFinancieros.Where(x => x != null))
                {
                    ValidarLargo(f.Estacion ?? f.Lugar, LargoEstacion, "Estación de peaje");
                    ValidarLargo(f.Comprobante, LargoComprobante, "Número de comprobante");
                    ValidarLargo(f.Observaciones, LargoDetalle, "Observaciones del comprobante");
                }
        }
    }
}
