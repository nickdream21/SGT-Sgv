using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Reglas puras de la edición de un lote (E6-28, antes una vista de ListaDespachos): la URL de la página, el id virtual
    /// que tendrá el lote tras guardar, a dónde se vuelve, los avisos de vuelta de ListaDespachos (<c>?msg=</c>), si el
    /// lote se puede editar y el texto de su auditoría. Sin BD ni controles.
    /// </summary>
    public static class EditarLoteReglas
    {
        public const string Ruta = "~/Views/EditarLote.aspx";
        public const string RutaLista = "~/Views/ListaDespachos.aspx";

        /// <summary>Código de <c>?msg=</c>: el lote se guardó con cambios.</summary>
        public const string AvisoGuardado = "lote_guardado";
        /// <summary>Código de <c>?msg=</c>: no había nada distinto que guardar.</summary>
        public const string AvisoSinCambios = "lote_sin_cambios";

        /// <summary>URL de la página (relativa a la aplicación, con ~) con <c>?lote=</c> codificado.</summary>
        public static string Url(string idLote) => Ruta + "?lote=" + HttpUtility.UrlEncode(idLote ?? "");

        /// <summary>
        /// Id virtual de un lote con la misma forma que arma <c>sp_LD_ObtenerLotesRegistrados</c> (su CONCAT): pedido NULL
        /// → NOPEDIDO, pedido '' se queda vacío (ISNULL no lo cambia), fecha yyyy-MM-dd y el ámbito como 0 o 1.
        /// </summary>
        public static string IdLoteVirtual(int idCliente, string pedido, DateTime fecha, string tipoOperacion, bool esInternacional, int idPlanta) =>
            idCliente.ToString(CultureInfo.InvariantCulture) + "_" +
            (pedido ?? "NOPEDIDO") + "_" +
            fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" +
            tipoOperacion + "_" +
            (esInternacional ? "1" : "0") + "_" +
            idPlanta.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Id que tendrá el lote tras guardar <paramref name="input"/>: el cliente no se edita (el de <paramref name="antes"/>)
        /// y un pedido vacío se guarda como NULL (como hace <c>ListaDespachosService.GuardarCambiosLote</c>).
        /// </summary>
        public static string IdTrasGuardar(LoteRegistrado antes, GuardarCambiosLoteInput input) =>
            IdLoteVirtual(antes.IdCliente, string.IsNullOrEmpty(input.NumeroPedido) ? null : input.NumeroPedido,
                input.FechaDespacho, input.TipoOperacion, input.EsInternacional, input.IdPlanta);

        /// <summary>
        /// A dónde vuelve "Guardar cambios": sin cambios, al detalle del lote original; con cambios, al detalle con el id
        /// nuevo si existe, si no al original y, si ninguno existe (otra pestaña lo cambió), a la lista de lotes.
        /// </summary>
        public static string DestinoTrasGuardar(string original, string candidato, bool sinCambios, Func<string, bool> existe)
        {
            if (sinCambios) return Detalle(original, AvisoSinCambios);
            if (!string.IsNullOrEmpty(candidato) && existe(candidato)) return Detalle(candidato, AvisoGuardado);
            if (!string.IsNullOrEmpty(original) && existe(original)) return Detalle(original, AvisoGuardado);
            return RutaLista + "?vista=lotes&msg=" + AvisoGuardado;
        }

        private static string Detalle(string idLote, string aviso) =>
            RutaLista + "?lote=" + HttpUtility.UrlEncode(idLote ?? "") + "&msg=" + aviso;

        /// <summary>
        /// Texto y tipo del aviso de <c>?msg=</c> en ListaDespachos; null si no es uno de los dos códigos (no se usa
        /// <c>?aviso=</c>, que es de Site.Master y ErrorAplicacionHelper).
        /// </summary>
        public static Tuple<string, string> TextoAviso(string codigo)
        {
            if (string.Equals(codigo, AvisoGuardado, StringComparison.Ordinal)) return Tuple.Create("Cambios del lote guardados.", "success");
            if (string.Equals(codigo, AvisoSinCambios, StringComparison.Ordinal)) return Tuple.Create("No había cambios que guardar.", "info");
            return null;
        }

        /// <summary>Un lote se edita si existe, tiene despachos y no está anulado.</summary>
        public static bool EsEditable(LoteRegistrado lote) =>
            lote != null && lote.IdsDespachos != null && lote.IdsDespachos.Count > 0 && lote.EstadoLote != "ANULADO";

        /// <summary>Descripción de la auditoría del lote editado: despachos, conductores cambiados y documentos tocados.</summary>
        public static string DescripcionAuditoria(string idsCsv, GuardarCambiosLoteInput input, ResultadoEdicionLote resultado)
        {
            string documentos = "";
            if (resultado.Factura != null) documentos += $"; factura {input.NumeroFactura?.Trim()} (id {resultado.Factura.Id}, {resultado.Factura.Accion})";
            if (resultado.Cpic != null) documentos += $"; CPIC {input.NumeroCPIC?.Trim()} (id {resultado.Cpic.Id}, {resultado.Cpic.Accion})";
            if (resultado.CpicsQuitadas.Count > 0) documentos += $"; CPIC quitada (id {string.Join(", ", resultado.CpicsQuitadas)})";
            if (resultado.FacturasQuitadas.Count > 0) documentos += $"; factura quitada (id {string.Join(", ", resultado.FacturasQuitadas)})";
            string conductores = resultado.ConductoresCambiados.Count == 0 ? "" :
                "; conductor cambiado en " + string.Join(", ", resultado.ConductoresCambiados.Select(c => $"despacho {c.Key} → idConductor {c.Value}"));
            return $"Lote editado - despachos {idsCsv}{conductores}{documentos}";
        }

        /// <summary>Monto obligatorio del formulario: vacío, no numérico, negativo o sobre el tope agrega el error del campo.</summary>
        public static void ValidarMontoObligatorio(string texto, string campo, List<string> errores)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                errores.Add($"El campo «{campo}» es obligatorio");
                return;
            }
            try { MontoHelper.LeerMonto(texto, campo); }
            catch (ErrorNegocioException ex) { errores.Add(ex.Message.TrimEnd('.')); }
        }
    }
}
