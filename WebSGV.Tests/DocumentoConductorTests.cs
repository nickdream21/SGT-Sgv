using System.Collections.Generic;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using Xunit;
using G = WebSGV.Services.Conductor.RegistroConductorService.DocumentoGuardado;
using R = WebSGV.Services.Conductor.RegistroConductorService;

namespace WebSGV.Tests
{
    /// <summary>
    /// Conductores y longitudes (bloque 5, B5-2): forma única del documento (la misma de la Lista de
    /// choferes), duplicados contra documentos guardados de otra forma, largos de las columnas en el alta y
    /// la edición del conductor y en los catálogos por cliente. Datos inventados.
    /// </summary>
    public class DocumentoConductorTests
    {
        // ── Normalización ───────────────────────────────────────────────

        [Theory]
        [InlineData("8800123", "008800123")]       // sin los ceros (Excel lo guardó como número)
        [InlineData("008800123", "008800123")]
        [InlineData(" 0088 00123 ", "008800123")]  // espacios
        [InlineData("123456", "000123456")]
        [InlineData("1234567890", "1234567890")]   // más de 9 dígitos: tal cual
        [InlineData("ab-12345", "AB-12345")]       // con letras: solo mayúsculas
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void NormalizarCarnet(string entrada, string esperado) =>
            Assert.Equal(esperado, DocumentoConductor.NormalizarCarnet(entrada));

        [Theory]
        [InlineData("09999001", "09999001")]
        [InlineData("9999001", "09999001")]        // 7 dígitos: recupera el cero
        [InlineData(" 0999 9001 ", "09999001")]
        [InlineData("09999001 ", "09999001")]      // char(8) de la BD con relleno
        [InlineData("123456", null)]
        [InlineData("123456789", null)]
        [InlineData("1234567A", null)]
        [InlineData(null, null)]
        public void NormalizarDni(string entrada, string esperado) =>
            Assert.Equal(esperado, DocumentoConductor.NormalizarDni(entrada));

        [Theory]
        [InlineData("8800123", true)]
        [InlineData("008800123", true)]
        [InlineData("AB-12345", true)]
        [InlineData("12345678901", true)]          // 11 = char(11)
        [InlineData("123456789012", false)]        // 12 no cabe
        [InlineData("12345", false)]
        [InlineData("ABCDEFG", false)]             // sin dígitos
        [InlineData("1234/567", false)]
        public void EsCarnetValido(string entrada, bool esperado) =>
            Assert.Equal(esperado, DocumentoConductor.EsCarnetValido(entrada));

        [Fact]
        public void ListaDeChoferes_conserva_la_misma_normalizacion()
        {
            var filas = ListaChoferesService.LeerTexto(
                "1\tJUAN PEREZ GOMEZ\tVENEZOLANA\tB-12345678\t8800123\n" +
                "2\tANA RUIZ PAZ\tPERUANA\tA-87654321\t9999001\n");

            Assert.Equal("008800123", filas[0].Carnet);
            Assert.Null(filas[0].Dni);
            Assert.Equal("09999001", filas[1].Dni);
            Assert.Null(filas[1].Carnet);
        }

        // ── Documento del alta ──────────────────────────────────────────

        [Fact]
        public void ValidarDocumento_carnet_se_guarda_con_ceros()
        {
            R.ValidarDocumento(R.TipoCarnet, " 8800123 ", out string dni, out string carnet);
            Assert.Null(dni);
            Assert.Equal("008800123", carnet);
        }

        [Fact]
        public void ValidarDocumento_dni_de_8_digitos()
        {
            R.ValidarDocumento(R.TipoDni, "09999001", out string dni, out string carnet);
            Assert.Equal("09999001", dni);
            Assert.Null(carnet);
        }

        [Theory]
        [InlineData("DNI", "9999001")]                 // el alta pide los 8 dígitos
        [InlineData("DNI", "1234567A")]
        [InlineData("Carnet de Extranjería", "123")]
        [InlineData("Carnet de Extranjería", "123456789012")]
        [InlineData("Pasaporte", "AB123456")]          // D3: ya no se acepta (no había dónde guardarlo)
        [InlineData("", "12345678")]
        public void ValidarDocumento_rechaza(string tipo, string numero) =>
            Assert.Throws<ErrorNegocioException>(() => R.ValidarDocumento(tipo, numero, out _, out _));

        // ── Duplicados ──────────────────────────────────────────────────

        private static readonly List<G> Guardados = new List<G>
        {
            new G { IdConductor = 1, Carnet = "008800123  ", NombreCompleto = "JUAN PEREZ" },   // char(11) con relleno
            new G { IdConductor = 2, Carnet = "8800456", NombreCompleto = "LUIS SOTO" },        // guardado sin ceros
            new G { IdConductor = 3, Dni = "9999001 ", NombreCompleto = "ANA RUIZ" }            // DNI sin el cero
        };

        [Fact]
        public void Duplicado_carnet_sin_ceros_contra_guardado_con_ceros()
        {
            string m = R.BuscarDuplicado(null, DocumentoConductor.NormalizarCarnet("8800123"), Guardados);
            Assert.Equal("Ya existe un conductor con el carnet de extranjería 008800123: JUAN PEREZ.", m);
        }

        [Fact]
        public void Duplicado_carnet_contra_guardado_sin_ceros()
        {
            string m = R.BuscarDuplicado(null, "008800456", Guardados);
            Assert.Equal("Ya existe un conductor con el carnet de extranjería 008800456 (registrado como 8800456): LUIS SOTO.", m);
        }

        [Fact]
        public void Duplicado_dni_contra_guardado_sin_el_cero()
        {
            Assert.Contains("ANA RUIZ", R.BuscarDuplicado("09999001", null, Guardados));
        }

        [Fact]
        public void Duplicado_excluye_al_propio_conductor_y_documentos_libres()
        {
            Assert.Null(R.BuscarDuplicado(null, "008800123", Guardados, idPropio: 1));
            Assert.Null(R.BuscarDuplicado(null, "008800124", Guardados));
            Assert.Null(R.BuscarDuplicado("12345678", null, Guardados));
        }

        // ── Largos del conductor ────────────────────────────────────────

        [Fact]
        public void ValidarPersona_respeta_los_largos_de_las_columnas()
        {
            var d = R.ValidarPersona(new string('A', 50), new string('B', 20), new string('C', 20), "987654321");
            Assert.Equal(50, d.Nombre.Length);

            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona(new string('A', 51), "PEREZ", "", ""));
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("JUAN", new string('B', 21), "", ""));
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("JUAN", "PEREZ", new string('C', 21), ""));
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("JUAN", "PEREZ", "", "12345"));
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("", "PEREZ", "", ""));
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("JUAN2", "PEREZ", "", ""));
        }

        [Fact]
        public void ValidarPersona_limpia_espacios_y_deja_null_lo_opcional()
        {
            var d = R.ValidarPersona("  JUAN   CARLOS ", " PEREZ ", "  ", " ");
            Assert.Equal("JUAN CARLOS", d.Nombre);
            Assert.Equal("PEREZ", d.ApPaterno);
            Assert.Null(d.ApMaterno);
            Assert.Null(d.Telefono);
        }

        [Fact]
        public void ValidarPersona_en_la_edicion_conserva_un_telefono_antiguo_que_no_cambia()
        {
            var guardado = new R.DatosConductor { Nombre = "JUAN", ApPaterno = "PEREZ", Telefono = "044-123456" };

            var d = R.ValidarPersona("JUAN", "PEREZ", "", "044-123456", guardado);
            Assert.Equal("044-123456", d.Telefono);

            var ex = Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona("JUAN", "PEREZ", "", "044-1234567", guardado));
            Assert.Contains("Teléfono", ex.Message);
            // Un nombre que cambia se valida igual que en el alta
            Assert.Throws<ErrorNegocioException>(() => R.ValidarPersona(new string('A', 51), "PEREZ", "", "044-123456", guardado));
        }

        // ── Largos de los catálogos por cliente ─────────────────────────

        [Theory]
        [InlineData(TipoCatalogo.Producto, 50, 300)]
        [InlineData(TipoCatalogo.PlantaCarga, 100, 200)]
        [InlineData(TipoCatalogo.PlantaDescarga, 100, 200)]
        [InlineData(TipoCatalogo.Ruta, 100, 200)]
        public void Catalogo_valida_los_largos(TipoCatalogo tipo, int largoNombre, int largoDetalle)
        {
            Assert.Equal(largoNombre, CatalogoClienteService.LargoNombre(tipo));
            Assert.Equal(largoDetalle, CatalogoClienteService.LargoDetalle(tipo));

            CatalogoClienteService.Validar(tipo, new string('N', largoNombre), new string('D', largoDetalle));
            CatalogoClienteService.Validar(tipo, " " + new string('N', largoNombre) + " ", null);   // se recorta

            Assert.Throws<ErrorNegocioException>(() => CatalogoClienteService.Validar(tipo, new string('N', largoNombre + 1), ""));
            Assert.Throws<ErrorNegocioException>(() => CatalogoClienteService.Validar(tipo, "X", new string('D', largoDetalle + 1)));
            Assert.Throws<ErrorNegocioException>(() => CatalogoClienteService.Validar(tipo, "  ", ""));
        }
    }
}
