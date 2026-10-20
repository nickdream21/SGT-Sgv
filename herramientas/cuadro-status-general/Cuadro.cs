using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.GpsIntegracion;

// Arma el cuadro STATUS GENERAL de un mes: viajes de Vitapro desde las programaciones en Word
// (carga en Trujillo + descarga en Guayaquil unidas por la carreta) y horas del GPS de Onway con
// la misma lógica que "Actualizar GPS" del seguimiento (TrazabilidadCarga/DescargaService).
// Sin --bd solo lee (la BD se usa para el token cacheado de Onway, OnwayAuthCache); con --bd crea los
// viajes que falten en el seguimiento de sgvActualizada. Ver README.md.
//
// Cuadro <raizRepo> <carpetaCache> <salida.xlsx> --mes=2026-10 [--bd=prueba|aplicar] <carpetaWords>...
class Cuadro
{
    static DateTime mes;                 // primer día del mes del cuadro
    static DateTime ultimaProgramacion;  // fecha del último Word leído
    class Carga { public DateTime FechaWord, FechaCarga; public FilaProgramacion Fila; public string Archivo; }
    class Descarga { public DateTime FechaWord; public string Pedido; public FilaProgramacion Fila; public string Archivo; public bool Usada; }
    class Viaje
    {
        public Carga C; public Descarga D;
        public ResultadoTramoCarga RC; public ResultadoTramoDescarga RD;
        public List<string> Notas = new List<string>();
    }

    static string cache;
    static OnwayApiClient cliente;
    static readonly Dictionary<string, OnwayDevice> dispositivos = new Dictionary<string, OnwayDevice>();
    static readonly Dictionary<string, List<OnwayHistoryPoint>> memoria = new Dictionary<string, List<OnwayHistoryPoint>>();
    static int llamadas;

    static void Main(string[] a)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string raiz = a[0]; cache = a[1]; string salida = a[2];
        Directory.CreateDirectory(cache);

        // La cadena de conexión se lee del archivo del repo (como las pruebas de integración), sin copiarla.
        var cs = XDocument.Load(Path.Combine(raiz, "WebSGV", "connectionStrings.config")).Descendants("add")
            .First(e => (string)e.Attribute("name") == "ConexionSGV");
        DbHelper.FijarConexionParaPruebas((string)cs.Attribute("connectionString"));
        cliente = new OnwayApiClient();

        // 1) Programaciones
        var cargas = new List<Carga>();
        var descargas = new List<Descarga>();
        string modoBd = a.Where(x => x.StartsWith("--bd=")).Select(x => x.Substring(5)).FirstOrDefault(); // prueba | aplicar
        string textoMes = a.Where(x => x.StartsWith("--mes=")).Select(x => x.Substring(6)).FirstOrDefault();
        if (textoMes == null || !DateTime.TryParseExact(textoMes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out mes))
        {
            Console.WriteLine("Falta --mes=AAAA-MM (el mes de carga del cuadro).");
            Environment.Exit(2);
        }
        foreach (var carpeta in a.Skip(3).Where(x => !x.StartsWith("--")))
            foreach (var f in Directory.GetFiles(carpeta, "*.docx").OrderBy(x => x))
            {
                var p = LectorProgramacionWord.Leer(File.ReadAllBytes(f));
                if (!p.Fecha.HasValue) { Console.WriteLine("SIN FECHA: " + f); continue; }
                if (p.Fecha.Value > ultimaProgramacion) ultimaProgramacion = p.Fecha.Value;
                string tituloVigente = "";
                foreach (var s in p.Secciones)
                {
                    string titulo = string.IsNullOrWhiteSpace(s.Titulo) ? tituloVigente : s.Titulo.Trim().ToUpperInvariant();
                    tituloVigente = titulo;
                    string t = SinTildes(titulo);
                    if (t.StartsWith("TRUJILLO-VITAPRO"))
                    {
                        DateTime fc = FechaCargaDelTitulo(t, p.Fecha.Value);
                        foreach (var r in s.Filas)
                            cargas.Add(new Carga { FechaWord = p.Fecha.Value, FechaCarga = fc, Fila = r, Archivo = Path.GetFileName(f) });
                    }
                    else if (t.StartsWith("GUAYAQUIL-VITAPRO"))
                    {
                        foreach (var r in s.Filas)
                            descargas.Add(new Descarga { FechaWord = p.Fecha.Value, Pedido = s.NumeroPedido, Fila = r, Archivo = Path.GetFileName(f) });
                    }
                }
            }

        var delMes = cargas.Where(c => c.FechaCarga >= mes && c.FechaCarga < mes.AddMonths(1))
            .OrderBy(c => c.FechaCarga).ThenBy(c => Hora(c.Fila.Horario) ?? TimeSpan.MaxValue).ToList();
        Console.WriteLine($"Cargas de Vitapro de {mes:MM/yyyy}: {delMes.Count}; filas de descarga leídas: {descargas.Count}");

        // 2) Unir cada carga con su descarga por la carreta: la primera programación de Guayaquil
        //    del mismo día de la carga o de los 6 siguientes.
        // Se recorre desde la descarga: la CAMBIO dice qué chofer trajo la carreta (o, vacía, que
        // es el mismo tracto), y eso elige la carga cuando la carreta aparece en dos cargas seguidas
        // (T0Q-991: AVO-818 el 09/10 y ANU-794 el 10/10; la descarga del 11/10 dice NOLBERTO SUNCIÓN).
        var viajes = delMes.Select(c => new Viaje { C = c }).ToList();
        var todas = cargas.Select(c => viajes.FirstOrDefault(v => v.C == c) ?? new Viaje { C = c }).ToList();
        foreach (var d in descargas.OrderBy(x => x.FechaWord))
        {
            string carreta = Placa(d.Fila.Carreta);
            string cambio = SinTildes(d.Fila.Cambio ?? "").Trim();
            var candidatas = todas.Where(v => v.D == null && Placa(v.C.Fila.Carreta) == carreta
                                              && v.C.FechaCarga <= d.FechaWord && v.C.FechaCarga >= d.FechaWord.AddDays(-6))
                                  .OrderByDescending(v => v.C.FechaCarga).ToList();
            if (candidatas.Count == 0) continue;
            var elegida = cambio.Length > 0
                ? candidatas.FirstOrDefault(v => MismoNombre(cambio, v.C.Fila.Chofer))
                : candidatas.FirstOrDefault(v => Placa(v.C.Fila.Tracto) == Placa(d.Fila.Tracto));
            if (elegida == null)
            {
                elegida = candidatas[0];
                elegida.Notas.Add(cambio.Length > 0
                    ? $"Word: CAMBIO dice {d.Fila.Cambio}, cargó {elegida.C.Fila.Chofer}."
                    : $"Word: la descarga no indica CAMBIO y el tracto es otro ({d.Fila.Tracto}).");
            }
            elegida.D = d;
        }
        foreach (var v in viajes.Where(v => v.D == null))
            v.Notas.Add($"Sin descarga programada en los Word hasta el {ultimaProgramacion:dd/MM}.");

        // 3) GPS, igual que SeguimientoExportacionGpsService.ConsultarYActualizar
        int i = 0;
        foreach (var v in viajes)
        {
            i++;
            Console.WriteLine($"[{i}/{viajes.Count}] {v.C.FechaCarga:dd/MM} {v.C.Fila.Tracto} {v.C.Fila.Carreta} -> {v.D?.Fila.Tracto} (llamadas API: {llamadas})");
            try
            {
                DateTime inicio = v.C.FechaWord; // fecha del despacho: sale de base la tarde anterior a la carga
                var d1 = Dispositivo(v.C.Fila.Tracto);
                DateTime? salidaPlantaUtc = null;
                if (d1 == null) v.Notas.Add($"{v.C.Fila.Tracto}: no está en Onway.");
                else
                {
                    var h1 = Historial(d1, inicio.AddDays(-2), inicio.AddDays(4));
                    v.RC = TrazabilidadCargaService.DetectarTramoCarga(h1, FechaHelper.ConvertirAUtc(inicio));
                    // Un tracto que cargó también el día anterior (BNG-862 el 09 y el 10/10): desde la
                    // fecha del despacho se encuentra la carga de ayer; se busca de nuevo después de esa.
                    for (int intento = 0; intento < 3 && v.RC.IngresoPlanta != null && v.RC.SalidaPlanta != null
                         && FechaHelper.ConvertirDeUtc(v.RC.IngresoPlanta.HoraUtc).Date < v.C.FechaCarga; intento++)
                        v.RC = TrazabilidadCargaService.DetectarTramoCarga(h1, v.RC.SalidaPlanta.HoraUtc.AddMinutes(1));
                    salidaPlantaUtc = v.RC.SalidaPlanta?.HoraUtc;
                    if (v.RC.PasoDirectoBase != null)
                        v.Notas.Add($"El tracto 1 pasó directo frente a la base sin entrar ({FechaHelper.ConvertirDeUtc(v.RC.PasoDirectoBase.HoraUtc):dd/MM HH:mm}).");
                }
                if (v.D != null)
                {
                    var d2 = Dispositivo(v.D.Fila.Tracto);
                    if (d2 == null) v.Notas.Add($"{v.D.Fila.Tracto}: no está en Onway.");
                    else
                    {
                        DateTime desdeUtc = salidaPlantaUtc ?? FechaHelper.ConvertirAUtc(inicio);
                        DateTime dia = FechaHelper.ConvertirDeUtc(desdeUtc).Date;
                        v.RD = TrazabilidadDescargaService.DetectarTramoDescarga(Historial(d2, dia.AddDays(-1), dia.AddDays(12)), desdeUtc);
                        if (v.RD.PasoDirectoTci && v.RD.LlegadaTci != null)
                            v.Notas.Add("Pasó directo por TCI: se tomó el control aduanero cercano.");
                    }
                }
            }
            catch (Exception ex)
            {
                v.Notas.Add("Error GPS: " + ex.Message);
                Console.WriteLine("   ERROR " + ex.Message);
            }
        }

        Escribir(viajes, salida);
        if (modoBd != null) CargarEnSistema(viajes, modoBd == "aplicar");
        Console.WriteLine($"RESUMEN viajes={viajes.Count} conDescarga={viajes.Count(v => v.D != null)} " +
            $"cargaCompleta={viajes.Count(v => v.RC?.SalidaBase != null && v.RC?.IngresoPlanta != null && v.RC?.SalidaPlanta != null && v.RC?.LlegadaBase != null)} " +
            $"conSalidaPlanta={viajes.Count(v => v.RC?.SalidaPlanta != null)} " +
            $"descargaHastaPlanta={viajes.Count(v => v.RD?.SalidaPlanta != null)} descargaConRegreso={viajes.Count(v => v.RD?.LlegadaBase != null)}");
        foreach (var v in viajes)
            Console.WriteLine($"FILA {v.C.FechaCarga:dd/MM} {Placa(v.C.Fila.Tracto)} {Placa(v.C.Fila.Carreta)} -> {(v.D == null ? "-" : Placa(v.D.Fila.Tracto) + " " + v.D.Pedido)} " +
                $"| carga: {(v.RC?.SalidaPlanta != null ? "ok" : "falta")} | descarga: {(v.RD?.SalidaPlanta != null ? v.RD.PlantaDescarga : v.RD?.SalidaBase != null ? "en curso" : "-")} | {string.Join(" ", v.Notas)}");
        Console.WriteLine($"Listo: {salida} (llamadas al API: {llamadas})");
    }

    // ---------- Carga en el seguimiento (sgvActualizada) ----------
    // Crea cada viaje como MANUAL con sp_SE_GuardarDatosViaje y registra sus horas con
    // sp_SE_RegistrarHitoGps (lo mismo que hace "Actualizar GPS": hora, origen, punto y regla).
    // Todo en una transacción: en modo prueba se deshace al final.
    static void CargarEnSistema(List<Viaje> viajes, bool aplicar)
    {
        var cad = new System.Data.SqlClient.SqlConnectionStringBuilder(DbHelper.ConnectionString);
        if (!string.Equals(cad.InitialCatalog, "sgvActualizada", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solo se carga en la BD de pruebas sgvActualizada; la conexión apunta a " + cad.InitialCatalog);
        if (!cad.Enlist) throw new InvalidOperationException("La cadena tiene Enlist=false: no se podría deshacer.");

        var existentes = new HashSet<string>(DbHelper.ConsultarTabla(
                "SELECT tracto1, carreta, CONVERT(date, fhProgramacion) AS dia FROM SeguimientoExportacion WHERE activo = 1 AND fhProgramacion >= @desde AND fhProgramacion < @hasta", DbHelper.Param("@desde", mes), DbHelper.Param("@hasta", mes.AddMonths(1)))
            .Rows.Cast<System.Data.DataRow>().Select(x => $"{x["tracto1"]}|{x["carreta"]}|{Convert.ToDateTime(x["dia"]):yyyyMMdd}"));

        int creados = 0, horas = 0, aplicadas = 0, yaEstaban = 0, sinPedido = 0;
        var opciones = new System.Transactions.TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(10) };
        using (var scope = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.RequiresNew, opciones))
        {
            foreach (var v in viajes)
            {
                string t1 = Placa(v.C.Fila.Tracto), carreta = Placa(v.C.Fila.Carreta);
                if (existentes.Contains($"{t1}|{carreta}|{v.C.FechaCarga:yyyyMMdd}")) { yaEstaban++; continue; }
                if (v.D == null || string.IsNullOrWhiteSpace(v.D.Pedido)) { sinPedido++; continue; }

                var hp = Hora(v.C.Fila.Horario);
                var salidaId = new System.Data.SqlClient.SqlParameter("@idResultado", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                DbHelper.EjecutarNonQuerySp("sp_SE_GuardarDatosViaje",
                    DbHelper.Param("@idSeguimiento", 0),
                    DbHelper.Param("@cliente", v.D.Pedido.Trim()),
                    DbHelper.Param("@carreta", carreta),
                    DbHelper.Param("@tracto1", t1),
                    DbHelper.Param("@conductorOrigen", SinTildesSalvoEnie(v.C.Fila.Chofer)),
                    DbHelper.Param("@tracto2", Placa(v.D.Fila.Tracto)),
                    DbHelper.Param("@conductorDestino", SinTildesSalvoEnie(v.D.Fila.Chofer)),
                    DbHelper.Param("@bodegaDescarga", v.RD?.PlantaDescarga),
                    DbHelper.Param("@fhProgramacion", hp.HasValue ? v.C.FechaCarga.Add(hp.Value) : v.C.FechaCarga),
                    DbHelper.Param("@idUsuario", null),
                    salidaId);
                int id = Convert.ToInt32(salidaId.Value);
                creados++;

                var hitos = new List<Tuple<string, HitoGps>>();
                void R(string col, HitoGps h) { if (h != null) hitos.Add(Tuple.Create(col, h)); }
                if (v.RC != null)
                {
                    R("fhSalidaBase1", v.RC.SalidaBase); R("fhLlegadaTrujillo", v.RC.LlegadaTrujillo);
                    R("fhIngresoPlanta", v.RC.IngresoPlanta); R("fhSalidaPlanta", v.RC.SalidaPlanta); R("fhLlegadaBase2", v.RC.LlegadaBase);
                }
                if (v.RD != null)
                {
                    R("fhSalidaBase2", v.RD.SalidaBase); R("fhLlegadaBodegaNacional", v.RD.LlegadaComplex);
                    R("fhIngresoBodegaNacional", v.RD.IngresoComplex); R("fhSalidaBodegaNacional", v.RD.SalidaComplex);
                    R("fhLlegadaCEBAF", v.RD.LlegadaCebaf); R("fhCruceEcuador", v.RD.Cruce);
                    R("fhLlegadaTCI", v.RD.LlegadaTci); R("fhSalidaTCI", v.RD.SalidaTci);
                    R("fhLlegadaPlantaEcuador", v.RD.LlegadaPlanta); R("fhLlegadaAlmacen", v.RD.LlegadaPlanta);
                    R("fhIngreso", v.RD.IngresoPlanta); R("fhSalida", v.RD.SalidaPlanta); R("fhLlegadaBaseFinal", v.RD.LlegadaBase);
                }
                foreach (var t in hitos)
                {
                    var h = t.Item2;
                    DateTime local = FechaHelper.ConvertirDeUtc(h.HoraUtc.AddTicks(-(h.HoraUtc.Ticks % TimeSpan.TicksPerSecond)));
                    bool onway = h.Fuente != null && h.Fuente.StartsWith("Alerta de geocerca de Onway", StringComparison.OrdinalIgnoreCase);
                    object ok = DbHelper.EjecutarEscalarSp("sp_SE_RegistrarHitoGps",
                        DbHelper.Param("@idSeguimiento", id),
                        DbHelper.Param("@columna", t.Item1),
                        DbHelper.Param("@horaGps", local),
                        DbHelper.Param("@fuenteGps", onway ? "ONWAY" : "GPS"),
                        DbHelper.Param("@latitud", h.Punto != null ? (object)Math.Round((decimal)h.Punto.Lat, 6) : null),
                        DbHelper.Param("@longitud", h.Punto != null ? (object)Math.Round((decimal)h.Punto.Lng, 6) : null),
                        DbHelper.Param("@regla", h.Fuente != null && h.Fuente.Length > 200 ? h.Fuente.Substring(0, 200) : h.Fuente));
                    horas++; if (Convert.ToInt32(ok) == 1) aplicadas++;
                }
                DbHelper.EjecutarNonQuery("UPDATE SeguimientoExportacion SET fhUltimaConsultaGps = dbo.fn_AhoraPeru() WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", id));
                AuditoriaHelper.Registrar("INSERT", "SeguimientoExportacion", id,
                    $"Cuadro STATUS GENERAL {mes:MM/yyyy} (programación en Word + GPS): pedido {v.D.Pedido}, carreta {carreta}, carga {t1}, descarga {Placa(v.D.Fila.Tracto)}; {hitos.Count} horas del GPS.");
            }

            var cuenta = DbHelper.ConsultarTabla("SELECT COUNT(*) n FROM SeguimientoExportacion WHERE activo = 1 AND fhProgramacion >= @desde AND fhProgramacion < @hasta", DbHelper.Param("@desde", mes), DbHelper.Param("@hasta", mes.AddMonths(1)));
            Console.WriteLine($"BD {(aplicar ? "APLICAR" : "PRUEBA")}: viajes creados={creados}, horas registradas={horas} (columnas llenadas={aplicadas}), " +
                              $"ya estaban={yaEstaban}, sin pedido (no se crean)={sinPedido}; viajes del mes en la BD dentro de la transacción={cuenta.Rows[0]["n"]}");
            if (aplicar) { scope.Complete(); Console.WriteLine("BD: confirmado."); }
            else Console.WriteLine("BD: deshecho (modo prueba).");
        }
    }

    // El Word escribe "SUNCIÓN" o "SUNCION" según el día: se guarda sin tildes (la Ñ se respeta).
    static string SinTildesSalvoEnie(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToUpperInvariant())
            sb.Append(ch == 'Ñ' ? "Ñ" : SinTildes(ch.ToString()));
        return Regex.Replace(sb.ToString(), @"\s+", " ");
    }

    // ---------- GPS ----------
    static OnwayDevice Dispositivo(string placa)
    {
        placa = Placa(placa);
        if (dispositivos.TryGetValue(placa, out var d)) return d;
        string archivo = Path.Combine(cache, "disp_" + placa + ".json");
        if (File.Exists(archivo)) d = JsonConvert.DeserializeObject<OnwayDevice>(File.ReadAllText(archivo));
        else
        {
            d = cliente.BuscarDispositivoPorPlaca(placa); llamadas++;
            File.WriteAllText(archivo, JsonConvert.SerializeObject(d));
        }
        dispositivos[placa] = d;
        return d;
    }

    static List<OnwayHistoryPoint> Historial(OnwayDevice d, DateTime desdeDia, DateTime hastaDia)
    {
        var puntos = new List<OnwayHistoryPoint>();
        for (DateTime dia = desdeDia; dia <= hastaDia && dia <= FechaHelper.Hoy(); dia = dia.AddDays(1))
        {
            string clave = Placa(d.Alias) + "_" + dia.ToString("yyyyMMdd"); // el Id es largo: la ruta pasaba de 260
            if (!memoria.TryGetValue(clave, out var lista))
            {
                string archivo = Path.Combine(cache, "h_" + clave + ".json");
                // El día de hoy todavía está llegando: no se guarda en disco.
                if (File.Exists(archivo) && dia < FechaHelper.Hoy())
                    lista = JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(archivo));
                else
                {
                    lista = cliente.ObtenerHistorial(d.Id, dia.AddHours(5), dia.AddDays(1).AddHours(5).AddSeconds(-1)); llamadas++;
                    if (dia < FechaHelper.Hoy()) File.WriteAllText(archivo, JsonConvert.SerializeObject(lista));
                }
                memoria[clave] = lista;
            }
            puntos.AddRange(lista);
        }
        return puntos;
    }

    // ---------- Excel ----------
    static readonly string[] Encabezados =
    {
        "CLIENTE", "Conductor Origen", "Tracto 1", "Carreta", "Conductor Destino", "Traxto 2",
        "F.H.S.Base:", "", "F.H.LL. Trujillo", "", "F.H.Registro", "", "F.H. PROGRAMACION", "", "F.H.I planta", "",
        "F.H.Inicio de Carga", "", "F.H.Termino carga", "", "F.H.S Planta", "", "F.H.LL. Base ", "", "F.H.S Base ", "",
        "F.H.LL.Bodega Nacional", "", "F.H.I. Bodega Nacional", "", "F.H.S.Bodega Nacional", "", "BODEGA",
        "F.H.LL CEBAF E", "", "F.H CRUCE E", "", "F.H. AUTORIZACION DE LA NACIONALIZACION DE PRODUCTO", "",
        "BODEGA ECUATORIANA", "F.H.LL.TCI", "", "F.H.S TCI", "", "BODEGA DESCARGA", "F.H.LL.Planta", "",
        "F.H.LL.Almacen", "", "F.H.Ingreso ", "", "F.H.I. descarga", "", "F.H.T. descarga", "", "F.H.Salida", "",
        "F.H.LL Base", "", "", "", "Motivo de retraso / Comentario",
    };

    static void Escribir(List<Viaje> viajes, string salida)
    {
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Seguimiento EXPORTACION");
            ws.Cell(1, 1).Value = "STATUS DE UNIDADES DE CARGA - SERVICIOS GENERALES VIVIANA E.I.R.L - " + mes.ToString("MMMM yyyy", new CultureInfo("es-PE")).ToUpperInvariant();
            ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = $"Viajes de Vitapro de las programaciones en Word (carga de {mes:MM/yyyy}, programaciones hasta el {ultimaProgramacion:dd/MM}). Horas del GPS de Onway, consultado el {FechaHelper.Ahora():dd/MM/yyyy HH:mm}. " +
                                  "Celdas en gris: datos que el GPS no conoce (registro, inicio/término de carga y descarga, autorización) — se llenan a mano.";
            const int filaEnc = 4;
            for (int c = 0; c < Encabezados.Length; c++) ws.Cell(filaEnc, c + 1).Value = Encabezados[c];
            var enc = ws.Range(filaEnc, 1, filaEnc, Encabezados.Length);
            enc.Style.Font.Bold = true; enc.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78"); enc.Style.Font.FontColor = XLColor.White;
            enc.Style.Alignment.WrapText = true; enc.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(filaEnc).Height = 45;

            var det = wb.AddWorksheet("Detalle GPS");
            string[] encDet = { "Pedido", "Carga", "Tracto 1", "Carreta", "Tracto 2", "Hito", "Fecha y hora (GPS)", "Regla / fuente", "Latitud", "Longitud", "Mapa" };
            for (int c = 0; c < encDet.Length; c++) det.Cell(1, c + 1).Value = encDet[c];
            det.Range(1, 1, 1, encDet.Length).Style.Font.Bold = true;
            int filaDet = 2;

            int r = filaEnc + 1;
            DateTime? diaAnterior = null;
            foreach (var v in viajes)
            {
                var c = v.C;
                ws.Cell(r, 1).Value = v.D?.Pedido ?? "";
                ws.Cell(r, 2).Value = c.Fila.Chofer;
                ws.Cell(r, 3).Value = Placa(c.Fila.Tracto);
                ws.Cell(r, 4).Value = Placa(c.Fila.Carreta);
                ws.Cell(r, 5).Value = v.D?.Fila.Chofer ?? "";
                ws.Cell(r, 6).Value = v.D != null ? Placa(v.D.Fila.Tracto) : "";

                var hp = Hora(c.Fila.Horario);
                if (hp.HasValue) FechaHora(ws, r, 13, c.FechaCarga.Add(hp.Value));
                else { ws.Cell(r, 13).Value = c.FechaCarga; ws.Cell(r, 13).Style.DateFormat.Format = "dd/MM/yyyy"; }

                void H(int col, HitoGps h, string nombre)
                {
                    if (h == null) return;
                    DateTime local = FechaHelper.ConvertirDeUtc(h.HoraUtc.AddTicks(-(h.HoraUtc.Ticks % TimeSpan.TicksPerSecond)));
                    if (col > 0) FechaHora(ws, r, col, local);
                    det.Cell(filaDet, 1).Value = v.D?.Pedido ?? "";
                    det.Cell(filaDet, 2).Value = c.FechaCarga; det.Cell(filaDet, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                    det.Cell(filaDet, 3).Value = Placa(c.Fila.Tracto);
                    det.Cell(filaDet, 4).Value = Placa(c.Fila.Carreta);
                    det.Cell(filaDet, 5).Value = v.D != null ? Placa(v.D.Fila.Tracto) : "";
                    det.Cell(filaDet, 6).Value = nombre;
                    det.Cell(filaDet, 7).Value = local; det.Cell(filaDet, 7).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
                    det.Cell(filaDet, 8).Value = h.Fuente ?? "";
                    if (h.Punto != null)
                    {
                        det.Cell(filaDet, 9).Value = Math.Round(h.Punto.Lat, 6);
                        det.Cell(filaDet, 10).Value = Math.Round(h.Punto.Lng, 6);
                        string url = "https://www.google.com/maps?q=" + h.Punto.Lat.ToString(CultureInfo.InvariantCulture) + "," + h.Punto.Lng.ToString(CultureInfo.InvariantCulture);
                        det.Cell(filaDet, 11).Value = "ver"; det.Cell(filaDet, 11).SetHyperlink(new XLHyperlink(url));
                    }
                    filaDet++;
                }

                if (v.RC != null)
                {
                    H(7, v.RC.SalidaBase, "Salida de base (tracto 1)");
                    H(9, v.RC.LlegadaTrujillo, "Llegada a Trujillo");
                    H(15, v.RC.IngresoPlanta, "Ingreso a planta Vitapro");
                    H(21, v.RC.SalidaPlanta, "Salida de planta Vitapro");
                    H(23, v.RC.LlegadaBase, "Llegada a base (tracto 1)");
                }
                if (v.RD != null)
                {
                    H(25, v.RD.SalidaBase, "Salida de base (tracto 2)");
                    H(27, v.RD.LlegadaComplex, "Llegada al Complex");
                    H(29, v.RD.IngresoComplex, "Ingreso al Complex");
                    H(31, v.RD.SalidaComplex, "Salida del Complex");
                    if (v.RD.LlegadaComplex != null || v.RD.SalidaComplex != null) ws.Cell(r, 33).Value = "COMPLEX";
                    H(34, v.RD.LlegadaCebaf, "Llegada al CEBAF");
                    H(36, v.RD.Cruce, "Cruce a Ecuador");
                    if (v.RD.LlegadaTci != null || v.RD.SalidaTci != null) ws.Cell(r, 40).Value = "TCI";
                    H(41, v.RD.LlegadaTci, v.RD.PasoDirectoTci ? "Llegada a TCI (control aduanero, pasó directo)" : "Llegada a TCI");
                    H(43, v.RD.SalidaTci, v.RD.PasoDirectoTci ? "Salida de TCI (control aduanero, pasó directo)" : "Salida de TCI");
                    if (!string.IsNullOrEmpty(v.RD.PlantaDescarga)) ws.Cell(r, 45).Value = v.RD.PlantaDescarga;
                    H(46, v.RD.LlegadaPlanta, "Llegada a planta en Ecuador");
                    if (v.RD.LlegadaPlanta != null) FechaHora(ws, r, 48, FechaHelper.ConvertirDeUtc(v.RD.LlegadaPlanta.HoraUtc.AddTicks(-(v.RD.LlegadaPlanta.HoraUtc.Ticks % TimeSpan.TicksPerSecond)))); // almacén = misma hora, como la administradora
                    H(50, v.RD.IngresoPlanta, "Ingreso a planta en Ecuador");
                    H(56, v.RD.SalidaPlanta, "Salida de planta en Ecuador");
                    H(58, v.RD.LlegadaBase, "Llegada a base (tracto 2)");
                }

                // Lo que falta y por qué
                var faltan = new List<string>();
                if (v.RC == null || v.RC.SalidaPlanta == null) faltan.Add("carga");
                if (v.D != null && (v.RD == null || v.RD.SalidaPlanta == null)) faltan.Add("descarga");
                var notas = new List<string>(v.Notas);
                if (faltan.Count > 0 && !notas.Any(n => n.StartsWith("Sin descarga")))
                    notas.Add("GPS incompleto en " + string.Join(" y ", faltan) + " (viaje en curso o sin señal).");
                ws.Cell(r, 62).Value = string.Join(" ", notas);

                // Gris en lo manual
                foreach (int col in new[] { 11, 12, 17, 18, 19, 20, 38, 39, 52, 53, 54, 55 })
                    ws.Cell(r, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEDED");
                if (diaAnterior.HasValue && diaAnterior != c.FechaCarga)
                    ws.Range(r, 1, r, 62).Style.Border.TopBorder = XLBorderStyleValues.Medium;
                diaAnterior = c.FechaCarga;
                r++;
            }

            ws.Column(1).Width = 12; ws.Columns(2, 2).Width = 24; ws.Column(5).Width = 24;
            ws.Columns(3, 4).Width = 10; ws.Column(6).Width = 10;
            for (int col = 7; col <= 61; col++) ws.Column(col).Width = ws.Cell(filaEnc, col).GetString().Length == 0 ? 7 : 11;
            foreach (int col in new[] { 33, 40, 45 }) ws.Column(col).Width = 11;
            ws.Column(62).Width = 70;
            ws.SheetView.FreezeRows(filaEnc); ws.SheetView.FreezeColumns(6);
            ws.Range(filaEnc, 1, r - 1, 62).SetAutoFilter();

            det.Columns().AdjustToContents(); det.Column(8).Width = 60;
            det.SheetView.FreezeRows(1);
            wb.SaveAs(salida);
        }
    }

    static void FechaHora(IXLWorksheet ws, int fila, int col, DateTime t)
    {
        ws.Cell(fila, col).Value = t.Date; ws.Cell(fila, col).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Cell(fila, col + 1).Value = t.TimeOfDay; ws.Cell(fila, col + 1).Style.DateFormat.Format = "hh:mm";
    }

    // ---------- texto ----------
    static readonly string[] Meses = { "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO", "AGOSTO", "SEPTIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE" };

    static DateTime FechaCargaDelTitulo(string t, DateTime fechaWord)
    {
        var m = Regex.Match(t, @"CARGAN\s+(\d{1,2})\s+DE\s+([A-Z]+)");
        if (m.Success)
        {
            string mes = m.Groups[2].Value == "SETIEMBRE" ? "SEPTIEMBRE" : m.Groups[2].Value;
            int nm = Array.IndexOf(Meses, mes) + 1;
            if (nm > 0) return new DateTime(fechaWord.Year, nm, int.Parse(m.Groups[1].Value));
        }
        return fechaWord.AddDays(1);
    }

    static TimeSpan? Hora(string h)
    {
        if (string.IsNullOrWhiteSpace(h)) return null;
        var m = Regex.Match(h.ToUpperInvariant(), @"(\d{1,2})[:.](\d{2})\s*([AP])?");
        if (!m.Success) return null;
        int hh = int.Parse(m.Groups[1].Value), mm = int.Parse(m.Groups[2].Value);
        if (m.Groups[3].Value == "P" && hh < 12) hh += 12;
        if (m.Groups[3].Value == "A" && hh == 12) hh = 0;
        return new TimeSpan(hh, mm, 0);
    }

    static string Placa(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "";
        var m = Regex.Match(p.ToUpperInvariant().Replace(" ", ""), @"([A-Z0-9]{3})-?([0-9]{3})");
        return m.Success ? m.Groups[1].Value + "-" + m.Groups[2].Value : p.Trim().ToUpperInvariant();
    }

    static string SinTildes(string s)
    {
        var n = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in n) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().ToUpperInvariant();
    }

    // "JAIMER ROMERO" / "CIRSTHIAN FLORES": basta con que coincida el apellido (2.ª palabra).
    static bool MismoNombre(string a, string b)
    {
        var x = SinTildes(a).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var y = SinTildes(b ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (x.Length == 0 || y.Length == 0) return false;
        return x.Last() == y.Last() || x[0] == y[0];
    }
}
