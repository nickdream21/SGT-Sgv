using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using WebSGV.Services.Conductor;
using Xunit;
using C = WebSGV.Services.Conductor.ListaChoferesService.ConductorRegistrado;
using T = WebSGV.Services.Conductor.ListaChoferesService.TipoCambio;

namespace WebSGV.Tests
{
    // Datos inventados: la forma es la de la "lista de choferes actuales" de transporte.
    public class ListaChoferesTests
    {
        [Fact]
        public void LeerTexto_FilasPegadasDeExcel()
        {
            var filas = ListaChoferesService.LeerTexto(
                "\tCHOFER\tNACIONALIDAD\tLICENCIA\tDNI\r\n" +
                "1\tPEDRO PABLO RAMOS QUISPE\tPERUANA\tB-01234567\t01234567\r\n" +
                "2\tANA LUZ DE LA CRUZ PAZ\tPERUANA\tA-87654321\t87654321\r\n");

            Assert.Equal(2, filas.Count);
            Assert.Equal("PEDRO PABLO RAMOS QUISPE", filas[0].Nombre);
            Assert.Equal("PERUANA", filas[0].Nacionalidad);
            Assert.Equal("B01234567", filas[0].Licencia);
            Assert.Equal("01234567", filas[0].Dni);
            Assert.Empty(filas[0].Avisos);
        }

        [Fact]
        public void LeerTexto_ConEspaciosYLicenciaConEspacio()
        {
            var f = ListaChoferesService.LeerTexto("49 JUAN PEREZ SOTO PERUANA B- 40000001 40000001").Single();
            Assert.Equal("JUAN PEREZ SOTO", f.Nombre);
            Assert.Equal("B40000001", f.Licencia);
            Assert.Equal("40000001", f.Dni);
        }

        [Fact]
        public void NueveDigitos_EsCarnetDeExtranjeria()
        {
            // La lista dice PERUANA por error: el DNI tiene 8 dígitos, el carnet 9
            var f = ListaChoferesService.LeerTexto("41\tLUIS PAZ ROJAS\tPERUANA\tB-001234567\t001234567").Single();
            Assert.Null(f.Dni);
            Assert.Equal("001234567", f.Carnet);
            Assert.Equal("B001234567", f.Licencia);
            Assert.Single(f.Avisos);
        }

        [Fact]
        public void NacionalidadExtranjera_EsCarnetSinAviso()
        {
            var f = ListaChoferesService.LeerTexto("52\tLUIS PAZ ROJAS\tVENEZOLANA\tB-001234567\t1234567").Single();
            Assert.Equal("001234567", f.Carnet);   // Excel numérico perdió los ceros
            Assert.Empty(f.Avisos);
        }

        [Fact]
        public void Comparar_CarnetGuardadoComoDni_ProponeCorregirDocumento()
        {
            var registrados = new List<C>
            {
                new C { Id = 7, Dni = "01234567", Nombre = "LUIS", ApPaterno = "PAZ", ApMaterno = "ROJAS", Licencia = "B01234567", Activo = true },
            };
            var r = ListaChoferesService.Comparar(Lista("41\tLUIS PAZ ROJAS\tVENEZOLANA\tB-001234567\t001234567"), registrados);

            Assert.Empty(r.Problemas);
            Assert.Contains(r.Cambios, c => c.Tipo == T.Documento && c.IdConductor == 7 && c.Carnet == "001234567" && c.MarcadoPorDefecto);
            Assert.Contains(r.Cambios, c => c.Tipo == T.Licencia && c.IdConductor == 7 && c.Licencia == "B001234567");
            Assert.DoesNotContain(r.Cambios, c => c.Tipo == T.Desactivar || c.Tipo == T.Registrar);
        }

        [Fact]
        public void Comparar_ExtranjeroYaRegistradoConCarnet_SinCambios()
        {
            var registrados = new List<C>
            {
                new C { Id = 8, Carnet = "001234567", Nombre = "LUIS", ApPaterno = "PAZ", ApMaterno = "ROJAS", Licencia = "B001234567", Activo = true },
            };
            var r = ListaChoferesService.Comparar(Lista("41\tLUIS PAZ ROJAS\tVENEZOLANA\tB-001234567\t001234567"), registrados);

            Assert.Equal(1, r.SinCambios);
            Assert.Empty(r.Cambios);
        }

        [Fact]
        public void Comparar_ExtranjeroNuevo_SeRegistraConCarnet()
        {
            var r = ListaChoferesService.Comparar(Lista("41\tLUIS PAZ ROJAS\tVENEZOLANA\tB-001234567\t001234567"), new List<C>());
            var alta = Assert.Single(r.Cambios);
            Assert.Equal(T.Registrar, alta.Tipo);
            Assert.Null(alta.Dni);
            Assert.Equal("001234567", alta.Carnet);
        }

        [Fact]
        public void DniComoNumeroEnExcel_RecuperaElCero()
        {
            var f = ListaChoferesService.InterpretarCeldas(new[] { "3", "LUIS PAZ ROJAS", "PERUANA", "B-01234567", "1234567" }, 3);
            Assert.Equal("01234567", f.Dni);
            Assert.Empty(f.Avisos);
        }

        [Fact]
        public void LeerExcel_PrimeraHoja()
        {
            using (var libro = new XLWorkbook())
            using (var ms = new MemoryStream())
            {
                var h = libro.AddWorksheet("Lista");
                h.Cell(1, 2).Value = "CHOFER"; h.Cell(1, 3).Value = "NACIONALIDAD"; h.Cell(1, 4).Value = "LICENCIA"; h.Cell(1, 5).Value = "DNI";
                h.Cell(2, 1).Value = 1; h.Cell(2, 2).Value = "PEDRO RAMOS QUISPE"; h.Cell(2, 3).Value = "PERUANA";
                h.Cell(2, 4).Value = "B-01234567"; h.Cell(2, 5).Value = 1234567;   // número: pierde el cero
                libro.SaveAs(ms);
                ms.Position = 0;

                var f = ListaChoferesService.LeerExcel(ms).Single();
                Assert.Equal("PEDRO RAMOS QUISPE", f.Nombre);
                Assert.Equal("01234567", f.Dni);
            }
        }

        [Theory]
        [InlineData("ALFREDO RAMOS QUISPE", "ALFREDO", "RAMOS", "QUISPE")]
        [InlineData("PRIEER DEL JESUS SILVA PAZ", "PRIEER DEL JESUS", "SILVA", "PAZ")]
        [InlineData("MARIO VEGA DE LA CRUZ", "MARIO", "VEGA", "DE LA CRUZ")]
        [InlineData("LUIS PAZ", "LUIS", "PAZ", null)]
        public void SepararNombre(string completo, string nombre, string paterno, string materno)
        {
            var p = ListaChoferesService.SepararNombre(completo);
            Assert.Null(p.Error);
            Assert.Equal(nombre, p.Nombre);
            Assert.Equal(paterno, p.ApPaterno);
            Assert.Equal(materno, p.ApMaterno);
        }

        private static List<ListaChoferesService.FilaLista> Lista(params string[] filas) =>
            ListaChoferesService.LeerTexto(string.Join("\n", filas));

        [Fact]
        public void Comparar_ProponeRegistrarCompletarYDesactivar()
        {
            var registrados = new List<C>
            {
                new C { Id = 1, Dni = "01234567", Nombre = "PEDRO PABLO", ApPaterno = "RAMOS", ApMaterno = "QUISPE", Licencia = "B01234567", Activo = true },
                new C { Id = 2, Dni = "02222222", Nombre = "MAXIM", ApPaterno = "VARGAS", ApMaterno = "PAZ", Activo = true },
                new C { Id = 3, Dni = "03333333", Nombre = "MARIO", ApPaterno = "SOTO", ApMaterno = "LUNA", Activo = true },
                new C { Id = 4, Dni = "04444444", Nombre = "RAUL", ApPaterno = "VEGA", ApMaterno = "RIOS", Activo = true, TieneViajeAbierto = true },
                new C { Id = 5, Dni = "05555555", Nombre = "ELSA", ApPaterno = "MORI", ApMaterno = "DIAZ", Activo = false },
            };
            var r = ListaChoferesService.Comparar(Lista(
                "1\tPEDRO RAMOS QUISPE\tPERUANA\tB-01234567\t01234567",          // al día (el registrado es más completo)
                "2\tMAXIMILIANO JOSE VARGAS PAZ\tPERUANA\tB-02222222\t02222222",   // nombre cortado + licencia faltante
                "3\tELSA MORI DIAZ\tPERUANA\tA-05555555\t05555555",               // vuelve
                "4\tJUAN NUEVO TORRES\tPERUANA\tC-06666666\t06666666"),           // nuevo
                registrados);

            Assert.Equal(1, r.SinCambios);
            Assert.Empty(r.Problemas);
            Assert.Contains(r.Cambios, c => c.Tipo == T.Nombre && c.IdConductor == 2 && c.MarcadoPorDefecto && c.Nombre == "MAXIMILIANO JOSE");
            Assert.Contains(r.Cambios, c => c.Tipo == T.Licencia && c.IdConductor == 2 && c.Licencia == "B02222222");
            Assert.Contains(r.Cambios, c => c.Tipo == T.Reactivar && c.IdConductor == 5);
            Assert.Contains(r.Cambios, c => c.Tipo == T.Registrar && c.Dni == "06666666" && c.ApPaterno == "NUEVO" && c.ApMaterno == "TORRES");
            Assert.Contains(r.Cambios, c => c.Tipo == T.Desactivar && c.IdConductor == 3 && c.MarcadoPorDefecto);
            // Con viaje abierto se propone, pero sin marcar
            Assert.Contains(r.Cambios, c => c.Tipo == T.Desactivar && c.IdConductor == 4 && !c.MarcadoPorDefecto);
            Assert.DoesNotContain(r.Cambios, c => c.IdConductor == 1);
        }

        [Fact]
        public void Comparar_DniRepetidoEnLaLista_NoRegistraAlQueNoEsSuDueno()
        {
            var registrados = new List<C>
            {
                new C { Id = 1, Dni = "07777777", Nombre = "HUGO", ApPaterno = "CANO", ApMaterno = "LEON", Licencia = "C07777777", Activo = true },
            };
            var r = ListaChoferesService.Comparar(Lista(
                "21\tIVAN SOSA DIAZ\tPERUANA\tC-07777777\t07777777",
                "33\tHUGO CANO LEON\tPERUANA\tC-07777777\t07777777"), registrados);

            var problema = Assert.Single(r.Problemas);
            Assert.Equal(21, problema.Fila);
            Assert.Contains("HUGO CANO LEON", problema.Mensaje);
            Assert.Contains("se repite", problema.Mensaje);
            Assert.Empty(r.Cambios);
            Assert.Equal(1, r.SinCambios);
        }

        [Fact]
        public void Comparar_DniRepetidoQueNoEstaRegistrado_NoRegistraNinguno()
        {
            var r = ListaChoferesService.Comparar(Lista(
                "1\tANA PAZ ROJAS\tPERUANA\tB-08888888\t08888888",
                "2\tLUIS SOTO MEZA\tPERUANA\tB-08888888\t08888888"), new List<C>());

            Assert.Equal(2, r.Problemas.Count);
            Assert.Empty(r.Cambios);
        }

        [Fact]
        public void Comparar_MismoNombreConOtroDni_NoDuplica()
        {
            var registrados = new List<C>
            {
                new C { Id = 9, Dni = "09999999", Nombre = "ANA", ApPaterno = "PAZ", ApMaterno = "ROJAS", Activo = true },
            };
            var r = ListaChoferesService.Comparar(Lista("1\tANA PAZ ROJAS\tPERUANA\tB-09999990\t09999990"), registrados);

            Assert.Single(r.Problemas);
            Assert.Empty(r.Cambios);   // ni se registra otra vez ni se desactiva al registrado
        }
    }
}
