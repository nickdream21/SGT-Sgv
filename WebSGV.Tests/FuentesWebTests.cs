using System;
using System.IO;
using System.Linq;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// El lector común de fuentes (E6-4) encuentra los controles .ascx junto con las páginas: hoy no hay ninguno, así
    /// que sin esta prueba los barridos de marcado podrían dejarlos fuera sin que nada lo notara.
    /// </summary>
    public class FuentesWebTests
    {
        [Fact]
        public void Lector_DevuelvePaginasYControlesSinDesignerNiObjNiBin()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sgv-e6-4-" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (string rel in new[]
                {
                    "P.aspx", "P.aspx.cs", "P.aspx.designer.cs",
                    "Sub/C.ascx", "Sub/C.ascx.cs", "Sub/C.ascx.designer.cs",
                    "obj/X.aspx", "obj/X.ascx.cs", "bin/Y.ascx", "Sub/bin/Z.aspx",
                    "Otro.aspxx", "H.ashx.cs", "Site.Master"
                })
                {
                    string f = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(f));
                    File.WriteAllText(f, "");
                }

                Assert.Equal(new[] { "P.aspx", "Sub/C.ascx" },
                    FuentesWeb.Marcado(dir).Select(f => FuentesWeb.Relativo(dir, f)).ToArray());
                Assert.Equal(new[] { "P.aspx.cs", "Sub/C.ascx.cs" },
                    FuentesWeb.CodigoDeMarcado(dir).Select(f => FuentesWeb.Relativo(dir, f)).ToArray());
                Assert.Equal(new[] { "H.ashx.cs", "P.aspx.cs", "Sub/C.ascx.cs" },
                    FuentesWeb.Archivos(dir, "*.cs").Select(f => FuentesWeb.Relativo(dir, f)).ToArray());
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Theory]
        [InlineData("Views/Controles/Carga.ascx", true)]
        [InlineData("Views/Controles/Carga.ascx.cs", true)]
        [InlineData("Views/RegistroDespacho.aspx", false)]
        [InlineData("Views/RegistroDespacho.aspx.cs", false)]
        [InlineData("Site.Master", false)]
        public void EsControl_SoloLosAscx(string ruta, bool esperado) =>
            Assert.Equal(esperado, FuentesWeb.EsControl(ruta));

        [Fact]
        public void LaRaizWebTieneLasVistas() =>
            Assert.True(Directory.Exists(Path.Combine(FuentesWeb.RaizWeb(), "Views")));
    }
}
