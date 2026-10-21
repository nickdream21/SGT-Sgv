using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Services.Reportes;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Arma el Excel "STATUS GENERAL" desde el seguimiento: cada hito como columna de fecha más
    /// una columna HORA (como el Excel original), con los encabezados que reconoce la
    /// importación (ImportarSeguimiento), así que el archivo se puede volver a subir.
    /// </summary>
    public static class ExportacionStatusGeneral
    {
        private static readonly (string Columna, string Encabezado)[] Hitos =
        {
            ("fhSalidaBase1", "F.H.S.BASE:"),
            ("fhLlegadaTrujillo", "F.H.LL. TRUJILLO"),
            ("fhRegistro", "F.H.REGISTRO"),
            ("fhProgramacion", "F.H. PROGRAMACION"),
            ("fhIngresoPlanta", "F.H.I PLANTA"),
            ("fhInicioCarga", "F.H.INICIO DE CARGA"),
            ("fhTerminoCarga", "F.H.TERMINO CARGA"),
            ("fhSalidaPlanta", "F.H.S PLANTA"),
            ("fhLlegadaBase2", "F.H.LL. BASE"),
            ("fhSalidaBase2", "F.H.S BASE"),
            ("fhLlegadaBodegaNacional", "F.H.LL.BODEGA NACIONAL"),
            ("fhIngresoBodegaNacional", "F.H.I. BODEGA NACIONAL"),
            ("fhSalidaBodegaNacional", "F.H.S.BODEGA NACIONAL"),
            ("fhLlegadaCEBAF", "F.H.LL CEBAF"),
            ("fhCruceEcuador", "F.H CRUCE"),
            ("fhAutorizacionNacionalizacion", "AUTORIZACION DE LA NACIONALIZACION"),
            ("fhLlegadaTCI", "F.H.LL.TCI"),
            ("fhSalidaTCI", "F.H.S TCI"),
            ("fhLlegadaPlantaEcuador", "F.H.LL.PLANTA"),
            ("fhLlegadaAlmacen", "F.H.LL.ALMACEN"),
            ("fhIngreso", "F.H.INGRESO"),
            ("fhInicioDescarga", "F.H.I. DESCARGA"),
            ("fhTerminoDescarga", "F.H.T. DESCARGA"),
            ("fhSalida", "F.H.SALIDA"),
            ("fhLlegadaBaseFinal", "F.H.LL BASE"),
        };

        /// <param name="status">Texto de STATUS por idSeguimiento (última columna); sin él no se agrega la columna.</param>
        public static ReporteTabular Construir(IList<DataRow> filas, IEnumerable<string> filtros, string generadoPor, IDictionary<int, string> status = null)
        {
            var r = new ReporteTabular
            {
                Titulo = "STATUS GENERAL - Seguimiento de exportación",
                NombreHoja = "SEGUIMIENTO",
                Filas = filas,
                IncluirTotales = false,
                GeneradoPor = generadoPor,
            };
            r.Filtros.AddRange(filtros);

            void Texto(string titulo, string columna, float ancho = 1.2f) =>
                r.Columnas.Add(new ColumnaReporte { Titulo = titulo, Tipo = TipoColumnaReporte.Texto, Ancho = ancho, Valor = f => Valor(f, columna) });

            Texto("CLIENTE", "cliente");
            Texto("CONDUCTOR ORIGEN", "conductorOrigen", 1.8f);
            Texto("TRACTO 1", "tracto1");
            Texto("CARRETA", "carreta");
            Texto("CONDUCTOR DESTINO", "conductorDestino", 1.8f);
            Texto("TRACTO 2", "tracto2");
            foreach (var h in Hitos)
            {
                string columna = h.Columna;
                r.Columnas.Add(new ColumnaReporte { Titulo = h.Encabezado, Tipo = TipoColumnaReporte.Fecha, Ancho = 1.3f, Valor = f => Valor(f, columna) });
                r.Columnas.Add(new ColumnaReporte
                {
                    Titulo = "HORA", Tipo = TipoColumnaReporte.Codigo, Ancho = 0.9f,
                    Valor = f => Valor(f, columna) is DateTime d ? d.ToString("HH:mm:ss") : null
                });
            }
            Texto("BODEGA", "bodegaNacional");
            Texto("BODEGA ECUATORIANA", "bodegaEcuatoriana");
            Texto("BODEGA DESCARGA", "bodegaDescarga");
            Texto("MOTIVO DE RETRASO", "motivoRetraso", 2.5f);
            if (status != null)
                r.Columnas.Add(new ColumnaReporte
                {
                    Titulo = "STATUS", Tipo = TipoColumnaReporte.Texto, Ancho = 4f,
                    Valor = f => status.TryGetValue(Convert.ToInt32(f["idSeguimiento"]), out var s) ? s : null
                });
            return r;
        }

        private static object Valor(DataRow f, string columna) =>
            f.Table.Columns.Contains(columna) && !f.IsNull(columna) ? f[columna] : null;
    }
}
