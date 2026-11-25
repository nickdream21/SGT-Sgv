using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Trinquete del tamaño del código (E6-4). Todo archivo C# de WebSGV de más de <see cref="LineasArchivo"/>
    /// líneas y todo método de más de <see cref="LineasMetodo"/> tiene un tope con la cifra de hoy
    /// (2026-11-25); al partirlo (E6-21 a E6-28) se baja el tope o se quita la fila. Falla si uno sube, si
    /// aparece uno nuevo por encima del límite, si uno baja sin ajustar su tope (la cifra de los documentos
    /// debe ser cierta) o si uno de la lista ya no existe. Los métodos se miden con Roslyn: de la firma
    /// (atributos incluidos) a la llave que lo cierra, sin los comentarios de antes.
    /// </summary>
    public class TamanoCodigoTests
    {
        internal const int LineasArchivo = 800;
        internal const int LineasMetodo = 80;

        /// <summary>Archivos de más de 800 líneas (ruta relativa a WebSGV/).</summary>
        private static readonly Dictionary<string, int> TopesArchivo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/RegistroDespacho.aspx.cs"] = 3424,
            ["Views/ListaDespachos.aspx.cs"] = 2120,
            ["Services/Despachos/BandejaDocumentosService.cs"] = 1549,
            ["Views/ReportesOrdenesViaje.aspx.cs"] = 1479,
            ["Services/PdfOrdenViajeService.cs"] = 1307,
            ["Views/DashboardConductor.aspx.cs"] = 1285,
            ["Views/LiquidacionesPendientes.aspx.cs"] = 1077,
            ["Views/BandejaDocumentos.aspx.cs"] = 912,
            ["Services/Despachos/Importacion/ImportacionPedidoService.cs"] = 901,
            ["Views/GestionUsuarios.aspx.cs"] = 849,
            ["Services/Liquidaciones/LiquidacionesPendientesService.cs"] = 860,
        };

        /// <summary>
        /// Métodos de más de 80 líneas: "archivo::Método" (con los tipos de los parámetros si el nombre se repite
        /// en el archivo; el constructor es "Clase.ctor").
        /// </summary>
        private static readonly Dictionary<string, int> TopesMetodo = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Services/Liquidaciones/LiquidacionesPendientesService.cs::ObtenerDetalleLiquidacion"] = 337,
            ["Services/GpsIntegracion/SeguimientoExportacionGpsService.cs::ConsultarYActualizar"] = 300,
            ["Views/DashboardConductor.aspx.cs::btnEnviarLiquidacion_Click"] = 186,
            ["Views/GestionUsuarios.aspx.cs::btnGuardarUsuario_Click"] = 181,
            ["Services/Reportes/ReporteTabular.cs::EscribirHoja"] = 179,
            ["Views/Exportacion/ImportarSeguimiento.aspx.cs::LeerExcel"] = 178,
            ["Services/GpsIntegracion/TrazabilidadTercerosService.cs::Detectar"] = 162,
            ["Services/Reportes/ReportesOrdenesViajeService.cs::ObtenerReportePersonalizado"] = 160,
            ["Services/Liquidaciones/LiquidacionesPendientesService.cs::EnviarAConformidad"] = 145,
            ["Services/Despachos/ProgramacionCarga.cs::Proponer"] = 144,
            ["Services/Despachos/FinalizarLoteService.cs::Finalizar"] = 135,
            ["Services/Despachos/ProgramacionDespachoService.cs::Proponer"] = 135,
            ["Services/Conductor/ListaChoferesService.cs::Comparar"] = 133,
            ["Services/GpsIntegracion/TrazabilidadDescargaService.cs::DetectarTramoDescarga"] = 124,
            ["Views/ReportesOrdenesViaje.aspx.cs::ObtenerSeccionGastos"] = 122,
            ["Services/Despachos/BandejaDocumentosService.cs::Registrar"] = 120,
            ["Services/Facturas/FacturaFleteService.cs::Conciliar"] = 114,
            ["Views/RegistroDespacho.aspx.cs::btnAplicarDescarga_Click"] = 113,
            ["Views/LiquidacionesPendientes.aspx.cs::EnviarAConformidad"] = 108,
            ["Views/Exportacion/SeguimientoViaje.aspx.cs::Cargar"] = 107,
            ["Services/Despachos/ExpedienteDocumentosService.cs::Obtener"] = 103,
            ["Views/Login.aspx.cs::btnLogin_Click"] = 102,
            ["Services/Facturas/FacturaFleteService.cs::Reporte"] = 101,
            ["Views/RegistroDespacho.aspx.cs::CrearNuevoLote"] = 101,
            ["Views/DashboardConductor.aspx.cs::ConstruirInputLiquidacion"] = 100,
            ["Views/RegistroDespacho.aspx.cs::btnProgramarDespachos_Click"] = 99,
            ["Views/DetalleOrdenViaje.aspx.cs::Mostrar"] = 99,
            ["Views/Login.aspx.cs::Page_Load"] = 98,
            ["Views/VerificarDocumento.aspx.cs::Verificar"] = 98,
            ["Views/BandejaDocumentos.aspx.cs::btnRegistrar_Click"] = 97,
            ["Views/RegistroDespacho.aspx.cs::ValidarConfiguracionBase"] = 95,
            ["Services/Despachos/ListaDespachosService.cs::GuardarCambiosLote"] = 94,
            ["Services/Conductor/DashboardConductorService.cs::CargarFinancieros"] = 93,
            ["Views/Login.aspx.cs::ValidarUsuario"] = 93,
            ["Services/GpsIntegracion/ReporteHistorialGpsService.cs::Generar"] = 92,
            ["Views/RegistroDespacho.aspx.cs::btnLeerProgramacionDescarga_Click"] = 91,
            ["Services/Despachos/Importacion/ImportacionPedidoService.cs::PdfsDelZip"] = 91,
            ["Views/RegistroClientes.aspx.cs::btnActualizarCliente_Click"] = 90,
            ["Global.asax.cs::Application_AcquireRequestState"] = 90,
            ["Services/Despachos/BandejaDocumentosService.cs::ResolverCpic"] = 89,
            ["Services/Despachos/Importacion/ImportacionPedidoService.cs::Leer(IEnumerable<ArchivoSubido>, ArchivoSubido, Dictionary<string, ArchivoSubido>, PresupuestoLectura)"] = 89,
            ["Views/ConformarLiquidacion.aspx.cs::ObtenerLiquidacion"] = 85,
            ["Views/RegistroDespacho.aspx.cs::btnLeerDocumentos_Click"] = 84,
            ["Global.asax.cs::Application_Error"] = 85,
            ["Views/BandejaDocumentos.aspx.cs::MostrarVistaPrevia"] = 84,
            ["Services/Despachos/BandejaDocumentosService.cs::AplicarEleccion"] = 83,
            ["Helpers/ReautenticacionHelper.cs::Verificar(int, string, PropositoReautenticacion, Func<int, string>, ControlIntentosLogin, Action<string, string, int, string>)"] = 82,
            ["Views/ReportesOrdenesViaje.aspx.cs::LeerFiltrosPersonalizado"] = 82,
            ["Services/Despachos/ProgramacionDescarga.cs::Proponer"] = 81,
        };

        // ── Medición ─────────────────────────────────────────────────────────────────────────

        /// <summary>Todo .cs de WebSGV sin obj/, bin/ ni *.designer.cs.</summary>
        private static List<string> Archivos(string raiz) => FuentesWeb.Archivos(raiz, "*.cs");

        internal static int ContarLineas(string texto)
        {
            if (texto.Length == 0) return 0;
            int n = texto.Count(c => c == '\n');
            return texto.EndsWith("\n", StringComparison.Ordinal) ? n : n + 1;
        }

        /// <summary>Métodos del código con su clave (sin el archivo) y su largo en líneas.</summary>
        internal static List<KeyValuePair<string, int>> Metodos(string codigo)
        {
            var raiz = CSharpSyntaxTree.ParseText(codigo).GetRoot();
            var metodos = raiz.DescendantNodes().OfType<BaseMethodDeclarationSyntax>()
                .Select(m => new { Nodo = m, Nombre = Nombre(m) })
                .ToList();
            var repetidos = new HashSet<string>(metodos.GroupBy(m => m.Nombre).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);

            var resultado = new List<KeyValuePair<string, int>>();
            var usadas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in metodos)
            {
                string clave = repetidos.Contains(m.Nombre)
                    ? m.Nombre + "(" + string.Join(", ", m.Nodo.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "")) + ")"
                    : m.Nombre;
                string unica = clave;
                for (int i = 2; !usadas.Add(unica); i++) unica = clave + "#" + i;
                var lineas = m.Nodo.GetLocation().GetLineSpan();
                resultado.Add(new KeyValuePair<string, int>(unica, lineas.EndLinePosition.Line - lineas.StartLinePosition.Line + 1));
            }
            return resultado;
        }

        private static string Nombre(BaseMethodDeclarationSyntax m)
        {
            switch (m)
            {
                case MethodDeclarationSyntax x: return x.Identifier.Text;
                case ConstructorDeclarationSyntax x: return x.Identifier.Text + ".ctor";
                case DestructorDeclarationSyntax x: return "~" + x.Identifier.Text;
                case OperatorDeclarationSyntax x: return "operator " + x.OperatorToken.Text;
                case ConversionOperatorDeclarationSyntax x: return "operator " + x.Type;
                default: return m.Kind().ToString();
            }
        }

        private static Dictionary<string, int> MedirArchivos(string raiz) =>
            Archivos(raiz)
                .Select(f => new { Ruta = FuentesWeb.Relativo(raiz, f), N = ContarLineas(File.ReadAllText(f)) })
                .Where(x => x.N > LineasArchivo)
                .ToDictionary(x => x.Ruta, x => x.N, StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, int> MedirMetodos(string raiz) =>
            Archivos(raiz)
                .SelectMany(f => Metodos(File.ReadAllText(f))
                    .Select(m => new { Clave = FuentesWeb.Relativo(raiz, f) + "::" + m.Key, N = m.Value }))
                .Where(x => x.N > LineasMetodo)
                .ToDictionary(x => x.Clave, x => x.N, StringComparer.Ordinal);

        /// <summary>Diferencias entre lo medido (solo lo que pasa del límite) y los topes; vacía si todo cuadra.</summary>
        internal static List<string> Diferencias(Dictionary<string, int> medido, IDictionary<string, int> topes,
            int limite, Func<string, bool> existe, string que)
        {
            var fallas = new List<string>();
            foreach (var m in medido.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (!topes.TryGetValue(m.Key, out int tope))
                    fallas.Add(m.Key + ": " + que + " nuevo de " + m.Value + " líneas (más de " + limite + "); pártalo");
                else if (m.Value > tope)
                    fallas.Add(m.Key + ": creció de " + tope + " a " + m.Value + " líneas; pártalo en vez de alargarlo");
                else if (m.Value < tope)
                    fallas.Add(m.Key + ": bajó, ajusta el tope a " + m.Value);
            }
            foreach (var t in topes.Where(t => !medido.ContainsKey(t.Key)).OrderBy(t => t.Key, StringComparer.Ordinal))
                fallas.Add(existe(t.Key)
                    ? t.Key + ": bajó a " + limite + " líneas o menos; quita la fila"
                    : t.Key + ": ya no existe; quita la fila");
            return fallas;
        }

        private static string Lista(Dictionary<string, int> medido) =>
            string.Join("\n", medido.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => "            [\"" + x.Key + "\"] = " + x.Value + ","));

        // ── Reglas ───────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ArchivosDeMasDe800Lineas_NoCrecenYLosTopesSonLaCifraDeHoy()
        {
            string raiz = FuentesWeb.RaizWeb();
            var medido = MedirArchivos(raiz);
            var fallas = Diferencias(medido, TopesArchivo, LineasArchivo,
                r => File.Exists(Path.Combine(raiz, r.Replace('/', Path.DirectorySeparatorChar))), "archivo");
            Assert.True(fallas.Count == 0, "Archivos C# de más de " + LineasArchivo + " líneas (" + medido.Count + "):\n"
                + string.Join("\n", fallas) + "\n\nCifra de hoy:\n" + Lista(medido));
        }

        [Fact]
        public void MetodosDeMasDe80Lineas_NoCrecenYLosTopesSonLaCifraDeHoy()
        {
            string raiz = FuentesWeb.RaizWeb();
            var medido = MedirMetodos(raiz);
            var todos = new HashSet<string>(Archivos(raiz)
                .SelectMany(f => Metodos(File.ReadAllText(f)).Select(m => FuentesWeb.Relativo(raiz, f) + "::" + m.Key)), StringComparer.Ordinal);
            var fallas = Diferencias(medido, TopesMetodo, LineasMetodo, todos.Contains, "método");
            Assert.True(fallas.Count == 0, "Métodos de más de " + LineasMetodo + " líneas (" + medido.Count + "):\n"
                + string.Join("\n", fallas) + "\n\nCifra de hoy:\n" + Lista(medido));
        }

        // ── El medidor mismo ─────────────────────────────────────────────────────────────────

        [Fact]
        public void Medidor_CuentaDeLaFirmaALaLlaveYSeparaLasSobrecargas()
        {
            const string codigo =
                "class A\n" +
                "{\n" +
                "    // comentario que no cuenta\n" +
                "    [Obsoleto]\n" +
                "    public void Uno()\n" +
                "    {\n" +
                "        int x = 1;\n" +
                "    }\n" +
                "    public A() { }\n" +
                "    int Dos(int a) => a;\n" +
                "    int Dos(string b, int c)\n" +
                "    {\n" +
                "        return c;\n" +
                "    }\n" +
                "    class B { void Uno2() { } }\n" +
                "}\n";
            var m = Metodos(codigo).ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal(5, m["Uno"]);
            Assert.Equal(1, m["A.ctor"]);
            Assert.Equal(1, m["Dos(int)"]);
            Assert.Equal(4, m["Dos(string, int)"]);
            Assert.Equal(1, m["Uno2"]);
            Assert.Equal(5, m.Count);

            Assert.Equal(16, ContarLineas(codigo));
            Assert.Equal(2, ContarLineas("a\nb"));
            Assert.Equal(0, ContarLineas(""));
        }

        [Fact]
        public void Diferencias_PideAjustarAlBajarYFallaAlCrecerOConUnoNuevo()
        {
            var topes = new Dictionary<string, int>(StringComparer.Ordinal) { ["a::X"] = 100, ["a::Y"] = 100, ["a::Z"] = 100, ["a::W"] = 90, ["a::V"] = 90 };
            var medido = new Dictionary<string, int>(StringComparer.Ordinal) { ["a::X"] = 100, ["a::Y"] = 101, ["a::Z"] = 95, ["a::N"] = 81 };
            var fallas = Diferencias(medido, topes, 80, k => k == "a::W", "método");
            Assert.Equal(5, fallas.Count);
            Assert.Contains("a::Y: creció de 100 a 101 líneas; pártalo en vez de alargarlo", fallas);
            Assert.Contains("a::Z: bajó, ajusta el tope a 95", fallas);
            Assert.Contains("a::N: método nuevo de 81 líneas (más de 80); pártalo", fallas);
            Assert.Contains("a::W: bajó a 80 líneas o menos; quita la fila", fallas);
            Assert.Contains("a::V: ya no existe; quita la fila", fallas);
        }
    }
}
