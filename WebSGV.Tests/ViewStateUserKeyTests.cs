using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-3: CA5368 está suprimida en PaginaBase porque Site.Master pone ViewStateUserKey en las
    /// páginas con sesión. Una página que hereda de PaginaBase sin master no recibe la clave, así que
    /// no puede tener postback de servidor (botones con OnClick/OnCommand, AutoPostBack ni lógica
    /// en IsPostBack distinta de <c>if (IsPostBack) return;</c>): hoy solo FirmarLiquidacion y
    /// ConformarLiquidacion, que trabajan con [WebMethod] JSON.
    /// </summary>
    public class ViewStateUserKeyTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly Regex HeredaDePaginaBase = new Regex(@"\bclass\s+\w+\s*:\s*PaginaBase\b");
        private static readonly Regex DirectivaConMaster = new Regex(@"<%@\s*Page\b[^>]*\bMasterPageFile\s*=", RegexOptions.IgnoreCase);
        private static readonly Regex EventoDeServidor = new Regex(
            @"<asp:[^>]*?\b(On\w*(Click|Command)|On\w*Changed|AutoPostBack\s*=\s*""true"")", RegexOptions.IgnoreCase);
        private static readonly Regex UsoIsPostBack = new Regex(@"\bIsPostBack\b");
        private static readonly Regex SoloSalir = new Regex(@"if\s*\(\s*IsPostBack\s*\)\s*return\s*;");

        [Fact]
        public void PaginasBaseSinMaster_NoTienenPostbackDeServidor()
        {
            string views = Path.Combine(RaizWeb(), "Views");
            var sinMaster = Directory.GetFiles(views, "*.aspx.cs", SearchOption.AllDirectories)
                .Where(cs => HeredaDePaginaBase.IsMatch(File.ReadAllText(cs)))
                .Where(cs => !DirectivaConMaster.IsMatch(File.ReadAllText(cs.Substring(0, cs.Length - 3))))
                .ToList();

            // Que la prueba de verdad recorra las páginas: estas dos no tienen master.
            Assert.Contains(sinMaster, cs => cs.EndsWith("FirmarLiquidacion.aspx.cs"));
            Assert.Contains(sinMaster, cs => cs.EndsWith("ConformarLiquidacion.aspx.cs"));

            var problemas = sinMaster.SelectMany(cs =>
            {
                string nombre = Path.GetFileName(cs);
                string codigo = File.ReadAllText(cs);
                string marcado = File.ReadAllText(cs.Substring(0, cs.Length - 3));
                var lista = EventoDeServidor.Matches(marcado).Cast<Match>()
                    .Select(m => nombre.Replace(".cs", "") + ": control con " + m.Groups[1].Value).ToList();
                if (UsoIsPostBack.Matches(codigo).Count != SoloSalir.Matches(codigo).Count)
                    lista.Add(nombre + ": usa IsPostBack para algo distinto de 'if (IsPostBack) return;'");
                return lista;
            }).ToList();

            Assert.True(problemas.Count == 0,
                "Páginas sin master (sin ViewStateUserKey) con postback de servidor; usar Site.Master o asignar la clave en OnInit: "
                + string.Join("; ", problemas));
        }
    }
}
