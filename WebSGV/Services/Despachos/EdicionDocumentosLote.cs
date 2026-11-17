using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;

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
