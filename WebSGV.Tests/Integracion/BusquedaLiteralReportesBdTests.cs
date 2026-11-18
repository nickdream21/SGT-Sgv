using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Solo lectura (bloque 6, B6-4): las búsquedas del tablero de seguimiento, del reporte de cargas y de los viajes
    /// activos de Reportes son literales —"%", "_" y "[" no actúan como comodines— y una búsqueda normal (sin esos
    /// caracteres) trae lo mismo que la consulta anterior. "Literal" se calcula aparte con CHARINDEX, que no tiene
    /// comodines.
    /// </summary>
    public class BusquedaLiteralReportesBdTests
    {
        private static readonly string[] Comodines = { "%", "_", "[", "%%", "[a-z]%", "a_b" };

        public BusquedaLiteralReportesBdTests() => BaseDePruebas.Preparar();

        private static int Contar(string sql, params System.Data.SqlClient.SqlParameter[] p) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, p));

        // ------------------------------------------------------------------ Tablero de seguimiento

        private const string ColumnasTablero = "se.cliente|se.carreta|se.tracto1|se.tracto2|se.conductorOrigen|se.conductorDestino|se.lugarEntrega";

        private static string CondicionTablero(string plantilla) =>
            string.Join(" OR ", ColumnasTablero.Split('|').Select(c => string.Format(plantilla, c)));

        /// <summary>Con texto, el tablero busca en todas las fechas; con historial y sin planta ni cliente no hay más filtros.</summary>
        private static int TableroServicio(string t) =>
            SeguimientoViajesService.ListarTableroConTotal(
                new FiltroTablero { Desde = DateTime.Today, Hasta = DateTime.Today, Texto = t, IncluirHistorial = true }, 1).Total;

        private static int TableroLiteral(string t) => Contar(
            "SELECT COUNT(*) FROM SeguimientoExportacion se WHERE se.activo = 1 AND (" + CondicionTablero("CHARINDEX(@t, {0}) > 0") + ")",
            DbHelper.Param("@t", t));

        /// <summary>La consulta de antes de B6-4: el texto tal cual dentro de '%...%'.</summary>
        private static int TableroAnterior(string t) => Contar(
            "SELECT COUNT(*) FROM SeguimientoExportacion se WHERE se.activo = 1 AND (" + CondicionTablero("{0} LIKE @like") + ")",
            DbHelper.Param("@like", "%" + t + "%"));

        [FactBD]
        public void Tablero_comodines_buscan_el_texto_literal()
        {
            foreach (string t in Comodines)
                Assert.True(TableroLiteral(t) == TableroServicio(t), $"Tablero con '{t}': literal {TableroLiteral(t)}, servicio {TableroServicio(t)}");

            // Antes "_" encontraba cualquier viaje con un cliente (o placa, chofer...) no vacío.
            Assert.True(TableroAnterior("_") > TableroServicio("_"), "La BD de pruebas no muestra la diferencia con '_'.");
        }

        [FactBD]
        public void Tablero_texto_normal_trae_lo_mismo_que_antes()
        {
            DataTable muestra = DbHelper.ConsultarTabla(@"
                SELECT TOP 1 LEFT(RTRIM(carreta), 4) AS carreta, LEFT(RTRIM(conductorOrigen), 5) AS conductor, LEFT(RTRIM(cliente), 4) AS cliente
                FROM SeguimientoExportacion
                WHERE activo = 1 AND LEN(RTRIM(carreta)) >= 4 AND LEN(RTRIM(conductorOrigen)) >= 5 AND LEN(RTRIM(cliente)) >= 4
                ORDER BY idSeguimiento DESC");
            Assert.True(muestra.Rows.Count == 1, "La BD de pruebas no tiene viajes de seguimiento con qué comparar.");
            foreach (string t in muestra.Rows[0].ItemArray.Select(Convert.ToString).Select(s => s.Trim()))
            {
                int antes = TableroAnterior(t);
                Assert.True(antes > 0, $"'{t}' debería encontrar algo.");
                Assert.True(antes == TableroServicio(t), $"Tablero con '{t}': antes {antes}, ahora {TableroServicio(t)}");
            }
        }

        // ------------------------------------------------------------------ Reporte de cargas

        private const string JoinsCargas = @"
            FROM Despachos d
            JOIN Cliente cl        ON cl.idCliente = d.idCliente
            LEFT JOIN Factura f    ON f.idFactura = d.idFactura
            LEFT JOIN Conductor co ON co.idConductor = d.idConductor
            LEFT JOIN Tracto t     ON t.idTracto = d.idTracto
            LEFT JOIN Carreta ca   ON ca.idCarreta = d.idCarreta
            WHERE (d.activo = 1 OR (d.activo = 0 AND d.estadoDespacho = 'ANULADO')) AND d.tipoOperacion = 'CARGA'";

        private const string NombreCargas =
            "LTRIM(RTRIM(co.nombre)) + ' ' + LTRIM(RTRIM(co.apPaterno)) + ' ' + ISNULL(LTRIM(RTRIM(co.apMaterno)), '')";

        private static FiltroCargas FiltroCargas(string texto, string pedido) => new FiltroCargas
        {
            Desde = new DateTime(2000, 1, 1), Hasta = new DateTime(2100, 12, 31), Texto = texto, Pedido = pedido
        };

        private static int CargasServicio(string texto, string pedido) =>
            ReporteCargasService.Despachos(FiltroCargas(texto, pedido)).Rows.Count;

        /// <summary>Lo que significa el filtro leído literalmente (placa comparada sin guiones; pedido "empieza por").</summary>
        private static int CargasLiteral(string texto, string pedido) => Contar("SELECT COUNT(*) " + JoinsCargas + @"
                AND (@p IS NULL OR LEFT(d.numeroPedido, LEN(@p)) = @p)
                AND (@t IS NULL
                     OR CHARINDEX(REPLACE(@t, '-', ''), REPLACE(t.placaTracto, '-', '')) > 0
                     OR CHARINDEX(REPLACE(@t, '-', ''), REPLACE(ca.placaCarreta, '-', '')) > 0
                     OR CHARINDEX(@t, " + NombreCargas + @") > 0
                     OR CHARINDEX(@t, d.guiaRemitente) > 0
                     OR CHARINDEX(@t, f.numeroFactura) > 0)",
            DbHelper.Param("@t", texto), DbHelper.Param("@p", pedido));

        /// <summary>El filtro de antes de B6-4, copiado tal cual (el patrón se armaba en el SQL).</summary>
        private static int CargasAnterior(string texto, string pedido) => Contar("SELECT COUNT(*) " + JoinsCargas + @"
                AND (@pedido IS NULL OR d.numeroPedido LIKE @pedido + '%')
                AND (@texto IS NULL
                     OR REPLACE(t.placaTracto, '-', '') LIKE '%' + REPLACE(@texto, '-', '') + '%'
                     OR REPLACE(ca.placaCarreta, '-', '') LIKE '%' + REPLACE(@texto, '-', '') + '%'
                     OR " + NombreCargas + @" LIKE '%' + @texto + '%'
                     OR d.guiaRemitente LIKE '%' + @texto + '%'
                     OR f.numeroFactura LIKE '%' + @texto + '%')",
            DbHelper.Param("@texto", texto), DbHelper.Param("@pedido", pedido));

        [FactBD]
        public void Cargas_comodines_buscan_el_texto_literal()
        {
            Assert.True(CargasServicio(null, null) > 1, "La BD de pruebas no tiene despachos de carga con qué comparar.");
            foreach (string t in Comodines)
            {
                Assert.True(CargasLiteral(t, null) == CargasServicio(t, null),
                    $"Texto '{t}': literal {CargasLiteral(t, null)}, servicio {CargasServicio(t, null)}");
                Assert.True(CargasLiteral(null, t) == CargasServicio(null, t),
                    $"Pedido '{t}': literal {CargasLiteral(null, t)}, servicio {CargasServicio(null, t)}");
            }
            Assert.True(CargasAnterior("_", null) > CargasServicio("_", null), "La BD de pruebas no muestra la diferencia con '_'.");
            Assert.True(CargasAnterior(null, "_") > CargasServicio(null, "_"), "La BD de pruebas no muestra la diferencia con '_' en el pedido.");
        }

        [FactBD]
        public void Cargas_texto_y_pedido_normales_traen_lo_mismo_que_antes()
        {
            DataTable muestra = DbHelper.ConsultarTabla(@"
                SELECT TOP 1 RTRIM(t.placaTracto) AS placa, RTRIM(ca.placaCarreta) AS carreta, LEFT(RTRIM(d.guiaRemitente), 6) AS guia,
                       RTRIM(co.apPaterno) AS apellido, LEFT(RTRIM(d.numeroPedido), 5) AS pedido
                FROM Despachos d
                JOIN Tracto t ON t.idTracto = d.idTracto
                JOIN Carreta ca ON ca.idCarreta = d.idCarreta
                JOIN Conductor co ON co.idConductor = d.idConductor
                WHERE d.tipoOperacion = 'CARGA' AND d.activo = 1 AND t.placaTracto LIKE '%-%'
                  AND LEN(RTRIM(d.guiaRemitente)) >= 6 AND LEN(RTRIM(d.numeroPedido)) >= 5 AND LEN(RTRIM(co.apPaterno)) >= 3
                ORDER BY d.idDespacho DESC");
            Assert.True(muestra.Rows.Count == 1, "La BD de pruebas no tiene despachos de carga completos con qué comparar.");
            DataRow r = muestra.Rows[0];
            string placa = Convert.ToString(r["placa"]);
            var textos = new[]
            {
                placa, placa.Replace("-", ""), Convert.ToString(r["carreta"]), Convert.ToString(r["carreta"]).Replace("-", ""),
                Convert.ToString(r["guia"]), Convert.ToString(r["apellido"])
            };
            foreach (string t in textos)
            {
                int antes = CargasAnterior(t, null);
                Assert.True(antes > 0, $"'{t}' debería encontrar algo.");
                Assert.True(antes == CargasServicio(t, null), $"Texto '{t}': antes {antes}, ahora {CargasServicio(t, null)}");
            }
            string pedido = Convert.ToString(r["pedido"]);
            Assert.True(CargasAnterior(null, pedido) > 0);
            Assert.Equal(CargasAnterior(null, pedido), CargasServicio(null, pedido));
        }

        // ------------------------------------------------------------------ Reportes: viajes activos

        [FactBD]
        public void Viajes_activos_por_conductor_buscan_el_texto_literal()
        {
            DataTable todos = ReportesOrdenesViajeService.ObtenerViajesActivos("", "TODOS");
            foreach (string t in Comodines)
            {
                DataTable filtrados = ReportesOrdenesViajeService.ObtenerViajesActivos(t, "TODOS");
                foreach (DataRow f in filtrados.Rows)
                {
                    // nombre o apPaterno contienen t → el Conductor (CONCAT de ellos) también.
                    bool literal = Convert.ToString(f["Conductor"]).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                                || Convert.ToString(f["DNI"]).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                    Assert.True(literal, $"'{t}' trajo el viaje {f["IdViaje"]} ({f["Conductor"]}), que no lo contiene.");
                }
                Assert.True(filtrados.Rows.Count <= todos.Rows.Count);
            }

            int conGuionBajo = Contar("SELECT COUNT(*) FROM Conductor WHERE CHARINDEX('_', CONCAT(nombre, apPaterno, DNI)) > 0");
            if (todos.Rows.Count > 0 && conGuionBajo == 0)
                Assert.Equal(0, ReportesOrdenesViajeService.ObtenerViajesActivos("_", "TODOS").Rows.Count);   // antes: todos
        }
    }
}
