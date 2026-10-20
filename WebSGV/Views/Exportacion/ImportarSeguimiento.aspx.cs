using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Importación masiva del seguimiento de exportación desde Excel (STATUS GENERAL VIVIANA o la
    /// plantilla) y descarga de la plantilla. Antes estaba dentro de RegistroSeguimiento.
    /// </summary>
    public partial class ImportarSeguimiento : System.Web.UI.Page
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        // Contadores de la última importación masiva (se reinician en cada importación)
        private int _importInsertados   = 0;
        private int _importActualizados = 0;

        // ── Mapa Excel → Columna lógica (case-insensitive, busca por contiene)
        // Clave = nombre lógico interno; Valor = lista de fragmentos que aceptamos.
        private static readonly Dictionary<string, string[]> ExcelHeaderMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "cliente",                        new[] { "CLIENTE" } },
                { "conductorOrigen",                new[] { "CONDUCTOR ORIGEN" } },
                { "tracto1",                        new[] { "TRACTO 1", "TRACTO1" } },
                { "carreta",                        new[] { "CARRETA" } },
                { "conductorDestino",               new[] { "CONDUCTOR DESTINO" } },
                { "tracto2",                        new[] { "TRACTO 2", "TRACTO2", "TRAXTO 2", "TRAXTO2" } },
                { "fhSalidaBase1",                  new[] { "F.H.S.BASE:", "F.H.S.BASE." } },
                { "fhLlegadaTrujillo",              new[] { "F.H.LL. TRUJILLO", "F.H.LL.TRUJILLO" } },
                { "fhRegistro",                     new[] { "F.H.REGISTRO" } },
                { "fhProgramacion",                 new[] { "F.H. PROGRAMACION", "F.H.PROGRAMACION" } },
                { "fhIngresoPlanta",                new[] { "F.H.I PLANTA", "F.H.I. PLANTA" } },
                { "fhInicioCarga",                  new[] { "F.H.INICIO DE CARGA" } },
                { "fhTerminoCarga",                 new[] { "F.H.TERMINO CARGA" } },
                { "fhSalidaPlanta",                 new[] { "F.H.S PLANTA" } },
                { "fhLlegadaBase2",                 new[] { "F.H.LL. BASE", "F.H.LL.BASE" } },
                { "fhSalidaBase2",                  new[] { "F.H.S BASE" } },
                { "fhLlegadaBodegaNacional",        new[] { "F.H.LL.BODEGA NACIONAL", "F.H.LL BODEGA NACIONAL" } },
                { "fhIngresoBodegaNacional",        new[] { "F.H.I. BODEGA NACIONAL", "F.H.I BODEGA NACIONAL" } },
                { "fhSalidaBodegaNacional",         new[] { "F.H.S.BODEGA NACIONAL", "F.H.S BODEGA NACIONAL" } },
                { "bodegaNacional",                 new[] { "BODEGA" } },
                { "fhLlegadaCEBAF",                 new[] { "F.H.LL CEBAF" } },
                { "fhCruceEcuador",                 new[] { "F.H CRUCE" } },
                { "fhAutorizacionNacionalizacion",  new[] { "AUTORIZACION DE LA NACIONALIZACION" } },
                { "bodegaEcuatoriana",              new[] { "BODEGA ECUATORIANA" } },
                { "fhLlegadaTCI",                   new[] { "F.H.LL.TCI" } },
                { "fhSalidaTCI",                    new[] { "F.H.S TCI" } },
                { "bodegaDescarga",                 new[] { "BODEGA DESCARGA" } },
                { "fhLlegadaPlantaEcuador",         new[] { "F.H.LL.PLANTA" } },
                { "fhLlegadaAlmacen",               new[] { "F.H.LL.ALMACEN" } },
                { "fhIngreso",                      new[] { "F.H.INGRESO" } },
                { "fhInicioDescarga",               new[] { "F.H.I. DESCARGA", "F.H.I DESCARGA" } },
                { "fhTerminoDescarga",              new[] { "F.H.T. DESCARGA", "F.H.T DESCARGA" } },
                { "fhSalida",                       new[] { "F.H.SALIDA" } },
                // Llegada final a base (STATUS GENERAL, columna BF "F.H.LL Base"): distinta de
                // "F.H.LL. Base" (con punto), que es la llegada a base antes de salir a Ecuador.
                { "fhLlegadaBaseFinal",             new[] { "F.H.LL BASE" } },
                { "motivoRetraso",                  new[] { "MOTIVO DE RETRASO", "COMENTARIO" } }
            };

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("SEGUIMIENTO_EXPORTACION");
            SecurityHelper.AgregarHeadersSeguridad();
        }

        protected void btnImportar_Click(object sender, EventArgs e)
        {
            if (!fileExcel.HasFile)
            {
                MostrarAlerta("Seleccione primero un archivo Excel.", "warning");
                return;
            }

            string ext = Path.GetExtension(fileExcel.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
            {
                MostrarAlerta("Solo se aceptan archivos .xlsx o .xls", "warning");
                return;
            }
            if (fileExcel.PostedFile.ContentLength > 10 * 1024 * 1024)
            {
                MostrarAlerta("El archivo supera el límite de 10MB.", "warning");
                return;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ext);
            try
            {
                fileExcel.SaveAs(tempPath);
                int filas = ProcesarExcel(tempPath);

                string resumen = $"Importación finalizada: {_importInsertados} nuevo(s) · {_importActualizados} actualizado(s) · {filas} fila(s) leída(s).";
                if (_duplicadosEnArchivo > 0)
                    resumen += $" {_duplicadosEnArchivo} fila(s) repetían pedido + tracto + programación: se tomó la última.";
                if (_filasSinProgramacion > 0)
                    resumen += $" {_filasSinProgramacion} fila(s) sin F.H. PROGRAMACION válida no se importaron.";
                if (_fechasDeducidas > 0)
                    resumen += $" {_fechasDeducidas} hito(s) tenían hora sin fecha: la fecha se dedujo por el orden del viaje.";
                if (_rechazadas.Count > 0)
                    resumen += $" {_rechazadas.Count} celda(s) de fecha/hora no se pudieron leer (detalle abajo).";
                MostrarAlerta(resumen, _rechazadas.Count > 0 || _filasSinProgramacion > 0 ? "warning" : "success");

                pnlReporteImportacion.Visible = _rechazadas.Count > 0;
                litReporteImportacion.Text = _rechazadas.Count > 0 ? ReporteRechazadasHtml(40) : string.Empty;
            }
            catch (Exception ex)
            {
                MostrarAlerta("Error al procesar el archivo: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }
        }

        // ── Plantilla de importación: orden de columnas + header canónico.
        // El header usado es un fragmento reconocido por ExcelHeaderMap, de modo que
        // una plantilla llenada por el usuario se importa sin ajustes.
        private static readonly (string Header, string Grupo)[] PlantillaColumnas =
        {
            ("CLIENTE",                             "IDENTIDAD"),
            ("CONDUCTOR ORIGEN",                    "IDENTIDAD"),
            ("TRACTO 1",                            "IDENTIDAD"),
            ("CARRETA",                             "IDENTIDAD"),
            ("CONDUCTOR DESTINO",                   "IDENTIDAD"),
            ("TRACTO 2",                            "IDENTIDAD"),
            ("F.H.S.BASE:",                         "PERU"),
            ("F.H.LL. TRUJILLO",                    "PERU"),
            ("F.H.REGISTRO",                        "PERU"),
            ("F.H. PROGRAMACION",                   "PERU"),
            ("F.H.I PLANTA",                        "PERU"),
            ("F.H.INICIO DE CARGA",                 "PERU"),
            ("F.H.TERMINO CARGA",                   "PERU"),
            ("F.H.S PLANTA",                        "PERU"),
            ("F.H.LL. BASE",                        "PERU"),
            ("F.H.S BASE",                          "PERU"),
            ("F.H.LL.BODEGA NACIONAL",              "BODEGA NACIONAL"),
            ("F.H.I. BODEGA NACIONAL",              "BODEGA NACIONAL"),
            ("F.H.S.BODEGA NACIONAL",               "BODEGA NACIONAL"),
            ("BODEGA",                              "BODEGA NACIONAL"),
            ("F.H.LL CEBAF",                        "FRONTERA"),
            ("F.H CRUCE",                           "FRONTERA"),
            ("AUTORIZACION DE LA NACIONALIZACION",  "FRONTERA"),
            ("BODEGA ECUATORIANA",                  "ECUADOR"),
            ("F.H.LL.TCI",                          "ECUADOR"),
            ("F.H.S TCI",                           "ECUADOR"),
            ("BODEGA DESCARGA",                     "ECUADOR"),
            ("F.H.LL.PLANTA",                       "ECUADOR"),
            ("F.H.LL.ALMACEN",                      "ECUADOR"),
            ("F.H.INGRESO",                         "ECUADOR"),
            ("F.H.I. DESCARGA",                     "ECUADOR"),
            ("F.H.T. DESCARGA",                     "ECUADOR"),
            ("F.H.SALIDA",                          "ECUADOR"),
            ("MOTIVO DE RETRASO",                   "INCIDENCIAS")
        };

        protected void btnDescargarPlantilla_Click(object sender, EventArgs e)
        {
            try
            {
                using (var wb = ConstruirPlantilla())
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("Content-Disposition",
                        "attachment; filename=Plantilla_Seguimiento_Exportacion.xlsx");
                    Response.BinaryWrite(ms.ToArray());
                    Response.Flush();
                    Response.End();
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Response.End() lanza ThreadAbortException por diseño: se ignora.
            }
            catch (Exception ex)
            {
                MostrarAlerta("No se pudo generar la plantilla: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        /// <summary>
        /// Genera el libro de la plantilla de importación con encabezados reconocidos por
        /// el importador, anchos razonables, fila de encabezado congelada y listas de
        /// validación para las columnas de bodega.
        /// </summary>
        private static XLWorkbook ConstruirPlantilla()
        {
            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("SEGUIMIENTO");

            for (int i = 0; i < PlantillaColumnas.Length; i++)
            {
                int c = i + 1;
                var celda = ws.Cell(1, c);
                celda.Value = PlantillaColumnas[i].Header;
                celda.Style.Font.Bold = true;
                celda.Style.Font.FontColor = XLColor.White;
                celda.Style.Fill.BackgroundColor = ColorGrupo(PlantillaColumnas[i].Grupo);
                celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                celda.Style.Alignment.WrapText = true;
                ws.Column(c).Width = AnchoColumna(PlantillaColumnas[i].Header);
            }

            ws.Row(1).Height = 34;
            ws.SheetView.FreezeRows(1);

            // Listas de validación para las bodegas (filas 2..1000)
            AplicarValidacionLista(ws, "BODEGA",             "DEPSA,COMPLEX");
            AplicarValidacionLista(ws, "BODEGA ECUATORIANA", "TCI,PUYANGO");
            AplicarValidacionLista(ws, "BODEGA DESCARGA",    "INBALNOR,JAVE,OREMANS");

            // Comentario guía en la primera columna de fecha
            int colFecha = IndiceHeader("F.H.S.BASE:");
            if (colFecha > 0)
            {
                var cab = ws.Cell(1, colFecha);
                cab.CreateComment().AddText("Formato fecha y hora: dd/mm/aaaa hh:mm. Deja la celda vacía si el hito aún no ocurre.");
            }

            // Segunda hoja con instrucciones básicas
            var wsInfo = wb.Worksheets.Add("Instrucciones");
            wsInfo.Cell(1, 1).Value = "Cómo usar esta plantilla";
            wsInfo.Cell(1, 1).Style.Font.Bold = true;
            wsInfo.Cell(1, 1).Style.Font.FontSize = 14;
            string[] pasos =
            {
                "1) Completa una fila por cada viaje en la hoja SEGUIMIENTO.",
                "2) Campos minimos para registrar un viaje: CLIENTE, TRACTO 1 y F.H. PROGRAMACION.",
                "3) Las fechas van con formato dd/mm/aaaa hh:mm. Deja vacias las que aun no ocurren.",
                "4) Las columnas BODEGA, BODEGA ECUATORIANA y BODEGA DESCARGA tienen lista desplegable.",
                "5) No cambies los nombres de los encabezados: el sistema los reconoce automaticamente.",
                "6) Guarda el archivo y subelo en la pestana 'Importar Excel'."
            };
            for (int i = 0; i < pasos.Length; i++)
                wsInfo.Cell(i + 3, 1).Value = pasos[i];
            wsInfo.Column(1).Width = 90;

            return wb;
        }

        private static XLColor ColorGrupo(string grupo)
        {
            switch (grupo)
            {
                case "PERU":            return XLColor.FromHtml("#1E3A8A");
                case "BODEGA NACIONAL": return XLColor.FromHtml("#92400E");
                case "FRONTERA":        return XLColor.FromHtml("#6B21A8");
                case "ECUADOR":         return XLColor.FromHtml("#065F46");
                case "INCIDENCIAS":     return XLColor.FromHtml("#991B1B");
                default:                return XLColor.FromHtml("#0B1426");
            }
        }

        private static double AnchoColumna(string header)
        {
            if (header.StartsWith("F.H", StringComparison.OrdinalIgnoreCase)) return 20;
            if (header.Contains("MOTIVO")) return 30;
            if (header.Contains("BODEGA") || header.Contains("CONDUCTOR")) return 22;
            return 16;
        }

        private static int IndiceHeader(string header)
        {
            for (int i = 0; i < PlantillaColumnas.Length; i++)
                if (PlantillaColumnas[i].Header == header) return i + 1;
            return 0;
        }

        private static void AplicarValidacionLista(IXLWorksheet ws, string header, string listaCsv)
        {
            int col = IndiceHeader(header);
            if (col == 0) return;
            var rango = ws.Range(ws.Cell(2, col), ws.Cell(1000, col));
            var val = rango.CreateDataValidation();
            val.List("\"" + listaCsv + "\"", true);
            val.IgnoreBlanks = true;
        }

        /// <summary>Hitos "F.H." en el orden de la plantilla (claves de ExcelHeaderMap).</summary>
        private static readonly string[] ClavesFechaHora =
        {
            "fhSalidaBase1","fhLlegadaTrujillo","fhRegistro","fhProgramacion",
            "fhIngresoPlanta","fhInicioCarga","fhTerminoCarga","fhSalidaPlanta",
            "fhLlegadaBase2","fhSalidaBase2",
            "fhLlegadaBodegaNacional","fhIngresoBodegaNacional","fhSalidaBodegaNacional",
            "fhLlegadaCEBAF","fhCruceEcuador","fhAutorizacionNacionalizacion",
            "fhLlegadaTCI","fhSalidaTCI",
            "fhLlegadaPlantaEcuador","fhLlegadaAlmacen","fhIngreso",
            "fhInicioDescarga","fhTerminoDescarga","fhSalida","fhLlegadaBaseFinal"
        };

        /// <summary>Celda que no se pudo leer en la importación (se informa al usuario).</summary>
        private sealed class CeldaRechazada
        {
            public int Fila;
            public string Columna;
            public string Motivo;
        }

        /// <summary>
        /// Hitos en el orden real del viaje (Trujillo → base → bodega nacional → frontera → Ecuador
        /// → regreso). Se usa para deducir la fecha de un hito que solo tiene hora. No incluye
        /// F.H. Registro, F.H. Programación ni la autorización de nacionalización, que no siguen
        /// ese orden.
        /// </summary>
        private static readonly string[] SecuenciaViaje =
        {
            "fhSalidaBase1","fhLlegadaTrujillo","fhIngresoPlanta","fhInicioCarga","fhTerminoCarga","fhSalidaPlanta",
            "fhLlegadaBase2","fhSalidaBase2",
            "fhLlegadaBodegaNacional","fhIngresoBodegaNacional","fhSalidaBodegaNacional",
            "fhLlegadaCEBAF","fhCruceEcuador","fhLlegadaTCI","fhSalidaTCI",
            "fhLlegadaPlantaEcuador","fhLlegadaAlmacen","fhIngreso","fhInicioDescarga","fhTerminoDescarga",
            "fhSalida","fhLlegadaBaseFinal"
        };

        private readonly List<CeldaRechazada> _rechazadas = new List<CeldaRechazada>();
        private int _filasSinProgramacion;
        private int _duplicadosEnArchivo;
        private int _fechasDeducidas;

        /// <summary>
        /// Lee el Excel (STATUS GENERAL VIVIANA o la plantilla), valida fecha y hora de cada hito
        /// con <see cref="FechaHoraExcel"/> y envía todas las filas en UNA llamada a
        /// sp_SE_ImportarLote (tipo tabla). Devuelve las filas enviadas.
        /// Una celda ilegible deja ese hito vacío y se informa; una fila sin F.H. PROGRAMACION
        /// válida no se importa (es la base del mes en el dashboard y parte de la llave).
        /// </summary>
        private int ProcesarExcel(string path)
        {
            _rechazadas.Clear();
            _filasSinProgramacion = 0;
            _duplicadosEnArchivo = 0;
            _fechasDeducidas = 0;

            using (var workbook = new XLWorkbook(path))
            {
                if (!workbook.Worksheets.Any())
                    throw new ErrorNegocioException("El archivo Excel no contiene hojas.");

                IXLWorksheet ws = workbook.Worksheets
                                      .FirstOrDefault(w => (w.Name ?? "").ToUpper().Contains("SEGUIMIENTO"))
                                  ?? workbook.Worksheets.First();

                var ultimaCelda = ws.LastCellUsed();
                if (ultimaCelda == null)
                    throw new ErrorNegocioException("La hoja seleccionada está vacía.");

                int rows = ultimaCelda.Address.RowNumber;
                int cols = ultimaCelda.Address.ColumnNumber;

                // Detectar fila de headers (puede estar en fila 1 o más abajo si hay título)
                int headerRow = DetectarFilaHeaders(ws, rows, cols);
                if (headerRow == -1)
                    throw new ErrorNegocioException("No se pudo identificar la fila de encabezados (se busca CLIENTE / CONDUCTOR).");

                // Mapear cada columna lógica → índice de columna en Excel.
                // Para evitar colisiones (ej: "BODEGA" coincidiendo con "BODEGA ECUATORIANA"),
                // se prueba primero el match EXACTO; si no, se hace contains priorizando los headers más largos.
                var colMap = new Dictionary<string, int>();
                var headersExcel = new Dictionary<int, string>();
                for (int c = 1; c <= cols; c++)
                {
                    string h = ws.Cell(headerRow, c).GetString().Trim();
                    if (h.Length > 0) headersExcel[c] = NormalizarHeader(h);
                }

                // 1) Match exacto (case-insensitive, normalizando espacios)
                foreach (var kv in ExcelHeaderMap)
                {
                    foreach (var frag in kv.Value)
                    {
                        string fragU = NormalizarHeader(frag);
                        var match = headersExcel.FirstOrDefault(h =>
                            h.Value == fragU && !colMap.ContainsValue(h.Key));
                        if (match.Key != 0)
                        {
                            colMap[kv.Key] = match.Key;
                            break;
                        }
                    }
                }

                // 2) Match por contains, ordenando los fragments más largos primero (más específicos)
                var pendientes = ExcelHeaderMap
                    .Where(kv => !colMap.ContainsKey(kv.Key))
                    .SelectMany(kv => kv.Value.Select(f => new { Key = kv.Key, Frag = NormalizarHeader(f) }))
                    .OrderByDescending(x => x.Frag.Length);

                foreach (var p in pendientes)
                {
                    if (colMap.ContainsKey(p.Key)) continue;
                    var match = headersExcel.FirstOrDefault(h =>
                        !colMap.ContainsValue(h.Key) && h.Value.Contains(p.Frag));
                    if (match.Key != 0)
                    {
                        colMap[p.Key] = match.Key;
                    }
                }

                if (!colMap.ContainsKey("cliente") && !colMap.ContainsKey("conductorOrigen"))
                    throw new ErrorNegocioException("El archivo no contiene columnas reconocibles (CLIENTE / CONDUCTOR ORIGEN).");
                if (!colMap.ContainsKey("fhProgramacion"))
                    throw new ErrorNegocioException("El archivo no tiene la columna F.H. PROGRAMACION (es obligatoria).");

                // Columna de HORA: el STATUS GENERAL guarda cada hito como par [fecha][hora], con la
                // hora en la columna siguiente sin encabezado (o con "HORA"). La plantilla nueva usa
                // una sola celda con fecha y hora: ahí la columna siguiente es otro hito y no se toma.
                var horaColMap = new Dictionary<string, int>();
                foreach (var kv in colMap)
                {
                    if (!kv.Key.StartsWith("fh", StringComparison.OrdinalIgnoreCase)) continue;
                    int next = kv.Value + 1;
                    if (next > cols || colMap.ContainsValue(next)) continue;
                    string nextHeader = NormalizarHeader(ws.Cell(headerRow, next).GetString());
                    if (nextHeader.Length == 0 || nextHeader.Contains("HORA"))
                        horaColMap[kv.Key] = next;
                }

                // Nombre visible de cada columna para el reporte (letra + encabezado del Excel)
                string NombreColumna(string key) =>
                    colMap.TryGetValue(key, out int c)
                        ? $"{ws.Cell(headerRow, c).Address.ColumnLetter} ({ws.Cell(headerRow, c).GetString().Trim()})"
                        : key;

                var tabla = CrearTablaImportacion();

                for (int r = headerRow + 1; r <= rows; r++)
                {
                    // Si la fila está totalmente vacía en las columnas mapeadas, saltar
                    if (FilaVacia(ws, r, colMap)) continue;

                    var fechasFila = new Dictionary<string, DateTime?>();
                    var horasSinFecha = new Dictionary<string, TimeSpan>();
                    foreach (var key in ClavesFechaHora)
                    {
                        if (!colMap.ContainsKey(key)) { fechasFila[key] = null; continue; }
                        object celdaHora = horaColMap.ContainsKey(key) ? ValorCelda(ws.Cell(r, horaColMap[key])) : null;
                        var leido = FechaHoraExcel.Combinar(LeerCelda(ws, r, colMap, key), celdaHora);
                        fechasFila[key] = leido.Valor;
                        if (leido.HoraSinFecha.HasValue)
                            horasSinFecha[key] = leido.HoraSinFecha.Value;   // se intenta deducir abajo
                        else if (leido.Error != null)
                            _rechazadas.Add(new CeldaRechazada { Fila = r, Columna = NombreColumna(key), Motivo = leido.Error });
                    }

                    // Hitos con hora pero sin fecha: deducir la fecha por el orden del viaje
                    // (entre el hito anterior y el siguiente). Si no es única, se informa.
                    for (int i = 0; i < SecuenciaViaje.Length; i++)
                    {
                        string key = SecuenciaViaje[i];
                        if (!horasSinFecha.TryGetValue(key, out TimeSpan hora)) continue;

                        DateTime? anterior = SecuenciaViaje.Take(i).Reverse()
                            .Select(k => fechasFila.TryGetValue(k, out var v) ? v : null).FirstOrDefault(v => v.HasValue);
                        DateTime? siguiente = SecuenciaViaje.Skip(i + 1)
                            .Select(k => fechasFila.TryGetValue(k, out var v) ? v : null).FirstOrDefault(v => v.HasValue);

                        DateTime? deducida = FechaHoraExcel.DeducirFecha(hora, anterior, siguiente);
                        if (deducida.HasValue)
                        {
                            fechasFila[key] = deducida;
                            _fechasDeducidas++;
                        }
                        else
                        {
                            _rechazadas.Add(new CeldaRechazada { Fila = r, Columna = NombreColumna(key), Motivo = "hay hora pero falta la fecha" });
                        }
                    }
                    // Los que no están en la secuencia (Registro, Programación, Autorización) no se deducen.
                    foreach (var key in horasSinFecha.Keys.Where(k => !SecuenciaViaje.Contains(k)))
                        _rechazadas.Add(new CeldaRechazada { Fila = r, Columna = NombreColumna(key), Motivo = "hay hora pero falta la fecha" });

                    // Sin F.H. PROGRAMACION válida la fila no se puede ubicar en un mes ni identificar.
                    if (!fechasFila["fhProgramacion"].HasValue)
                    {
                        _filasSinProgramacion++;
                        continue;
                    }

                    var fila = tabla.NewRow();
                    fila["fila"] = r;
                    fila["cliente"]          = Texto(LeerTexto(ws, r, colMap, "cliente"), 150);
                    fila["conductorOrigen"]  = Texto(LeerTexto(ws, r, colMap, "conductorOrigen"), 150);
                    fila["tracto1"]          = Texto(LeerTexto(ws, r, colMap, "tracto1"), 20);
                    fila["carreta"]          = Texto(LeerTexto(ws, r, colMap, "carreta"), 20);
                    fila["conductorDestino"] = Texto(LeerTexto(ws, r, colMap, "conductorDestino"), 150);
                    fila["tracto2"]          = Texto(LeerTexto(ws, r, colMap, "tracto2"), 20);
                    foreach (var key in ClavesFechaHora)
                        fila[key] = (object)fechasFila[key] ?? DBNull.Value;
                    fila["bodegaNacional"]    = Texto(LeerTexto(ws, r, colMap, "bodegaNacional"), 150);
                    fila["bodegaEcuatoriana"] = Texto(LeerTexto(ws, r, colMap, "bodegaEcuatoriana"), 150);
                    fila["bodegaDescarga"]    = Texto(LeerTexto(ws, r, colMap, "bodegaDescarga"), 150);
                    fila["motivoRetraso"]     = Texto(LeerTexto(ws, r, colMap, "motivoRetraso"), 1000);
                    fila["estado"]            = DerivarEstado(fechasFila);
                    tabla.Rows.Add(fila);
                }

                if (tabla.Rows.Count > 0)
                {
                    using (var conn = new SqlConnection(ConnStr))
                    using (var cmd = new SqlCommand("sp_SE_ImportarLote", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 300;
                        var p = cmd.Parameters.Add("@filas", SqlDbType.Structured);
                        p.TypeName = "dbo.TipoSeguimientoImportacion";
                        p.Value = tabla;
                        cmd.Parameters.Add("@idUsuario", SqlDbType.Int).Value = (object)ObtenerIdUsuarioSesion() ?? DBNull.Value;

                        conn.Open();
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                _importInsertados   = Convert.ToInt32(rd["insertados"]);
                                _importActualizados = Convert.ToInt32(rd["actualizados"]);
                                _duplicadosEnArchivo = Convert.ToInt32(rd["duplicadosEnArchivo"]);
                            }
                        }
                    }
                }

                try
                {
                    AuditoriaHelper.Registrar(
                        accion: "IMPORT",
                        tablaAfectada: "SeguimientoExportacion",
                        idRegistroAfectado: "BULK",
                        descripcion: $"Importación Excel: {_importInsertados} nuevos, {_importActualizados} actualizados, " +
                                     $"{_filasSinProgramacion} sin F.H. PROGRAMACION, {_rechazadas.Count} celdas ilegibles.");
                }
                catch { /* No bloquear */ }

                return tabla.Rows.Count;
            }
        }

        /// <summary>Estructura idéntica a dbo.TipoSeguimientoImportacion (mismo orden de columnas).</summary>
        private static DataTable CrearTablaImportacion()
        {
            var t = new DataTable();
            t.Columns.Add("fila", typeof(int));
            foreach (var c in new[] { "cliente", "conductorOrigen", "tracto1", "carreta", "conductorDestino", "tracto2" })
                t.Columns.Add(c, typeof(string));
            foreach (var c in new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhRegistro", "fhProgramacion",
                                      "fhIngresoPlanta", "fhInicioCarga", "fhTerminoCarga", "fhSalidaPlanta",
                                      "fhLlegadaBase2", "fhSalidaBase2",
                                      "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaNacional", typeof(string));
            foreach (var c in new[] { "fhLlegadaCEBAF", "fhCruceEcuador", "fhAutorizacionNacionalizacion" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaEcuatoriana", typeof(string));
            foreach (var c in new[] { "fhLlegadaTCI", "fhSalidaTCI" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaDescarga", typeof(string));
            foreach (var c in new[] { "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso",
                                      "fhInicioDescarga", "fhTerminoDescarga", "fhSalida", "fhLlegadaBaseFinal" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("motivoRetraso", typeof(string));
            t.Columns.Add("estado", typeof(string));
            return t;
        }

        /// <summary>Texto recortado al largo de la columna; vacío o "-" se guarda como NULL.</summary>
        private static object Texto(string valor, int largo)
        {
            if (FechaHoraExcel.EsVacio(valor)) return DBNull.Value;
            string t = valor.Trim();
            return t.Length > largo ? t.Substring(0, largo) : t;
        }

        /// <summary>HTML (codificado) con las celdas rechazadas de la última importación.</summary>
        /// <summary>
        /// Agrupa por columna y motivo (con cantidad, filas del Excel y valores de ejemplo):
        /// en un STATUS GENERAL completo puede haber cientos de celdas y una lista plana no se lee.
        /// </summary>
        private string ReporteRechazadasHtml(int maxFilasPorGrupo)
        {
            var rxValor = new System.Text.RegularExpressions.Regex(@"\s*\((.*)\)\s*$");
            var grupos = _rechazadas
                .Select(c =>
                {
                    var m = rxValor.Match(c.Motivo);
                    return new
                    {
                        c.Fila,
                        c.Columna,
                        Motivo = m.Success ? c.Motivo.Substring(0, m.Index) : c.Motivo,
                        Valor = m.Success ? m.Groups[1].Value : null
                    };
                })
                .GroupBy(x => new { x.Columna, x.Motivo })
                .OrderByDescending(g => g.Count());

            var sb = new System.Text.StringBuilder();
            sb.Append("<div class=\"sx-tabla-caja\"><table class=\"table sgv-tabla\"><thead><tr><th>Columna</th><th>Motivo</th><th>Celdas</th>")
              .Append("<th>Filas del Excel</th><th>Ejemplos</th></tr></thead><tbody>");
            foreach (var g in grupos)
            {
                var filas = g.Select(x => x.Fila).Distinct().OrderBy(f => f).ToList();
                string listaFilas = string.Join(", ", filas.Take(maxFilasPorGrupo)) +
                                    (filas.Count > maxFilasPorGrupo ? $" … (+{filas.Count - maxFilasPorGrupo})" : "");
                string ejemplos = string.Join("  ·  ", g.Where(x => x.Valor != null).Select(x => x.Valor).Distinct().Take(5));

                sb.Append("<tr><td>").Append(System.Web.HttpUtility.HtmlEncode(g.Key.Columna))
                  .Append("</td><td>").Append(System.Web.HttpUtility.HtmlEncode(g.Key.Motivo))
                  .Append("</td><td>").Append(g.Count())
                  .Append("</td><td>").Append(System.Web.HttpUtility.HtmlEncode(listaFilas))
                  .Append("</td><td>").Append(System.Web.HttpUtility.HtmlEncode(ejemplos))
                  .Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static int DetectarFilaHeaders(IXLWorksheet ws, int rows, int cols)
        {
            int maxFila = Math.Min(15, rows);
            for (int r = 1; r <= maxFila; r++)
            {
                int hits = 0;
                for (int c = 1; c <= cols; c++)
                {
                    string val = ws.Cell(r, c).GetString().Trim().ToUpper();
                    if (val.Contains("CLIENTE") || val.Contains("CONDUCTOR") || val.Contains("TRACTO") || val.Contains("CARRETA"))
                        hits++;
                    if (hits >= 2) return r;
                }
            }
            return -1;
        }

        private static bool FilaVacia(IXLWorksheet ws, int row, Dictionary<string, int> colMap)
        {
            foreach (var kv in colMap)
            {
                var v = ValorCelda(ws.Cell(row, kv.Value));
                if (v != null && !string.IsNullOrWhiteSpace(v.ToString())) return false;
            }
            return true;
        }

        private static string LeerTexto(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.ContainsKey(key)) return null;
            object v = ValorCelda(ws.Cell(row, colMap[key]));
            return v?.ToString()?.Trim();
        }

        private static object LeerCelda(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.ContainsKey(key)) return null;
            return ValorCelda(ws.Cell(row, colMap[key]));
        }

        // Convierte una celda ClosedXML al mismo tipo de objeto que devolvía EPPlus
        // (null, double, string, DateTime, TimeSpan o bool) para no alterar la lógica posterior.
        private static object ValorCelda(IXLCell cell)
        {
            var v = cell.Value;
            switch (v.Type)
            {
                case XLDataType.Boolean: return v.GetBoolean();
                case XLDataType.Number: return v.GetNumber();
                case XLDataType.Text: return v.GetText();
                case XLDataType.DateTime: return v.GetDateTime();
                case XLDataType.TimeSpan: return v.GetTimeSpan();
                default: return null; // Blank o Error
            }
        }

        /// <summary>
        /// Normaliza un encabezado para comparación robusta:
        /// MAYÚSCULAS + colapsa espacios múltiples a 1 + trim.
        /// </summary>
        private static string NormalizarHeader(string h)
        {
            if (string.IsNullOrEmpty(h)) return "";
            string up = h.ToUpperInvariant().Trim();
            // Colapsar espacios múltiples (incluye tabs / nbsp)
            return System.Text.RegularExpressions.Regex.Replace(up, @"\s+", " ");
        }

        // ============================================================
        //  Helpers
        // ============================================================
        private void MostrarAlerta(string mensaje, string tipo)
        {
            string clase;
            switch ((tipo ?? "info").ToLower())
            {
                case "success": clase = "sx-mensaje sx-mensaje-ok"; break;
                case "warning": clase = "sx-mensaje sx-mensaje-alerta"; break;
                case "danger":  clase = "sx-mensaje sx-mensaje-peligro"; break;
                default:        clase = "sx-mensaje sx-mensaje-info"; break;
            }
            litMensaje.Text = $"<div class=\"{clase}\" role=\"status\">{System.Web.HttpUtility.HtmlEncode(mensaje)}</div>";
            pnlMensaje.Visible = true;
        }

        private static int? ObtenerIdUsuarioSesion()
        {
            int id = SecurityHelper.ObtenerIdUsuario();
            return id > 0 ? id : (int?)null;
        }

        /// <summary>
        /// Determina el estado de un registro importado en función de las fechas presentes:
        ///   - COMPLETADO: hay fecha de término de descarga o salida del destino final.
        ///   - EN_CURSO  : hay fechas intermedias pero no la final.
        ///   - PENDIENTE : no hay fechas significativas registradas.
        /// </summary>
        private static string DerivarEstado(Dictionary<string, DateTime?> fechas)
        {
            DateTime? Get(string k) => fechas.TryGetValue(k, out var v) ? v : null;

            // Indicadores de viaje finalizado
            if (Get("fhTerminoDescarga").HasValue || Get("fhSalida").HasValue)
                return "COMPLETADO";

            // Cualquier hito intermedio significa que el viaje arrancó
            string[] enCurso = {
                "fhSalidaBase1","fhLlegadaTrujillo","fhIngresoPlanta","fhInicioCarga","fhTerminoCarga",
                "fhSalidaPlanta","fhLlegadaBase2","fhSalidaBase2","fhLlegadaBodegaNacional",
                "fhIngresoBodegaNacional","fhSalidaBodegaNacional","fhLlegadaCEBAF","fhCruceEcuador",
                "fhAutorizacionNacionalizacion","fhLlegadaTCI","fhSalidaTCI","fhLlegadaPlantaEcuador",
                "fhLlegadaAlmacen","fhIngreso","fhInicioDescarga"
            };
            if (enCurso.Any(k => Get(k).HasValue))
                return "EN_CURSO";

            return "PENDIENTE";
        }
    }
}
