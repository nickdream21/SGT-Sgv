using System;
using System.Collections.Generic;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Qué documentos le faltan a un lote. El despacho se registra con el Word aunque la
    /// factura, las guías y el CPIC lleguen después (correo, escaneos): no se bloquea nada,
    /// solo se muestra lo pendiente y desde cuándo. Los conteos los hace
    /// <c>sp_LD_ObtenerLotesRegistrados</c> por despacho no anulado:
    /// <list type="bullet">
    /// <item>CARGA: pedido, guía de remitente y factura (tercero nacional sin pedido: nuestra GRE transportista, que cita la del remitente).</item>
    /// <item>Con factura: su PDF.</item>
    /// <item>Internacional: CPIC; con CPIC, su PDF; y su manifiesto (MCI).</item>
    /// </list>
    /// </summary>
    public sealed class EstadoDocumental
    {
        public int Despachos { get; set; }
        public int SinPedido { get; set; }
        public int SinGuia { get; set; }
        public int SinFactura { get; set; }
        public int SinPdfFactura { get; set; }
        public int SinCPIC { get; set; }
        public int SinPdfCPIC { get; set; }
        public int SinManifiesto { get; set; }

        /// <summary>PEDIDO, CPIC o GUIA (ver <see cref="LoteRegistrado.PerfilDocumentos"/>).</summary>
        public string Perfil { get; set; } = "PEDIDO";

        public static EstadoDocumental DeLote(LoteRegistrado lote) => new EstadoDocumental
        {
            Despachos = lote.DespachosVigentes,
            SinPedido = lote.DespachosSinPedido,
            SinGuia = lote.DespachosSinGuia,
            SinFactura = lote.DespachosSinFactura,
            SinPdfFactura = lote.DespachosSinPdfFactura,
            SinCPIC = lote.DespachosSinCPIC,
            SinPdfCPIC = lote.DespachosSinPdfCPIC,
            SinManifiesto = lote.DespachosSinManifiesto,
            Perfil = string.IsNullOrEmpty(lote.PerfilDocumentos) ? "PEDIDO" : lote.PerfilDocumentos
        };

        public bool Completo =>
            SinPedido + SinGuia + SinFactura + SinPdfFactura + SinCPIC + SinPdfCPIC + SinManifiesto == 0;

        /// <summary>
        /// Carga programada con el Word que todavía no recibió nada: espera el ZIP del correo
        /// (RegistroDespacho → "Leer documentos" la completa).
        /// </summary>
        public bool SinNingunDocumento =>
            Perfil == "PEDIDO" && Despachos > 0 && SinPedido == Despachos && SinGuia == Despachos && SinFactura == Despachos;

        /// <summary>Lo que falta, un texto por documento ("Falta guía (2 de 6)").</summary>
        public IReadOnlyList<string> Faltantes
        {
            get
            {
                var faltan = new List<string>();
                if (SinNingunDocumento)
                {
                    faltan.Add("Sin documentos (pedido, factura y guías)");
                }
                else
                {
                    Agregar(faltan, SinPedido, "Falta pedido");
                    Agregar(faltan, SinFactura, Perfil == "CPIC" ? "Falta factura comercial (de la CPIC)" : "Falta factura");
                    Agregar(faltan, SinGuia, Perfil == "GUIA" ? "Falta GRE transportista" : "Falta guía");
                }
                Agregar(faltan, SinPdfFactura, "Sin PDF de factura");
                Agregar(faltan, SinCPIC, "Falta CPIC");
                Agregar(faltan, SinPdfCPIC, "Sin PDF del CPIC");
                Agregar(faltan, SinManifiesto, "Falta manifiesto (MCI)");
                return faltan;
            }
        }

        public string Resumen => Completo ? "Completo" : string.Join(" · ", Faltantes);

        /// <summary>"hoy", "hace 1 día", "hace 5 días" desde la fecha del despacho; vacío si está completo o es futuro.</summary>
        public string Antiguedad(DateTime fechaDespacho, DateTime hoy)
        {
            if (Completo) return "";
            int dias = (hoy.Date - fechaDespacho.Date).Days;
            if (dias < 0) return "";
            if (dias == 0) return "hoy";
            return dias == 1 ? "hace 1 día" : $"hace {dias} días";
        }

        /// <summary>Más de <paramref name="diasTolerancia"/> días sin completar: se marca en rojo.</summary>
        public bool Atrasado(DateTime fechaDespacho, DateTime hoy, int diasTolerancia = 3) =>
            !Completo && (hoy.Date - fechaDespacho.Date).Days > diasTolerancia;

        private void Agregar(List<string> faltan, int cuantos, string texto)
        {
            if (cuantos <= 0) return;
            faltan.Add(cuantos >= Despachos ? texto : $"{texto} ({cuantos} de {Despachos})");
        }
    }
}
