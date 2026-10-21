using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Excel liviano para enviar el STATUS de las unidades (pedido o rango filtrado en el tablero), con
    /// las columnas del cuadro que arma la administradora: fecha, pedido, conductores, placas y el
    /// texto de STATUS con la hora de la ubicación. No reemplaza al STATUS GENERAL completo
    /// (<see cref="ExportacionStatusGeneral"/>), que lleva todas las horas.
    /// </summary>
    public static class ExportacionStatusUnidades
    {
        public static ReporteTabular Construir(IList<ViajeResumen> viajes, IEnumerable<string> filtros, string generadoPor)
        {
            var porId = viajes.ToDictionary(v => Convert.ToInt32(v.Fila["idSeguimiento"]));
            var r = new ReporteTabular
            {
                Titulo = "STATUS de unidades - Exportación",
                NombreHoja = "STATUS",
                Filas = viajes.Select(v => v.Fila).ToList(),
                IncluirTotales = false,
                GeneradoPor = generadoPor,
                Nota = "STATUS según el último punto del GPS; \"Ubicación al\" es la hora de ese punto.",
            };
            r.Filtros.AddRange(filtros);

            void Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<ViajeResumen, object> valor) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = f => valor(porId[Convert.ToInt32(f["idSeguimiento"])]) });

            Col("FECHA", TipoColumnaReporte.Codigo, 0.9f, v => v.FhProgramacion?.ToString("dd/MM/yy"));
            Col("NRO DE PEDIDO", TipoColumnaReporte.Codigo, 1.2f, v => v.Pedido);
            Col("CARRETA", TipoColumnaReporte.Codigo, 1f, v => v.Carreta);
            Col("CONDUCTOR DE CARGA", TipoColumnaReporte.Texto, 1.8f, v => v.Chofer1);
            Col("CONDUCTOR DE DESCARGA", TipoColumnaReporte.Texto, 1.8f, v => v.Chofer2);
            Col("PLACA TRACTO PARA LA CARGA", TipoColumnaReporte.Codigo, 1.1f, v => v.Tracto1);
            Col("PLACA TRACTO PARA EL CRUCE EN FRONTERA", TipoColumnaReporte.Codigo, 1.1f, v => v.Tracto2);
            Col("STATUS", TipoColumnaReporte.Texto, 4.5f, v => v.Status);
            Col("UBICACIÓN AL", TipoColumnaReporte.Codigo, 1.1f, v => v.UbicacionHora);
            return r;
        }
    }
}
