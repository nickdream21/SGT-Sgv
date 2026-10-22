using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Excel STATUS EXTRA: el cuadro de los clientes terceros (Crisar, Novopan, Polinplast) que la
    /// administradora arma y envía, con sus mismas columnas (STATUS EXTRA VIVIANA.xlsx). Sin relevo,
    /// el conductor y el tracto de descarga son los de la carga, como en el cuadro.
    /// </summary>
    public static class ExportacionStatusExtra
    {
        public static ReporteTabular Construir(IList<ViajeResumen> viajes, IEnumerable<string> filtros, string generadoPor)
        {
            var terceros = viajes.Where(v => RutaTercero.Es(v.TipoRuta))
                .OrderBy(v => v.FhProgramacion ?? DateTime.MaxValue).ThenBy(v => v.Id).ToList();
            var porId = terceros.ToDictionary(v => v.Id);
            var r = new ReporteTabular
            {
                Titulo = "STATUS EXTRA - Clientes terceros",
                NombreHoja = "STATUS EXTRA",
                Filas = terceros.Select(v => v.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Nota = "STATUS según el último punto del GPS; \"Ubicación al\" es la hora de ese punto.",
            };
            r.Filtros.AddRange(filtros);

            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<ViajeResumen, object> valor) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]) });

            Col("FECHA CARGA", TipoColumnaReporte.Codigo, 0.9f, v => v.FhProgramacion?.ToString("dd/MM/yy"));
            Col("CONDUCTOR DE CARGA", TipoColumnaReporte.Texto, 1.8f, v => v.Chofer1);
            Col("CONDUCTOR DE DESCARGA", TipoColumnaReporte.Texto, 1.8f, v => v.Chofer2 ?? (v.Tracto2 == null || v.Tracto2 == v.Tracto1 ? v.Chofer1 : null));
            Col("PLACA TRACTOR CARGA", TipoColumnaReporte.Codigo, 1f, v => v.Tracto1);
            Col("PLACA TRACTOR DESCARGA", TipoColumnaReporte.Codigo, 1f, v => v.Tracto2 ?? v.Tracto1);
            Col("PLACA SEMIREMOLQUE", TipoColumnaReporte.Codigo, 1f, v => v.Carreta);
            Col("CLIENTE", TipoColumnaReporte.Codigo, 1.1f, v => v.TipoRuta);
            Col("LUGAR DE CARGA", TipoColumnaReporte.Codigo, 1.1f, v => v.LugarCarga);
            Col("LUGAR DE ENTREGA", TipoColumnaReporte.Codigo, 1.1f, v => v.LugarEntrega);
            Col("STATUS", TipoColumnaReporte.Texto, 4f, v => v.Status);
            Col("UBICACIÓN AL", TipoColumnaReporte.Codigo, 1.1f, v => v.UbicacionHora);
            return r;
        }
    }
}
