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
    /// E6-13: un <c>catch</c> del código web (WebSGV/**/*.cs) registra el error (LogSGV, <c>ParaUsuario</c> o un envoltorio que
    /// los llama) o lo relanza; si no, tapa una falla de verdad. Los que no hacen ninguna de las dos cosas a propósito están
    /// en <see cref="Legitimos"/>, una lista cerrada por "archivo::Miembro" con cuántos tiene ese miembro: la prueba falla si
    /// aparece uno nuevo (agrégale registro, o súmalo aquí con el motivo) o si uno de la lista desaparece sin ajustarla.
    /// Un <c>catch (ThreadAbortException)</c> no cuenta: es el aviso normal de <c>Response.End</c>/<c>Redirect</c>.
    /// </summary>
    public class CatchSinRegistroTests
    {
        /// <summary>Llamadas que dejan el error en el log (por el final del nombre de la expresión llamada).</summary>
        internal static readonly string[] LlamadasQueRegistran =
        {
            "LogSGV.Error", "LogSGV.Advertencia", "LogSGV.Info", "LogSGV.InfoCon", "LogSGV.AdvertenciaCon",
            "ParaUsuario", "ParaUsuarioOTexto",
            "RegistrarError",            // RegistroDespacho: LogSGV.Error con el contexto
            "RegistrarFalloAuditoria",   // AuditoriaHelper: LogSGV.Error de la auditoría que falló
            "ResponderErrorExportacion", // ReportesOrdenesViaje: ParaUsuario y la respuesta de texto
            "MostrarErrorOperacion",     // RegistroDespacho: ParaUsuarioOTexto y el aviso
            "AvisarUnaVez",              // LecturaSegura: LogSGV.Advertencia una vez por columna
        };

        /// <summary>
        /// Los <c>catch</c> que a propósito no registran ni relanzan (2026-11-25). Motivos, por grupos:
        /// el propio log y la medición (registrar dentro del log entraría en bucle o perdería la petición); limpieza de
        /// temporales y cierres en un <c>finally</c> o tras un error ya registrado; deshacer una transacción (el error
        /// original sigue su camino); la respuesta ya enviada al navegador (pipeline); lecturas tolerantes que devuelven
        /// "no se sabe" y el llamador decide (fechas, números, JSON de un borrador del navegador, archivos opcionales).
        /// </summary>
        internal static readonly Dictionary<string, int> Legitimos = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // El propio log, la medición y el aviso de errores: registrar aquí dentro entraría en bucle o tumbaría la
            // petición; ya están dentro de un manejador de errores.
            ["Global.asax.cs::Application_AcquireRequestState"] = 1,    // sesión de solo lectura: vale el dato de la BD
            ["Global.asax.cs::Application_Error"] = 4,                  // sin sesión en el pipeline (3) y Application_Error nunca lanza
            ["Global.asax.cs::Application_PreSendRequestHeaders"] = 1,  // cabeceras ya enviadas
            ["CspInforme.ashx.cs::ProcessRequest"] = 1,                 // un informe CSP mal formado responde 204
            ["Helpers/AuditoriaHelper.cs::RegistrarFalloAuditoria"] = 1, // el log de la auditoría que falló
            ["Helpers/ArchivosTemporalesHelper.cs::LimpiarVencidos"] = 1, // el log de la limpieza que falló
            ["Helpers/ControlIntentosLogin.cs::PasarARespaldo"] = 1,    // el log del respaldo del login
            ["Helpers/ErrorMetodoPagina.cs::AlLanzarse"] = 1,           // manejador de FirstChanceException
            ["Helpers/ErrorMetodoPagina.cs::RegistrarSiFallo"] = 1,     // el registro del WebMethod es secundario
            ["Helpers/ErrorMetodoPagina.cs::TieneCabeceraJsonError"] = 1, // modo clásico: sin cabeceras que leer
            ["Helpers/LogSGV.cs::Inicializar"] = 1,                     // un log que no arranca no impide arrancar la app
            ["Helpers/LogSGV.cs::Leer"] = 2,                            // enriquecedor del log: sin sesión, nunca lanza
            ["Helpers/MedicionPeticiones.cs::AlDetener"] = 1,
            ["Helpers/MedicionPeticiones.cs::AlTenerSesion"] = 1,
            ["Helpers/MedicionPeticiones.cs::AlTerminar"] = 1,
            ["Services/Operacion/AvisoErrores.cs::Lugar"] = 1,          // sin reflexión de la pila
            ["Services/Operacion/AvisoErrores.cs::Notificar"] = 1,      // sin sesión en el pipeline
            ["Services/Operacion/InformeCsp.cs::Analizar"] = 1,         // JSON del informe CSP ilegible
            ["Services/Operacion/InformeCsp.cs::Procesar"] = 1,         // un informe nunca da 500
            ["Site.Master.cs::ScriptManager1_AsyncPostBackError"] = 1,  // dentro del manejador de error del ScriptManager
            // La respuesta, la sesión o el contexto web no están disponibles (pipeline, pruebas fuera de IIS).
            ["Helpers/AuditoriaHelper.cs::ObtenerIPCliente"] = 1,
            ["Helpers/CompresionRespuesta.cs::AplicarSiCorresponde"] = 1, // quitar una cabecera que ya no está
            ["Helpers/SesionVigenteHelper.cs::InvalidarCache"] = 1,
            ["Services/FirmaService.cs::UrlVerificacion"] = 2,
            ["Services/Liquidaciones/ArchivoOrdenViajeService.cs::RutaFisica"] = 1,
            ["Services/PdfOrdenViajeService.cs::RutaLogo"] = 1,
            ["Site.Master.cs::TieneSesionActivaLocal"] = 1,
            ["Site.Master.cs::UsuarioLogin"] = 1,
            ["Views/ReportesOrdenesViaje.aspx.cs::ResponderTexto"] = 1, // el archivo ya empezó a enviarse
            // Deshacer o reintentar: el error original sigue su camino (lo relanza o registra el catch de afuera).
            ["Helpers/DbHelper.cs::EnTransaccion"] = 1,                 // rollback que también falla
            ["Services/Conductor/LiquidacionConductorService.cs::EnviarLiquidacion"] = 1, // rollback que también falla
            ["Services/PdfOrdenViajeService.cs::GuardarSinSobrescribir"] = 2, // nombre ya usado; borrar el PDF a medias
            ["Services/Facturas/FacturaFleteService.cs::GuardarSinSobrescribir"] = 1, // nombre ya usado: el siguiente
            ["Services/GpsIntegracion/OnwayAuthCacheService.cs::ConBloqueoDeRenovacion"] = 1, // el bloqueo se libera al cerrar
            ["Helpers/ControlIntentosLogin.cs::ConReintentos"] = 1,     // bloqueo mutuo (1205): se reintenta
            ["Helpers/ControlIntentosLogin.cs::Devolver"] = 1,          // pasa al respaldo, que registra
            ["Helpers/ControlIntentosLogin.cs::Quitar"] = 1,
            ["Helpers/ControlIntentosLogin.cs::Reservar"] = 1,
            // Temporales y cachés en disco: un archivo que no se pudo borrar se borra en la próxima limpieza.
            ["Services/GpsIntegracion/HistorialGpsCache.cs::LimpiarAntiguos"] = 1,
            ["Views/Exportacion/ImportarSeguimiento.aspx.cs::btnImportar_Click"] = 1,
            // Lecturas tolerantes: "no se sabe" o "no es válido" y el llamador decide o lo muestra (no es una falla).
            ["Helpers/ExcelSubidoHelper.cs::ErrorDirectorio"] = 1,      // Excel ilegible: mensaje al usuario
            ["Helpers/ExcelSubidoHelper.cs::Validar"] = 1,
            ["Services/Exportacion/LimitesImportacionSeguimiento.cs::ValidarDescomprimido"] = 1,
            ["Services/Despachos/Importacion/ImportacionPedidoService.cs::AbrirZip"] = 1,
            ["Helpers/PasswordHelper.cs::Derivar"] = 3,                 // sin CNG: PBKDF2 administrado (MarcarCngNoDisponible registra)
            ["Helpers/PasswordHelper.cs::TryLeer"] = 1,                 // hash mal formado: no verifica
            ["Helpers/PoliticaContrasena.cs::PalabrasEmpresa"] = 1,     // sin configuración de empresa
            ["Helpers/ValidadorPng.cs::Analizar"] = 1,                  // PNG inválido
            ["Helpers/ValidadorPng.cs::LogoUtilizable"] = 2,            // logo que no se puede leer: sin logo
            ["Models/Despachos/BandejaDocumentosModels.cs::Pdf"] = 2,   // temporal ya borrado
            ["Services/Despachos/Importacion/LectorDocumentosAduana.cs::Fecha"] = 1, // fecha imposible en el PDF
            ["Services/Despachos/ListaDespachosService.cs::ParsearIdLoteVirtual"] = 1, // id de lote que no es virtual
            ["Services/Liquidaciones/LiquidacionCalculos.cs::DecodificarPngBase64"] = 1, // trazo de firma inválido: se rechaza
            ["Services/Operacion/EstadoSistemaService.cs::ConvertidorAPeru"] = 1, // hora inexistente al cambiar de horario
            ["Services/Operacion/EstadoSistemaService.cs::LeerArchivo"] = 2, // log en uso: se cuenta como no leído
            ["Services/Operacion/EstadoSistemaService.cs::LeerConsultas"] = 1, // sin Query Store: bloque vacío
            ["Services/Usuarios/SegundoFactorReglas.cs::ConsultarToleraFaltaTabla"] = 1, // registra con el delegado registrar
            ["Services/Usuarios/SegundoFactorReglas.cs::VerificarCodigoRecuperacion"] = 1, // código mal formado
            ["Services/Usuarios/SegundoFactorService.cs::ClaveConfigurada"] = 1,
            ["Services/GpsIntegracion/ReporteHistorialGpsService.cs::ObtenerCanbus"] = 1, // equipo sin CANBUS
            ["Services/Consultas/ConsultaDocumentoService.cs::ObtenerHttp"] = 1, // 4xx/5xx: decide el servicio
            // Errores de negocio que se muestran (no son fallas del sistema) o que ya registró otro.
            ["Services/Consultas/ConsultaDocumentoService.cs::Buscar"] = 1,
            ["Services/GpsIntegracion/RutaViajeService.cs::Tramo"] = 1,
            ["Services/GpsIntegracion/ReporteHistorialGpsService.cs::Generar"] = 1, // OnwayApiClient ya registró la respuesta
            ["Services/Exportacion/ActualizacionGpsAutomatica.cs::Ejecutar"] = 1, // _textosTrasError = TextosTrasError (ParaUsuario)
            ["Views/DashboardConductor.aspx.cs::MantenerPestanaLiquidar"] = 1, // el envío ya mostró y registró el error
            ["Views/ListaDespachos.aspx.cs::LeerMontoOpcional"] = 1,    // monto inválido: a la lista de errores del formulario
            ["Views/ListaDespachos.aspx.cs::ValidarMontoObligatorio"] = 1,
            ["Views/Login.aspx.cs::btnLogin_Click"] = 1,                // ValidarUsuario ya registró el error
        };

        /// <summary>"archivo::Miembro" de cada catch que no registra ni relanza (uno por catch, con repetidos).</summary>
        internal static List<string> CatchSinRegistro(string relativo, string codigo)
        {
            var raiz = CSharpSyntaxTree.ParseText(codigo).GetRoot();
            var resultado = new List<string>();
            foreach (CatchClauseSyntax c in raiz.DescendantNodes().OfType<CatchClauseSyntax>())
            {
                string tipo = c.Declaration?.Type.ToString() ?? "";
                if (tipo == "ThreadAbortException" || tipo.EndsWith(".ThreadAbortException", StringComparison.Ordinal))
                    continue;
                if (Relanza(c.Block) || Registra(c.Block))
                    continue;
                resultado.Add(relativo + "::" + Miembro(c));
            }
            return resultado;
        }

        /// <summary>
        /// Un <c>throw</c> que es sentencia directa del bloque catch: siempre relanza. Uno dentro de un if, un try o una
        /// lambda anidados no cuenta (el catch puede terminar sin relanzar ni registrar).
        /// </summary>
        private static bool Relanza(BlockSyntax bloque) =>
            bloque.Statements.OfType<ThrowStatementSyntax>().Any();

        private static bool Registra(BlockSyntax bloque) =>
            bloque.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i =>
            {
                string e = SinGenericos(i.Expression).ToString();
                return LlamadasQueRegistran.Any(l => e == l || e.EndsWith("." + l, StringComparison.Ordinal));
            });

        private static ExpressionSyntax SinGenericos(ExpressionSyntax e)
        {
            if (e is MemberAccessExpressionSyntax m && m.Name is GenericNameSyntax g)
                return m.WithName(SyntaxFactory.IdentifierName(g.Identifier));
            if (e is GenericNameSyntax g2)
                return SyntaxFactory.IdentifierName(g2.Identifier);
            return e;
        }

        /// <summary>Nombre del miembro que contiene el catch (método, constructor "Clase.ctor", propiedad, operador).</summary>
        private static string Miembro(SyntaxNode n)
        {
            foreach (SyntaxNode a in n.Ancestors())
            {
                switch (a)
                {
                    case MethodDeclarationSyntax m: return m.Identifier.Text;
                    case ConstructorDeclarationSyntax k: return k.Identifier.Text + ".ctor";
                    case PropertyDeclarationSyntax p: return p.Identifier.Text;
                    case IndexerDeclarationSyntax _: return "this[]";
                    case OperatorDeclarationSyntax o: return "operator" + o.OperatorToken.Text;
                    case ConversionOperatorDeclarationSyntax _: return "conversion";
                    case FieldDeclarationSyntax f: return f.Declaration.Variables.First().Identifier.Text;
                    case EventDeclarationSyntax ev: return ev.Identifier.Text;
                }
            }
            return "(tipo)";
        }

        private static Dictionary<string, int> Contar(IEnumerable<string> claves) =>
            claves.GroupBy(k => k, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        [Fact]
        public void CatchSinRegistroNiRelanzamiento_SoloLosDeLaListaCerrada()
        {
            string raiz = FuentesWeb.RaizWeb();
            var hoy = Contar(FuentesWeb.Archivos(raiz, "*.cs")
                .SelectMany(f => CatchSinRegistro(FuentesWeb.Relativo(raiz, f), File.ReadAllText(f))));

            var nuevos = hoy.Where(kv => !Legitimos.TryGetValue(kv.Key, out int n) || kv.Value > n)
                .Select(kv => kv.Key + " (" + kv.Value + (Legitimos.TryGetValue(kv.Key, out int t) ? ", la lista dice " + t : "") + ")")
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
            var sobran = Legitimos.Where(kv => !hoy.TryGetValue(kv.Key, out int n) || n < kv.Value)
                .Select(kv => kv.Key + " (la lista dice " + kv.Value + ", hay " + (hoy.TryGetValue(kv.Key, out int h) ? h : 0) + ")")
                .OrderBy(s => s, StringComparer.Ordinal).ToList();

            Assert.True(nuevos.Count == 0 && sobran.Count == 0,
                "catch que no registran ni relanzan fuera de la lista cerrada (registra con LogSGV / MensajeErrorHelper.ParaUsuario, " +
                "relanza, o si es legítimo súmalo a CatchSinRegistroTests.Legitimos con su motivo).\n" +
                "Nuevos o con más de los anotados (" + nuevos.Count + "):\n" + string.Join("\n", nuevos) +
                "\nEn la lista pero ya no están o son menos (ajusta la lista) (" + sobran.Count + "):\n" + string.Join("\n", sobran) +
                "\n\nLista de hoy:\n" + string.Join("\n", hoy.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => "            [\"" + kv.Key + "\"] = " + kv.Value + ",")));
        }

        [Fact]
        public void ListaCerrada_TieneTotalConocido()
        {
            // La cifra de los documentos (HOJA_DE_RUTA_CALIDAD, E6-13): si cambia la lista, cambia también aquí.
            Assert.Equal(TotalLegitimos, Legitimos.Values.Sum());
        }

        internal const int TotalLegitimos = 80;

        [Fact]
        public void Detector_DistingueRegistroRelanzamientoYSilencio()
        {
            const string antes = "class A { void M() { try { } ";
            const string despues = " } }";
            Assert.Equal(new[] { "a.cs::M" }, CatchSinRegistro("a.cs", antes + "catch { }" + despues));
            Assert.Equal(new[] { "a.cs::M" }, CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { x = 1; }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { LogSGV.Advertencia(ex, \"x\"); }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { WebSGV.Helpers.LogSGV.Error(ex, \"x\"); }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { lbl.Text = MensajeErrorHelper.ParaUsuario(ex); }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { t = MensajeErrorHelper.ParaUsuarioOTexto(ex, \"a\", \"b\"); }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch { throw; }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { throw new ErrorNegocioException(\"x\", ex); }" + despues));
            Assert.Empty(CatchSinRegistro("a.cs", antes + "catch (System.Threading.ThreadAbortException) { }" + despues));
            // Un throw anidado (if, lambda) no asegura el relanzamiento.
            Assert.Single(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { if (x) throw; }" + despues));
            Assert.Single(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { f = () => throw ex; }" + despues));
            // Un método que se llama igual pero no es del log no cuenta (p. ej. Registrar de auditoría).
            Assert.Single(CatchSinRegistro("a.cs", antes + "catch (System.Exception ex) { AuditoriaHelper.Registrar(\"a\", \"b\"); }" + despues));
            Assert.Equal(new[] { "a.cs::A.ctor" }, CatchSinRegistro("a.cs", "class A { A() { try { } catch { } } }"));
        }
    }
}
