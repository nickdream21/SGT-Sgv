using System;

namespace WebSGV.Models.Despachos
{
    /// <summary>Fila de la tabla <c>DocumentosManifiesto</c> (ver ManifiestoService).</summary>
    public class DocumentoManifiesto
    {
        public int IdDocumentoManifiesto { get; set; }
        public int IdDespacho { get; set; }
        public string TipoManifiesto { get; set; } // CRUCE | RETORNO
        public string NombreOriginal { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaArchivo { get; set; }
        public string TipoArchivo { get; set; }
        public long TamanoBytes { get; set; }
        public DateTime FechaSubida { get; set; }
        public string UsuarioSubida { get; set; }
    }

    /// <summary>
    /// Fila de la grilla "Manifiesto por Conductor" en ListaDespachos.aspx: un despacho del
    /// lote con el documento de cruce/retorno más reciente (si existe).
    /// </summary>
    public class ManifiestoDespachoRow
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public string NombreConductor { get; set; }

        public int? CruceIdDocumento { get; set; }
        public string CruceNombreOriginal { get; set; }

        public int? RetornoIdDocumento { get; set; }
        public string RetornoNombreOriginal { get; set; }
    }
}
