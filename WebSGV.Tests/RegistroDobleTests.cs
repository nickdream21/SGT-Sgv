using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-10: un <c>catch</c> no registra el mismo error dos veces. <c>MensajeErrorHelper.ParaUsuario</c> ya registra el
    /// error técnico (con la plantilla de contexto que se le pase) y deja el de negocio como información; un
    /// <c>LogSGV.Error(</c> en el mismo <c>catch</c> lo duplicaba y además dejaba como ERR el aviso de negocio.
    /// </summary>
    public class RegistroDobleTests
    {
        /// <summary>"archivo:línea" de cada catch cuyo bloque llama a la vez a LogSGV.Error( y a ParaUsuario(.</summary>
        internal static List<string> CatchConRegistroDoble(string relativo, string codigo)
        {
            var raiz = CSharpSyntaxTree.ParseText(codigo).GetRoot();
            var resultado = new List<string>();
            foreach (CatchClauseSyntax c in raiz.DescendantNodes().OfType<CatchClauseSyntax>())
            {
                var llamadas = c.Block.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(i => i.Expression.ToString())
                    .ToList();
                // RegistrarError es el envoltorio de RegistroDespacho (LogSGV.Error con el contexto): E6-13 quitó 14 pares
                // RegistrarError + ParaUsuario que este detector no veía.
                bool logError = llamadas.Any(e => e == "LogSGV.Error" || e.EndsWith(".LogSGV.Error", System.StringComparison.Ordinal)
                    || e == "RegistrarError");
                bool paraUsuario = llamadas.Any(e => e == "ParaUsuario" || e.EndsWith(".ParaUsuario", System.StringComparison.Ordinal)
                    || e == "ParaUsuarioOTexto" || e.EndsWith(".ParaUsuarioOTexto", System.StringComparison.Ordinal));
                if (logError && paraUsuario)
                    resultado.Add(relativo + ":" + (c.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
            }
            return resultado;
        }

        [Fact]
        public void NingunCatch_RegistraConLogSGVErrorYParaUsuario()
        {
            string raiz = FuentesWeb.RaizWeb();
            var dobles = FuentesWeb.Archivos(raiz, "*.cs")
                .SelectMany(f => CatchConRegistroDoble(FuentesWeb.Relativo(raiz, f), File.ReadAllText(f)))
                .ToList();
            Assert.True(dobles.Count == 0,
                "Estos catch registran el error dos veces (LogSGV.Error y ParaUsuario): pasa el contexto a " +
                "MensajeErrorHelper.ParaUsuario(ex, \"plantilla {Valor}\", valor) y quita el LogSGV.Error (" + dobles.Count + "):\n" +
                string.Join("\n", dobles));
        }

        [Fact]
        public void Detector_EncuentraElRegistroDobleYNoLosDemas()
        {
            Assert.Single(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { } catch (System.Exception ex) { LogSGV.Error(ex, \"x\"); lbl.Text = MensajeErrorHelper.ParaUsuario(ex); } } }"));
            Assert.Empty(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { } catch (System.Exception ex) { lbl.Text = MensajeErrorHelper.ParaUsuario(ex, \"x {Id}\", 1); } } }"));
            Assert.Empty(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { } catch (System.Exception ex) { LogSGV.Error(ex, \"x\"); } } }"));
            Assert.Single(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { } catch (System.Exception ex) { RegistrarError(\"x\", ex); lbl.Text = MensajeErrorHelper.ParaUsuario(ex); } } }"));
            Assert.Single(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { } catch (System.Exception ex) { LogSGV.Error(ex, \"x\"); t = MensajeErrorHelper.ParaUsuarioOTexto(ex, \"a\", \"b\"); } } }"));
            // Uno en el try y otro en el catch no es doble.
            Assert.Empty(CatchConRegistroDoble("a.cs",
                "class A { void M() { try { LogSGV.Error(\"x\"); } catch (System.Exception ex) { lbl.Text = MensajeErrorHelper.ParaUsuario(ex); } } }"));
        }
    }
}
