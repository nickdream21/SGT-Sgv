using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-10: toda consulta de LISTADO lleva un límite (<c>TOP</c>, <c>OFFSET</c> o un rango de fechas),
    /// para que una pantalla no traiga la tabla entera cuando la tabla crezca. Prueba de fuente, sin BD.
    ///
    /// Heurística (es una aproximación a propósito; afinarla aquí si da un falso positivo):
    /// <list type="bullet">
    /// <item><b>SQL en línea</b> (<c>Services/**</c> y <c>Views/**/*.cs</c>): se juntan los literales de texto
    ///   de cada miembro (método, propiedad o constante; así cuenta también el SQL armado con
    ///   <c>StringBuilder</c> o <c>+=</c>). Es un listado si tiene <c>SELECT … FROM</c> y un <c>ORDER BY</c>
    ///   que no es el de un <c>OVER (…)</c>: una consulta que ordena es la que pinta varias filas.</item>
    /// <item><b>SP</b> (<c>Database/StoredProcedures</c>): los <c>sp_*Obtener|Listar|Buscar|Consultar|Reporte*</c>
    ///   con <c>ORDER BY</c> fuera de un <c>OVER (…)</c>.</item>
    /// <item><b>Límite</b>: <c>TOP</c>, <c>OFFSET</c>, o un rango de fechas (<c>fecha… &gt;=|&lt;=|&lt;|&gt;|BETWEEN</c>,
    ///   <c>BETWEEN @…</c>, o un período <c>Year|Month|anio|mes = @…</c>).</item>
    /// <item><b>No es un listado que crece</b> la consulta acotada a un padre: <c>id…|…Id|numero… = @p</c> con
    ///   un parámetro obligatorio (no aparece en <c>@p IS NULL</c>, <c>@p &lt;= 0</c>, <c>@p = 0</c> ni
    ///   <c>@p = ''</c>), o una lista de ids (<c>STRING_SPLIT(@…)</c>, <c>IN ({…})</c>): son las filas de UNA
    ///   orden, un despacho, un lote.</item>
    /// <item>El resto va en una <b>lista cerrada</b> con su motivo (catálogos y conjuntos que no crecen con el
    ///   tiempo). Una excepción que ya no hace falta también falla: la lista se ajusta a lo que hay.</item>
    /// </list>
    /// Los comentarios (C# y SQL) no cuentan.
    /// </summary>
    public class ConsultasListadoAcotadasTests
    {
        /// <summary>SQL en línea sin límite permitido: "ruta relativa a WebSGV::miembro" → motivo.</summary>
        private static readonly Dictionary<string, string> ExcepcionesEnLinea = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Services/Clientes/CatalogoClienteService.cs::ObtenerClientes",      "catálogo de clientes activos" },
            { "Services/Clientes/CatalogoClienteService.cs::Obtener",              "catálogo de productos/rutas/lugares de UN cliente (el ?: solo elige si suma los de uso general)" },
            { "Services/Liquidaciones/LiquidacionesPendientesService.cs::ObtenerAprobadas", "pendiente M-137: Control de aprobadas abre con 30 días, pero sin fechas trae todas las aprobadas" },
            { "Services/Liquidaciones/LiquidacionesPendientesService.cs::ObtenerPorConformar", "solo las POR_CONFORMAR (salen al firmar el conductor; no crecen con el tiempo)" },
            { "Views/CargarExcel.aspx.cs::LoadUploadHistory",                      "pendiente M-139: historial de cargas de Excel; el mes/año es opcional (0 filas hoy)" },
            { "Services/Auditoria/ConsultaAuditoriaService.cs::ObtenerAcciones",   "códigos de acción distintos (decenas)" },
            { "Services/Despachos/DestinoDescargaService.cs::ObtenerPlantas",       "catálogo de plantas" },
            { "Services/Despachos/DestinoDescargaService.cs::ObtenerPlantasDeDescarga", "catálogo de plantas internacionales" },
            { "Services/Despachos/ListaDespachosService.cs::ObtenerPlantasActivas", "catálogo de plantas" },
            { "Services/Reportes/ReportesOrdenesViajeService.cs::ObtenerConductoresActivos", "catálogo de conductores activos" },
            { "Services/Reportes/ReportesOrdenesViajeService.cs::ObtenerClientes",  "catálogo de clientes" },
            { "Views/Auditoria.aspx.cs::CargarFiltroTablas",                        "nombres distintos de tabla auditada (decenas)" },
            { "Views/BandejaDocumentos.aspx.cs::Catalogos",                         "catálogos de conductores, tractos y carretas" },
            { "Views/DashboardAdminSistema.aspx.cs::CargarRoles",                  "una fila por rol (GROUP BY rol)" },
            { "Views/GestionUsuarios.aspx.cs::CargarConductores",                   "catálogo de conductores para vincular" },
            { "Views/GestionUsuarios.aspx.cs::EnlazarUsuarios",                     "usuarios del sistema (decenas)" },
            { "Views/MovimientosViaje.aspx.cs::CargarListas",                      "catálogo de conductores" },
            { "Views/RegistroChoferes.aspx.cs::CargarConductores",                  "catálogo de conductores" },
            { "Views/RegistroClientes.aspx.cs::CargarClientes",                     "catálogo de clientes" },
            { "Views/RegistroPeajes.aspx.cs::CargarPeajes",                         "catálogo de estaciones de peaje" },
            { "Views/RegistroPlantas.aspx.cs::CargarPlantas",                       "catálogo de plantas" },
            { "Views/RegistroProductos.aspx.cs::CargarClientes",                    "catálogo de clientes" },
            { "Views/RegistroRutas.aspx.cs::CargarClientes",                        "catálogo de clientes" },
            { "Views/SaldosConductores.aspx.cs::LlenarFiltros",                     "catálogo de conductores con liquidaciones" },
        };

        /// <summary>SP de listado sin límite permitidos → motivo.</summary>
        private static readonly Dictionary<string, string> ExcepcionesSp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "sp_DC_ObtenerEstacionesPeaje",      "catálogo de estaciones de peaje" },
            { "sp_LD_ObtenerConductoresConViajes", "conductores con un viaje ABIERTO (pocos a la vez)" },
            { "sp_LD_ObtenerLotesRegistrados",     "pendiente M-138: las fechas son opcionales y ListaDespachos lo llama sin fechas (contadores de EstablecerContadores, \"documentos pendientes\" y campos vaciados)" },
            { "sp_ObtenerLiquidacionesPendientes", "solo las PENDIENTES de revisión (salen al revisarlas; no crecen con el tiempo)" },
            { "sp_LD_ObtenerTodosConductores",     "catálogo de conductores" },
            { "sp_LD_ObtenerViajesActivos",        "viajes ABIERTOS (pocos a la vez; los cerrados no salen)" },
            { "sp_ObtenerCarretasActivas",         "catálogo de carretas" },
            { "sp_ObtenerClientesActivos",         "catálogo de clientes" },
            { "sp_ObtenerConductoresActivos",      "catálogo de conductores" },
            { "sp_ObtenerPlantasPorAmbito",        "catálogo de plantas" },
            { "sp_ObtenerTractosActivos",          "catálogo de tractos" },
        };

        // ── Pruebas ─────────────────────────────────────────────────────────

        [Fact]
        public void ConsultasEnLinea_DeListado_TienenLimite()
        {
            var sinLimite = ListadosEnLineaSinLimite();
            var fallas = sinLimite.Keys.Where(k => !ExcepcionesEnLinea.ContainsKey(k))
                .Select(k => k + " → " + sinLimite[k]).ToList();
            Assert.True(fallas.Count == 0,
                "Consulta de listado sin TOP, OFFSET ni rango de fechas (agregue el límite, o si es un catálogo, " +
                "agréguela a ExcepcionesEnLinea con su motivo):\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void ExcepcionesEnLinea_SiguenHaciendoFalta()
        {
            var sinLimite = ListadosEnLineaSinLimite();
            var sobran = ExcepcionesEnLinea.Keys.Where(k => !sinLimite.ContainsKey(k)).ToList();
            Assert.True(sobran.Count == 0,
                "Excepción que ya no corresponde a una consulta de listado sin límite (quítela de ExcepcionesEnLinea):\n" +
                string.Join("\n", sobran));
        }

        [Fact]
        public void SpDeListado_TienenLimite()
        {
            var sinLimite = SpDeListadoSinLimite();
            var fallas = sinLimite.Where(n => !ExcepcionesSp.ContainsKey(n)).ToList();
            Assert.True(fallas.Count == 0,
                "SP de listado sin TOP, OFFSET ni rango de fechas (agregue el límite, o si es un catálogo, " +
                "agréguelo a ExcepcionesSp con su motivo):\n" + string.Join("\n", fallas));
        }

        [Fact]
        public void ExcepcionesSp_SiguenHaciendoFalta()
        {
            var sinLimite = new HashSet<string>(SpDeListadoSinLimite(), StringComparer.OrdinalIgnoreCase);
            var sobran = ExcepcionesSp.Keys.Where(k => !sinLimite.Contains(k)).ToList();
            Assert.True(sobran.Count == 0, "Excepción de SP que ya no hace falta (quítela de ExcepcionesSp):\n" + string.Join("\n", sobran));
        }

        [Fact]
        public void LiquidacionesContabilidad_PaginaConOffsetYTotal_YSinSubconsultasEscalares()
        {
            string cs = File.ReadAllText(Path.Combine(RaizWeb(), "Services", "Liquidaciones", "LiquidacionesContabilidadService.cs"));
            string sql = string.Join(" ", MiembrosConSql(cs).Where(m => m.Nombre == "SqlPagina").Select(m => m.Sql));
            Assert.False(string.IsNullOrEmpty(sql), "No está la constante SqlPagina.");
            Assert.Matches(@"(?i)OFFSET\s+@Salto\s+ROWS\s+FETCH\s+NEXT\s+@Tamano\s+ROWS\s+ONLY", sql);
            Assert.Matches(@"(?i)COUNT\(\*\)\s+OVER\s*\(\s*\)", sql);
            Assert.Matches(@"(?i)fechaSalida\s*>=\s*@Desde", sql);
            Assert.Matches(@"(?i)fechaSalida\s*<=\s*@Hasta", sql);
            Assert.Equal(2, Regex.Matches(sql, @"(?i)OUTER\s+APPLY").Count);
            Assert.DoesNotMatch(@"(?i)ISNULL\s*\(\s*\(\s*SELECT", sql);
            Assert.Contains("public const int TamanoPagina = 50;", cs);
            Assert.Contains("public const int DiasRangoPorDefecto = 90;", cs);

            // El script abre con los 90 días (U-095) y tiene "Todo el historial" y el paginador.
            string js = File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "LiquidacionesAprobadasContabilidad.js"));
            Assert.Contains("var DIAS_RANGO_POR_DEFECTO = 90;", js);
            Assert.Contains("verTodoHistorial", js);
            Assert.Contains("'Mostrando '", js);
            Assert.Contains("data-accion=\"irPagina\"", js);
            // El paginador pide otra página de la ÚLTIMA búsqueda, no de lo que esté escrito en los filtros.
            Assert.Contains("buscarLiquidaciones(true, pagina, _ultimosFiltros)", js);
            Assert.Contains("_ultimosFiltros = filtros;", js);
            string aspx = File.ReadAllText(Path.Combine(RaizWeb(), "Views", "LiquidacionesAprobadasContabilidad.aspx"));
            Assert.Contains("data-accion=\"verTodoHistorial\"", aspx);
            Assert.Contains("<label for=\"txtFechaDesde\">", aspx);
            Assert.Contains("<label for=\"txtFechaHasta\">", aspx);
        }

        [Theory]
        [InlineData("", true, null)]
        [InlineData("  ", true, null)]
        [InlineData(null, true, null)]
        [InlineData("2026-08-24", true, "2026-08-24")]
        [InlineData("24/08/2026", false, null)]
        [InlineData("2026-02-30", false, null)]
        [InlineData("1899-01-01", false, null)]
        [InlineData("2026-08-24'; DROP", false, null)]
        public void Contabilidad_LeerFecha(string texto, bool valida, string esperada)
        {
            Assert.Equal(valida, WebSGV.Views.LiquidacionesAprobadasContabilidad.LeerFecha(texto, out DateTime? f));
            Assert.Equal(esperada, f?.ToString("yyyy-MM-dd"));
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(0, 0)]
        [InlineData(3, 3)]
        [InlineData(100000, 100000)]
        [InlineData(int.MaxValue, 100000)]   // sin el tope, int.MaxValue * 50 desbordaba el salto
        public void Contabilidad_PaginaSegura_NoDesborda(int pedida, int esperada)
        {
            int p = WebSGV.Services.Liquidaciones.LiquidacionesContabilidadService.PaginaSegura(pedida);
            Assert.Equal(esperada, p);
            Assert.True((long)p * WebSGV.Services.Liquidaciones.LiquidacionesContabilidadService.TamanoPagina <= int.MaxValue);
        }

        // ── La heurística, probada sola ────────────────────────────────────

        [Theory]
        [InlineData("SELECT a FROM T ORDER BY a", false)]
        [InlineData("SELECT TOP 50 a FROM T ORDER BY a", true)]
        [InlineData("SELECT TOP (@max) a FROM T ORDER BY a", true)]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET @s ROWS FETCH NEXT 50 ROWS ONLY", true)]
        [InlineData("SELECT a FROM T WHERE fechaSalida >= @Desde ORDER BY a", true)]
        [InlineData("SELECT a FROM T WHERE d.fechaDespacho BETWEEN @desde AND @hasta ORDER BY a", true)]
        [InlineData("SELECT a FROM T WHERE x.idOrden = @id ORDER BY a", true)]           // filas de un padre
        [InlineData("SELECT a FROM T WHERE numeroOrdenViaje = @n ORDER BY a", true)]
        [InlineData("SELECT a FROM T WHERE (@id IS NULL OR idCliente = @id) ORDER BY a", false)]  // filtro opcional
        [InlineData("SELECT a FROM T WHERE (@id <= 0 OR idConductor = @id) ORDER BY a", false)]
        [InlineData("SELECT a FROM T WHERE id IN (SELECT value FROM STRING_SPLIT(@ids, ',')) ORDER BY a", true)]
        [InlineData("SELECT a FROM T WHERE id IN ({lista}) ORDER BY a", true)]
        [InlineData("SELECT a FROM T WHERE Status <> 'X' AND Year = @Year ORDER BY a", true)]   // período
        [InlineData("SELECT a FROM T WHERE (@Year IS NULL OR Year = @Year) ORDER BY a", false)]  // período opcional
        [InlineData("SELECT a FROM T WHERE (@Desde IS NULL OR fechaSalida >= @Desde) ORDER BY a", false)]  // fecha opcional
        [InlineData("SELECT a FROM T WHERE (@fechaDesde IS NULL OR d.fechaDespacho >= @fechaDesde) AND (@fechaHastaFin IS NULL OR d.fechaDespacho <= @fechaHastaFin) ORDER BY a", false)]
        [InlineData("SELECT a FROM T WHERE (@h IS NULL OR fechaSalida <= @h) AND fechaSalida >= @d ORDER BY a", true)]  // una obligatoria basta
        [InlineData("SELECT a FROM T WHERE fecha >= DATEADD(DAY, -30, SYSDATETIME()) ORDER BY a", true)]  // valor fijo
        [InlineData("SELECT a FROM T WHERE (@Desde IS NULL OR fechaSalida >= @Desde) ORDER BY a OFFSET 0 ROWS", true)]
        [InlineData("SELECT a FROM T WHERE activo = 1 -- TOP 10\n ORDER BY a", false)]  // comentario no cuenta
        public void Heuristica_ReconoceElLimite(string sql, bool limitada)
        {
            Assert.True(EsListado(sql));
            Assert.Equal(limitada, TieneLimiteOAcotada(sql));
        }

        [Theory]
        [InlineData("SELECT a FROM T WHERE id = @id")]                                     // sin ORDER BY: no es listado
        [InlineData("SELECT a, ROW_NUMBER() OVER (PARTITION BY b ORDER BY c) AS n FROM T")] // el ORDER BY es del OVER
        [InlineData("UPDATE T SET a = 1 ORDER BY")]                                        // sin SELECT ... FROM
        public void Heuristica_NoEsListado(string sql)
        {
            Assert.False(EsListado(sql));
        }

        [Fact]
        public void Lector_SeparaMiembrosYNoLeeComentarios()
        {
            string cs = "class X {\n" +
                        "  public static DataTable Uno(int a)\n  {\n    // \"SELECT b FROM C ORDER BY b\"\n" +
                        "    var sb = new StringBuilder(@\"SELECT a\n FROM T\"); sb.Append(\" ORDER BY a\"); /* \"TOP 1\" */\n  }\n" +
                        "  private const string Dos = \"SELECT x FROM Y\" + \" WHERE id = @id\";\n}";
            var m = MiembrosConSql(cs).ToList();
            Assert.Equal(new[] { "Uno", "Dos" }, m.Select(x => x.Nombre).ToArray());
            Assert.Equal("SELECT a\n FROM T  ORDER BY a", m[0].Sql);
            Assert.True(EsListado(m[0].Sql));
            Assert.False(TieneLimiteOAcotada(m[0].SqlFijo));
        }

        /// <summary>Un filtro que se agrega solo a veces (if / else / ?:) no limita; uno fijo sí.</summary>
        [Theory]
        [InlineData("if (d.HasValue) query += \" AND fechaSalida >= @d\";", false)]
        [InlineData("if (d.HasValue) { query += \" AND fechaSalida >= @d\"; }", false)]
        [InlineData("if (x) { y(); } else { sb.Append(\" AND fechaSalida >= @d\"); }", false)]
        [InlineData("query += d.HasValue ? \" AND fechaSalida >= @d\" : \"\";", false)]
        [InlineData("query += \" AND fechaSalida >= @d\";", true)]
        [InlineData("if (x) { y(); } query += \" AND fechaSalida >= @d\";", true)]
        [InlineData("if (x) query += \" AND a = 1; b\"; query += \" AND fechaSalida >= @d\";", true)]   // el ; del SQL no corta
        public void Lector_FiltroCondicionalNoLimita(string cuerpo, bool limitada)
        {
            string cs = "class X {\n  public static DataTable Uno(DateTime? d)\n  {\n" +
                        "    string query = \"SELECT a FROM T WHERE activo = 1\";\n    " + cuerpo + "\n" +
                        "    query += \" ORDER BY a\";\n  }\n}";
            var m = MiembrosConSql(cs).Single();
            Assert.True(EsListado(m.Sql));
            Assert.Contains("fechaSalida", m.Sql);
            Assert.Equal(limitada, TieneLimiteOAcotada(m.SqlFijo));
        }

        // ── Lectura del fuente ─────────────────────────────────────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        /// <summary>"ruta::miembro" → inicio del SQL, de cada listado en línea sin límite.</summary>
        private static Dictionary<string, string> ListadosEnLineaSinLimite()
        {
            string raiz = RaizWeb();
            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var archivos = new[] { "Services", "Views" }
                .SelectMany(d => Directory.GetFiles(Path.Combine(raiz, d), "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase));
            foreach (string archivo in archivos)
            {
                string rel = archivo.Substring(raiz.Length + 1).Replace('\\', '/');
                foreach (var miembro in MiembrosConSql(File.ReadAllText(archivo)))
                {
                    if (!EsListado(miembro.Sql) || TieneLimiteOAcotada(miembro.SqlFijo)) continue;
                    string sql = Regex.Replace(miembro.Sql, @"\s+", " ").Trim();
                    resultado[rel + "::" + miembro.Nombre] = sql.Length > 120 ? sql.Substring(0, 120) + "…" : sql;
                }
            }
            return resultado;
        }

        private static List<string> SpDeListadoSinLimite()
        {
            string carpeta = Path.Combine(RaizWeb(), "Database", "StoredProcedures");
            var lista = new List<string>();
            foreach (string archivo in Directory.GetFiles(carpeta, "sp_*.sql"))
            {
                string nombre = Path.GetFileNameWithoutExtension(archivo);
                if (!Regex.IsMatch(nombre, @"(?i)Obtener|Listar|Buscar|Consultar|Reporte")) continue;
                string sql = File.ReadAllText(archivo);
                if (EsListado(sql) && !TieneLimiteOAcotada(sql)) lista.Add(nombre);
            }
            return lista;
        }

        private static readonly Regex DeclaracionMiembro = new Regex(
            @"(?m)^[ \t]*(?:\[[^\]\r\n]*\][ \t]*)*(?:public|private|internal|protected)\b[^\r\n]*");
        private static readonly Regex NombreMiembro = new Regex(@"(\w+)\s*(?:<[^>()]*>)?\s*(?:\(|=|\{|;)");

        /// <summary>SQL de un miembro: todos sus literales (<see cref="Sql"/>) y solo los que no se agregan bajo una condición (<see cref="SqlFijo"/>).</summary>
        internal sealed class MiembroSql
        {
            public string Nombre { get; set; }
            public string Sql { get; set; }
            /// <summary>Sin los literales que van en una rama <c>if</c>/<c>else</c> o un <c>?:</c>: un filtro agregado solo a veces no limita.</summary>
            public string SqlFijo { get; set; }
        }

        /// <summary>
        /// Miembros (método, propiedad, constante) con SQL: sus literales de texto juntos, en orden. Lee el C#
        /// con un lector simple: salta comentarios y literales de carácter; los huecos <c>{…}</c> de un texto
        /// interpolado quedan como texto.
        /// </summary>
        internal static IEnumerable<MiembroSql> MiembrosConSql(string cs)
        {
            var inicios = DeclaracionMiembro.Matches(cs).Cast<Match>()
                .Select(m => new { m.Index, Nombre = NombreDe(m.Value), EsConstante = Regex.IsMatch(m.Value, @"\b(const|readonly)\s+string\b") }).ToList();
            var literales = Literales(cs, out string codigo);
            var porMiembro = new Dictionary<int, (StringBuilder Todo, StringBuilder Fijo)>();
            foreach (var lit in literales)
            {
                int idx = -1;
                for (int i = 0; i < inicios.Count && inicios[i].Index <= lit.Inicio; i++) idx = i;
                if (idx < 0) continue;
                if (!porMiembro.TryGetValue(idx, out var m))
                    porMiembro[idx] = m = (new StringBuilder(), new StringBuilder());
                m.Todo.Append(m.Todo.Length > 0 ? " " : "").Append(lit.Texto);
                // Un fragmento agregado bajo una condición no limita; una consulta entera (con su SELECT)
                // dentro de un if sigue siendo la consulta que corre, con sus propios límites.
                bool condicional = EsCondicional(codigo, lit.Inicio) && !Regex.IsMatch(lit.Texto, @"(?i)\bSELECT\b");
                if (!condicional)
                    m.Fijo.Append(m.Fijo.Length > 0 ? " " : "").Append(lit.Texto);
            }

            // Constantes (const / static readonly string) de SQL del mismo archivo que el miembro usa por su nombre (p. ej. un SELECT común
            // + el WHERE que arma el método): su texto se suma al del miembro.
            var constantes = porMiembro.Where(p => inicios[p.Key].EsConstante && Regex.IsMatch(p.Value.Todo.ToString(), @"(?i)\bSELECT\b"))
                                       .ToDictionary(p => p.Key, p => p.Value);
            foreach (var p in porMiembro.OrderBy(x => x.Key))
            {
                int desde = inicios[p.Key].Index;
                int hasta = p.Key + 1 < inicios.Count ? inicios[p.Key + 1].Index : codigo.Length;
                string cuerpo = codigo.Substring(desde, hasta - desde);
                string previo = "", previoFijo = "";
                foreach (var c in constantes.Where(c => c.Key != p.Key))
                {
                    if (!Regex.IsMatch(cuerpo, @"\b" + Regex.Escape(inicios[c.Key].Nombre) + @"\b")) continue;
                    previo += c.Value.Todo + " ";
                    previoFijo += c.Value.Fijo + " ";
                }
                string todo = previo + p.Value.Todo;
                if (!Regex.IsMatch(todo, @"(?i)\bSELECT\b")) continue;
                yield return new MiembroSql { Nombre = inicios[p.Key].Nombre, Sql = todo, SqlFijo = previoFijo + p.Value.Fijo };
            }
        }
        private static string NombreDe(string declaracion)
        {
            // Sin modificadores ni tipo de retorno: el primer identificador seguido de ( = { ;
            var m = NombreMiembro.Match(declaracion);
            return m.Success ? m.Groups[1].Value : declaracion.Trim();
        }

        private static readonly Regex Condicion = new Regex(@"\b(if|else)\b|\?");

        /// <summary>
        /// ¿El literal en <paramref name="pos"/> se agrega solo a veces? Sí si su sentencia está en un
        /// <c>if</c>/<c>else</c> sin llaves o en un <c>?:</c>, o si el bloque <c>{ }</c> que la contiene es el de
        /// un <c>if</c>/<c>else</c>. <paramref name="codigo"/> es el C# con literales y comentarios en blanco.
        /// </summary>
        internal static bool EsCondicional(string codigo, int pos)
        {
            int j = pos - 1;
            while (j >= 0 && codigo[j] != ';' && codigo[j] != '{' && codigo[j] != '}') j--;
            if (Condicion.IsMatch(codigo.Substring(j + 1, pos - j - 1))) return true;
            if (j < 0 || codigo[j] != '{') return false;
            int k = j - 1;
            while (k >= 0 && codigo[k] != ';' && codigo[k] != '{' && codigo[k] != '}') k--;
            return Regex.IsMatch(codigo.Substring(k + 1, j - k - 1), @"\b(if|else)\b");
        }

        /// <summary>
        /// Cada literal de texto de C# fuera de comentarios (inicio y contenido); en <paramref name="codigo"/>
        /// devuelve el fuente con los literales y comentarios en blanco (mismo largo), para buscar llaves y
        /// <c>;</c> sin tropezar con los de dentro del SQL.
        /// </summary>
        private static List<(int Inicio, string Texto)> Literales(string cs, out string codigo)
        {
            var lista = new List<(int, string)>();
            var blanco = cs.ToCharArray();
            void Borrar(int desde, int hasta) { for (int x = desde; x < hasta && x < blanco.Length; x++) if (blanco[x] != '\n') blanco[x] = ' '; }
            int i = 0, n = cs.Length;
            while (i < n)
            {
                char c = cs[i];
                if (c == '/' && i + 1 < n && cs[i + 1] == '/') { int d = i; while (i < n && cs[i] != '\n') i++; Borrar(d, i); continue; }
                if (c == '/' && i + 1 < n && cs[i + 1] == '*') { int d = i; int f = cs.IndexOf("*/", i + 2, StringComparison.Ordinal); i = f < 0 ? n : f + 2; Borrar(d, i); continue; }
                if (c == '\'')
                {
                    int d = i;
                    i++;
                    while (i < n && cs[i] != '\'') i += cs[i] == '\\' ? 2 : 1;
                    i++;
                    Borrar(d, i);
                    continue;
                }
                bool verbatim = false;
                int inicio = i;
                if ((c == '@' || c == '$') && i + 1 < n && (cs[i + 1] == '"' || ((cs[i + 1] == '@' || cs[i + 1] == '$') && i + 2 < n && cs[i + 2] == '"')))
                {
                    verbatim = c == '@' || cs[i + 1] == '@';
                    i += cs[i + 1] == '"' ? 1 : 2;
                    c = cs[i];
                }
                if (c != '"') { i++; continue; }
                i++;
                var sb = new StringBuilder();
                while (i < n)
                {
                    if (verbatim)
                    {
                        if (cs[i] == '"' && i + 1 < n && cs[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        if (cs[i] == '"') break;
                    }
                    else
                    {
                        if (cs[i] == '\\' && i + 1 < n) { sb.Append(cs[i + 1] == 'n' ? '\n' : cs[i + 1]); i += 2; continue; }
                        if (cs[i] == '"' || cs[i] == '\n') break;
                    }
                    sb.Append(cs[i]);
                    i++;
                }
                i++;
                Borrar(inicio, i);
                lista.Add((inicio, sb.ToString()));
            }
            codigo = new string(blanco);
            return lista;
        }
        // ── Clasificación ──────────────────────────────────────────────────

        private static string SinComentariosNiOver(string sql)
        {
            sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            sql = Regex.Replace(sql, @"--[^\r\n]*", " ");
            return Regex.Replace(sql, @"(?i)\bOVER\s*\([^)]*\)", " ");
        }

        internal static bool EsListado(string sql)
        {
            string s = SinComentariosNiOver(sql);
            return Regex.IsMatch(s, @"(?is)\bSELECT\b.*\bFROM\b") && Regex.IsMatch(s, @"(?i)\bORDER\s+BY\b");
        }

        /// <summary>¿El parámetro es opcional en la consulta (<c>@p IS NULL</c>, <c>@p &lt;= 0</c>, <c>@p = 0</c>, <c>@p = ''</c>)?</summary>
        private static bool EsOpcional(string s, string parametro)
        {
            string p = Regex.Escape(parametro) + @"\b";
            return Regex.IsMatch(s, "(?i)" + p + @"\s+IS\s+NULL|" + p + @"\s*(<=|<|=)\s*(0\b|'')");
        }

        internal static bool TieneLimiteOAcotada(string sql)
        {
            string s = SinComentariosNiOver(sql);
            if (Regex.IsMatch(s, @"(?i)\bTOP\b|\bOFFSET\b")) return true;
            if (Regex.IsMatch(s, @"(?i)STRING_SPLIT\s*\(\s*@|\bIN\s*\(\s*\{")) return true;
            // Rango de fechas o período: cuenta si el valor es fijo (p. ej. DATEADD) o un parámetro obligatorio.
            var rangos = Regex.Matches(s, @"(?i)\b[\w.]*fecha\w*\s*(?:>=|<=|<|>|\bBETWEEN\b)\s*(@\w+)?|\bBETWEEN\s+(@\w+)|\b(?:Year|Month|anio|mes)\s*=\s*(@\w+)");
            foreach (Match m in rangos)
            {
                string p = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : null;
                if (p == null || !EsOpcional(s, p)) return true;
            }
            foreach (Match m in Regex.Matches(s, @"(?i)([\w.]+)\s*=\s*(@\w+)"))
            {
                string columna = m.Groups[1].Value.Split('.').Last();
                if (!Regex.IsMatch(columna, @"(?i)^(id|numero)|id$")) continue;
                if (!EsOpcional(s, m.Groups[2].Value)) return true;
            }
            return false;
        }
    }
}