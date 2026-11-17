using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos
{
    /// <summary>Qué campos de documentos muestra "Editar lote".</summary>
    public class PanelesDocumentosLote
    {
        public bool MostrarFactura { get; set; }
        public bool MostrarCpic { get; set; }

        /// <summary>
        /// El lote lleva más de una factura o CPIC: Editar lote no los muestra (escribir un número los juntaría en uno)
        /// y remite a "Factura y CPIC por despacho".
        /// </summary>
        public bool VariosDocumentos { get; set; }
    }

    /// <summary>Qué hacer con la factura o la CPIC al guardar Editar lote.</summary>
    public enum AccionDocumentoLote
    {
        /// <summary>No tocarlo (panel oculto o sin número).</summary>
        NADA,
        /// <summary>Enlazar, crear, renombrar o actualizar (sp_LD_GestionarFacturaLote / sp_LD_GestionarCPICLote).</summary>
        GESTIONAR,
        /// <summary>Quitarlo de los despachos del lote; el documento no se borra (sp_LD_DesvincularDocumentoLote).</summary>
        QUITAR
    }

    /// <summary>Lo que de verdad cambia al guardar Editar lote (<see cref="EdicionDocumentosLote.CalcularCambios"/>).</summary>
    public class CambiosEdicionLote
    {
        /// <summary>Despachos con algún dato del lote o el conductor distinto de lo guardado.</summary>
        public List<int> DespachosAActualizar { get; set; } = new List<int>();
        /// <summary>idDespacho → nuevo conductor, solo los que cambian.</summary>
        public Dictionary<int, int> ConductoresCambiados { get; set; } = new Dictionary<int, int>();
        public bool GestionarFactura { get; set; }
        public bool GestionarCpic { get; set; }
        /// <summary>Ids de las facturas / CPIC que se quitan (las que hoy llevan los despachos del lote).</summary>
        public List<int> FacturasAQuitar { get; set; } = new List<int>();
        public List<int> CpicsAQuitar { get; set; } = new List<int>();

        public bool HayCambios =>
            DespachosAActualizar.Count > 0 || ConductoresCambiados.Count > 0 || GestionarFactura || GestionarCpic
            || FacturasAQuitar.Count > 0 || CpicsAQuitar.Count > 0;
    }

    /// <summary>
    /// Reglas puras de la factura y la CPIC en la edición de un lote (ListaDespachos; 2026-11-17, bloque 4, tarea 11).
    /// La CPIC es una por factura comercial (embarque) y la factura es del cliente; un lote de un mismo día puede
    /// llevar dos (Novopan). Antes el panel de la CPIC se ocultaba en la carga nacional y, al guardar, el lote se
    /// quedaba sin su CPIC: aquí nada se oculta si el lote lo tiene, y nada se desvincula por estar oculto.
    /// </summary>
    public static class EdicionDocumentosLote
    {
        public static bool TieneVariosDocumentos(int cantidadFacturas, int cantidadCpic) => cantidadFacturas > 1 || cantidadCpic > 1;

        /// <summary>
        /// Paneles de Editar lote: la factura en las cargas; la CPIC en lo internacional, en la carga de un cliente que
        /// cruza (perfil CPIC) y siempre que el lote ya tenga una. Con más de un documento, ninguno.
        /// </summary>
        public static PanelesDocumentosLote Paneles(string tipoOperacion, bool esInternacional, bool loteTieneCpic,
            string perfilDocumentos, int cantidadFacturas, int cantidadCpic)
        {
            var p = new PanelesDocumentosLote { VariosDocumentos = TieneVariosDocumentos(cantidadFacturas, cantidadCpic) };
            if (p.VariosDocumentos || string.IsNullOrEmpty(tipoOperacion)) return p;

            bool carga = tipoOperacion == "CARGA";
            p.MostrarFactura = carga;
            p.MostrarCpic = esInternacional || loteTieneCpic || (carga && perfilDocumentos == "CPIC");
            return p;
        }

        /// <summary>Se escribe la factura o la CPIC solo si su panel está a la vista y tiene número.</summary>
        public static bool Gestionar(bool panelVisible, string numero) => panelVisible && !string.IsNullOrWhiteSpace(numero);

        /// <summary>
        /// Qué hacer con un documento al guardar: nada si su panel está oculto; quitar si se marcó la casilla
        /// (gana aunque haya número); gestionar si tiene número; si no, nada.
        /// </summary>
        public static AccionDocumentoLote Accion(bool panelVisible, bool quitar, string numero)
        {
            if (!panelVisible) return AccionDocumentoLote.NADA;
            if (quitar) return AccionDocumentoLote.QUITAR;
            return string.IsNullOrWhiteSpace(numero) ? AccionDocumentoLote.NADA : AccionDocumentoLote.GESTIONAR;
        }

        /// <summary>
        /// Compara lo guardado (<paramref name="antes"/>) con lo que pide el formulario (<paramref name="input"/>) y
        /// devuelve solo lo que cambia (2026-11-17, bloque 4, tarea 12): guardar sin tocar nada no escribe, y cambiar
        /// el conductor de un despacho no reubica los demás. Un documento se gestiona si cambia su número, fecha, valor
        /// o moneda, si el lote aún no lo tiene o si algún despacho del lote no lo lleva (lo enlaza).
        /// </summary>
        public static CambiosEdicionLote CalcularCambios(InstantaneaLote antes, GuardarCambiosLoteInput input)
        {
            var c = new CambiosEdicionLote();
            if (antes == null || input == null) return c;
            var conductores = input.CambiosConductores ?? new Dictionary<int, int>();

            foreach (var d in antes.Despachos)
            {
                if (conductores.TryGetValue(d.IdDespacho, out int elegido) && elegido > 0 && d.IdConductor != elegido)
                    c.ConductoresCambiados[d.IdDespacho] = elegido;

                bool datoDistinto = d.FechaDespacho?.Date != input.FechaDespacho.Date
                    || (d.NumeroPedido ?? "").Trim() != (input.NumeroPedido ?? "").Trim()
                    || d.IdPlanta != input.IdPlanta
                    || (d.TipoOperacion ?? "").Trim() != (input.TipoOperacion ?? "").Trim()
                    || d.EsInternacional != input.EsInternacional;
                if (datoDistinto || c.ConductoresCambiados.ContainsKey(d.IdDespacho))
                    c.DespachosAActualizar.Add(d.IdDespacho);
            }

            // Otro usuario pudo asignar una 2.ª factura o CPIC mientras el formulario estaba abierto (el panel solo se
            // muestra con una): quitar desde aquí las quitaría todas.
            if (input.QuitarFactura && antes.Facturas.Count > 1)
                throw new ErrorNegocioException("El lote ahora tiene más de una factura: quítelas por despacho en Documentos del lote → Factura y CPIC por despacho.");
            if (input.QuitarCpic && antes.Cpics.Count > 1)
                throw new ErrorNegocioException("El lote ahora tiene más de una CPIC: quítelas por despacho en Documentos del lote → Factura y CPIC por despacho.");

            if (input.QuitarFactura)
                c.FacturasAQuitar = antes.Despachos.Where(d => d.IdFactura.HasValue).Select(d => d.IdFactura.Value)
                    .Distinct().OrderBy(x => x).ToList();
            else if (input.GestionarFactura)
                c.GestionarFactura = DocumentoDistinto(antes.Facturas, antes.Despachos.Select(d => d.IdFactura),
                    input.NumeroFactura, input.FechaEmisionFactura, input.ValorTotalFactura, input.MonedaFactura);

            if (input.QuitarCpic)
                c.CpicsAQuitar = antes.Despachos.Where(d => d.IdCPIC.HasValue).Select(d => d.IdCPIC.Value)
                    .Distinct().OrderBy(x => x).ToList();
            else if (input.GestionarCpic)
                c.GestionarCpic = DocumentoDistinto(antes.Cpics, antes.Despachos.Select(d => d.IdCPIC),
                    input.NumeroCPIC, input.FechaEmisionCPIC, input.ValorFlete, null);

            return c;
        }

        /// <summary>
        /// ¿Hay que escribir el documento? Sí si el lote no lleva exactamente uno, si algún despacho no lo lleva o si
        /// difiere el número, la fecha, el valor o la moneda (moneda null en el formulario = deja la que tenía; una
        /// factura sin moneda se muestra como PEN).
        /// </summary>
        private static bool DocumentoDistinto(List<InstantaneaDocumento> documentos, IEnumerable<int?> idsEnDespachos,
            string numero, DateTime? fecha, decimal? valor, string moneda)
        {
            if (documentos == null || documentos.Count != 1) return true;
            var doc = documentos[0];
            if (idsEnDespachos.Any(id => id != doc.Id)) return true;
            if (!string.Equals((doc.Numero ?? "").Trim(), (numero ?? "").Trim(), StringComparison.Ordinal)) return true;
            if (doc.Fecha?.Date != fecha?.Date) return true;
            if (doc.Valor != valor) return true;
            if (!string.IsNullOrEmpty(moneda)
                && !string.Equals((doc.Moneda ?? "PEN").Trim(), moneda.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Facturas y CPIC distintas de los despachos del lote (las facturas primero), con cuántos despachos llevan cada una.</summary>
        public static List<DocumentoDelLote> DocumentosDistintos(IEnumerable<DocumentoDespachoLote> despachos)
        {
            var lista = (despachos ?? Enumerable.Empty<DocumentoDespachoLote>()).ToList();
            var facturas = lista.Where(d => d.IdFactura.HasValue).GroupBy(d => d.IdFactura.Value)
                .Select(g => new DocumentoDelLote
                {
                    Tipo = DocumentoDelLote.FACTURA, Id = g.Key, Numero = g.First().NumeroFactura,
                    ConPdf = g.First().FacturaConPdf, Despachos = g.Count()
                });
            var cpics = lista.Where(d => d.IdCPIC.HasValue).GroupBy(d => d.IdCPIC.Value)
                .Select(g => new DocumentoDelLote
                {
                    Tipo = DocumentoDelLote.CPIC, Id = g.Key, Numero = g.First().NumeroCPIC,
                    ConPdf = g.First().CpicConPdf, Despachos = g.Count()
                });
            return facturas.OrderBy(d => d.Numero, StringComparer.OrdinalIgnoreCase)
                .Concat(cpics.OrderBy(d => d.Numero, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Los ids marcados que de verdad son del lote (las casillas llegan del navegador): descarta los ajenos y repetidos.
        /// </summary>
        public static List<int> MarcadosDelLote(IEnumerable<int> marcados, IEnumerable<int> idsDelLote)
        {
            var delLote = new HashSet<int>(idsDelLote ?? Enumerable.Empty<int>());
            return (marcados ?? Enumerable.Empty<int>()).Where(delLote.Contains).Distinct().OrderBy(x => x).ToList();
        }

        /// <summary>Interpreta "FACTURA:12" / "CPIC:34" (argumento de Ver/Subir PDF); false si no tiene ese formato.</summary>
        public static bool LeerClave(string clave, out string tipo, out int id)
        {
            tipo = null;
            id = 0;
            var partes = (clave ?? "").Split(':');
            if (partes.Length != 2 || !int.TryParse(partes[1], out id) || id <= 0) return false;
            if (partes[0] != DocumentoDelLote.FACTURA && partes[0] != DocumentoDelLote.CPIC) return false;
            tipo = partes[0];
            return true;
        }
    }
}
