using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-20 (M-174): ningún método público de WebSGV sin una sola referencia. Se mide por nombre, con Roslyn:
    /// un método público (sin <c>override</c> ni implementación explícita de interfaz, y sin <c>[WebMethod]</c>,
    /// que llama el navegador) está en uso si su nombre aparece como identificador en algún C# de WebSGV, de sus
    /// pruebas o de herramientas/ (sin contar las declaraciones de métodos con ese nombre), o como palabra en el
    /// marcado y los scripts del sitio (.aspx, .ascx, .Master, .ashx, .asax, Scripts/paginas/*.js: handlers de
    /// eventos y PageMethods). Es conservador: dos métodos con el mismo nombre se cubren uno al otro.
    /// Lo que solo llama el framework o un programa de afuera va en una lista cerrada con su motivo; una excepción
    /// que ya no hace falta también falla.
    /// </summary>
    public class MetodosPublicosSinUsoTests
    {
        /// <summary>"ruta relativa a WebSGV::Clase.Método" → por qué no tiene referencias en el código.</summary>
        private static readonly Dictionary<string, string> Excepciones = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "CspInforme.ashx.cs::CspInforme.ProcessRequest",                     "IHttpHandler: lo llama IIS al pedir CspInforme.ashx" },
            { "Salud.ashx.cs::Salud.ProcessRequest",                               "IHttpHandler: lo llama IIS al pedir Salud.ashx" },
            { "Views/Exportacion/GpsAutomatico.ashx.cs::GpsAutomatico.ProcessRequest", "IHttpHandler: lo llama IIS al pedir GpsAutomatico.ashx" },
            { "Services/PdfOrdenViajeService.cs::OrdenViajeDocumento.Compose",     "IDocument de QuestPDF: lo llama QuestPDF al generar el PDF" },
            { "Services/Reportes/ReporteTabular.cs::ReporteTabularPdf.Compose",    "IDocument de QuestPDF: lo llama QuestPDF al generar el PDF" },
        };

        private static string Repo => Path.GetDirectoryName(FuentesWeb.RaizWeb());

        private static List<string> CodigoCs() =>
            FuentesWeb.Archivos(FuentesWeb.RaizWeb(), "*.cs")
                .Concat(FuentesWeb.Archivos(Path.Combine(Repo, "WebSGV.Tests"), "*.cs"))
                .Concat(FuentesWeb.Archivos(Path.Combine(Repo, "herramientas"), "*.cs"))
                .ToList();

        private static List<string> MarcadoYScripts()
        {
            string raiz = FuentesWeb.RaizWeb();
            return FuentesWeb.Archivos(raiz, "*.aspx", "*.ascx", "*.Master", "*.ashx", "*.asax")
                .Concat(FuentesWeb.Archivos(Path.Combine(raiz, "Scripts", "paginas"), "*.js"))
                .ToList();
        }

        private static bool EsCandidato(MethodDeclarationSyntax m) =>
            m.Modifiers.Any(SyntaxKind.PublicKeyword)
            && !m.Modifiers.Any(SyntaxKind.OverrideKeyword)
            && m.ExplicitInterfaceSpecifier == null
            && !(m.Parent is InterfaceDeclarationSyntax)
            && !m.AttributeLists.SelectMany(a => a.Attributes).Any(a => a.Name.ToString().EndsWith("WebMethod", StringComparison.Ordinal));

        /// <summary>Lo que se usa en un conjunto de C#: nombres sueltos, "Clase.Nombre" y los sueltos de cada archivo.</summary>
        internal sealed class Usos
        {
            /// <summary>Todo nombre usado (calificado o no), sin las declaraciones de métodos.</summary>
            public readonly HashSet<string> Nombres = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>"Clase.Nombre" de cada acceso <c>X.Clase.Nombre</c>.</summary>
            public readonly HashSet<string> Calificados = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Por archivo, los nombres usados sin calificar (llamadas desde la misma clase).</summary>
            public readonly Dictionary<string, HashSet<string>> SueltosPorArchivo = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Clases traídas con <c>using static</c> (sus miembros se usan sin calificar).</summary>
            public readonly HashSet<string> ConUsingStatic = new HashSet<string>(StringComparer.Ordinal);

            public void Leer(string archivo, SyntaxNode arbol)
            {
                var sueltos = new HashSet<string>(StringComparer.Ordinal);
                SueltosPorArchivo[archivo] = sueltos;
                foreach (UsingDirectiveSyntax u in arbol.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => !u.StaticKeyword.IsKind(SyntaxKind.None)))
                    ConUsingStatic.Add(Ultimo(u.Name));
                foreach (SimpleNameSyntax n in arbol.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    string nombre = n.Identifier.ValueText;
                    Nombres.Add(nombre);
                    if (n.Parent is MemberAccessExpressionSyntax ma && ma.Name == n)
                    {
                        string clase = Ultimo(ma.Expression);
                        if (clase != null) Calificados.Add(clase + "." + nombre);
                    }
                    else if (!(n.Parent is MemberBindingExpressionSyntax) && !(n.Parent is QualifiedNameSyntax q && q.Right == n))
                        sueltos.Add(nombre);
                }
            }

            private static string Ultimo(SyntaxNode expresion)
            {
                switch (expresion)
                {
                    case SimpleNameSyntax s: return s.Identifier.ValueText;
                    case MemberAccessExpressionSyntax m: return m.Name.Identifier.ValueText;
                    case QualifiedNameSyntax q: return q.Right.Identifier.ValueText;
                    case AliasQualifiedNameSyntax a: return a.Name.Identifier.ValueText;
                    default: return null;
                }
            }
        }

        /// <summary>
        /// ¿Tiene alguna referencia? Un método estático de una clase estática (que no sea de extensión) solo cuenta si
        /// se llama como <c>Clase.Método</c>, sin calificar en el archivo de su clase o con <c>using static</c>: así
        /// <c>RolesHelper.EsAdmin()</c> no queda cubierto por <c>SecurityHelper.EsAdmin()</c>. Los demás, por nombre.
        /// </summary>
        internal static bool EstaEnUso(MethodDeclarationSyntax m, string archivo, Usos usos, string marcado)
        {
            string nombre = m.Identifier.ValueText;
            var clase = m.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            bool estricto = clase != null && clase.Modifiers.Any(SyntaxKind.StaticKeyword)
                && m.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !(m.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) ?? false)
                && !usos.ConUsingStatic.Contains(clase.Identifier.ValueText);
            if (!estricto)
                return usos.Nombres.Contains(nombre) || Regex.IsMatch(marcado, @"\b" + Regex.Escape(nombre) + @"\b");
            string calificado = clase.Identifier.ValueText + "." + nombre;
            return usos.Calificados.Contains(calificado)
                || (usos.SueltosPorArchivo.TryGetValue(archivo, out var sueltos) && sueltos.Contains(nombre))
                || Regex.IsMatch(marcado, @"\b" + Regex.Escape(calificado) + @"\b");
        }

        /// <summary>Métodos públicos de WebSGV sin ninguna referencia: "ruta::Clase.Método".</summary>
        internal static List<string> SinReferencias()
        {
            string raiz = FuentesWeb.RaizWeb();
            var usos = new Usos();
            var candidatos = new List<(string Archivo, MethodDeclarationSyntax Metodo)>();

            foreach (string archivo in CodigoCs())
            {
                SyntaxNode arbol = CSharpSyntaxTree.ParseText(File.ReadAllText(archivo)).GetRoot();
                usos.Leer(archivo, arbol);
                if (archivo.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    candidatos.AddRange(arbol.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(EsCandidato).Select(m => (archivo, m)));
            }

            string marcado = string.Join("\n", MarcadoYScripts().Select(File.ReadAllText));
            return candidatos
                .Where(c => !EstaEnUso(c.Metodo, c.Archivo, usos, marcado))
                .Select(c => FuentesWeb.Relativo(raiz, c.Archivo) + "::"
                    + (c.Metodo.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "?")
                    + "." + c.Metodo.Identifier.ValueText)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();
        }

        [Fact]
        public void MetodosPublicos_TodosTienenAlgunaReferencia()
        {
            var sinUso = SinReferencias().Where(c => !Excepciones.ContainsKey(c)).ToList();
            Assert.True(sinUso.Count == 0,
                "Métodos públicos sin ninguna referencia (bórrelos, o si solo los llama el framework o un programa de afuera, " +
                "agréguelos a Excepciones con su motivo):\n" + string.Join("\n", sinUso));
        }

        [Fact]
        public void Excepciones_SiguenHaciendoFalta()
        {
            var sinUso = new HashSet<string>(SinReferencias(), StringComparer.Ordinal);
            var sobran = Excepciones.Keys.Where(k => !sinUso.Contains(k)).ToList();
            Assert.True(sobran.Count == 0, "Excepción que ya no corresponde a un método público sin referencias (quítela):\n" + string.Join("\n", sobran));
        }

        [Fact]
        public void Medidor_CuentaUsosYDistingueLaClaseEnLasEstaticas()
        {
            const string a =
                "class Pagina { public void Usado() { } public void Solo() { } void B() { Usado(); } public override string ToString() => \"\";\n" +
                "  [WebMethod] public static int Ajax() => 1; public void Handler_Click() { } }\n" +
                "static class Roles { public static bool EsAdmin() => true; public static bool EsJefe() => Interno(); public static bool Interno() => true;\n" +
                "  /// <see cref=\"EsJefe\"/>\n  public static bool EsCitado() => true; public static bool Ext(this string s) => true; }\n" +
                "static class Seguridad { public static bool EsAdmin() => true; public static bool EsJefe() => true; }";
            const string b = "class Otra { void X() { Seguridad.EsAdmin(); WebSGV.Roles.EsJefe(); \"a\".Ext(); } }";
            SyntaxNode arbolA = CSharpSyntaxTree.ParseText(a).GetRoot();
            var usos = new Usos();
            usos.Leer("a.cs", arbolA);
            usos.Leer("b.cs", CSharpSyntaxTree.ParseText(b).GetRoot());
            const string marcado = "<asp:Button OnClick=\"Handler_Click\" runat=\"server\" />";

            var enUso = arbolA.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(EsCandidato)
                .ToDictionary(m => m.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ValueText + "." + m.Identifier.ValueText,
                              m => EstaEnUso(m, "a.cs", usos, marcado));

            Assert.False(enUso.ContainsKey("Pagina.Ajax"));          // [WebMethod]: lo llama el navegador
            Assert.False(enUso.ContainsKey("Pagina.ToString"));      // override
            Assert.True(enUso["Pagina.Usado"]);
            Assert.False(enUso["Pagina.Solo"]);
            Assert.True(enUso["Pagina.Handler_Click"]);              // enlazado desde el marcado
            Assert.False(enUso["Roles.EsAdmin"]);                    // solo se llama Seguridad.EsAdmin
            Assert.True(enUso["Seguridad.EsAdmin"]);
            Assert.True(enUso["Roles.EsJefe"]);                      // calificado con el espacio de nombres
            Assert.True(enUso["Roles.Interno"]);                     // sin calificar, desde su clase
            Assert.False(enUso["Roles.EsCitado"]);                   // un cref no es una llamada
            Assert.True(enUso["Roles.Ext"]);                         // de extensión: por nombre
        }
    }
}
