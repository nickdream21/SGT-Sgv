using System.IO;
using WebSGV.Helpers;
using WebSGV.Services.Clientes;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Bloque 5, B5-5: búsqueda literal en GestionUsuarios (los comodines del LIKE se escapan), mensajes de
    /// nombre repetido del catálogo del cliente (las rutas son comunes a todos) y la rama sin codificar de
    /// DestinoDescarga.
    /// </summary>
    public class LimpiezaBloque5Tests
    {
        [Theory]
        [InlineData("juan", "%juan%")]
        [InlineData("a_b", "%a[_]b%")]
        [InlineData("100%", "%100[%]%")]
        [InlineData("[x]", "%[[]x]%")]
        [InlineData("%_[", "%[%][_][[]%")]
        [InlineData("", "%%")]
        [InlineData(null, "%%")]
        public void PatronContiene_escapa_los_comodines(string texto, string esperado)
        {
            Assert.Equal(esperado, DbHelper.PatronContiene(texto));
        }

        [Fact]
        public void Duplicado_de_ruta_dice_que_ya_existe_en_general()
        {
            Assert.Equal("Ya existe una ruta con ese nombre.", CatalogoClienteService.MensajeDuplicado(TipoCatalogo.Ruta));
        }

        [Fact]
        public void Duplicado_de_producto_y_planta_es_dentro_del_cliente()
        {
            Assert.Equal("Este cliente ya tiene un producto con ese nombre.",
                CatalogoClienteService.MensajeDuplicado(TipoCatalogo.Producto));
            Assert.Equal("Este cliente ya tiene una planta de carga con ese nombre.",
                CatalogoClienteService.MensajeDuplicado(TipoCatalogo.PlantaCarga));
            Assert.Equal("Este cliente ya tiene una planta de descarga con ese nombre.",
                CatalogoClienteService.MensajeDuplicado(TipoCatalogo.PlantaDescarga));
        }

        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        [Fact]
        public void GestionUsuarios_busca_con_el_patron_escapado()
        {
            string codigo = Fuente("WebSGV", "Views", "GestionUsuarios.aspx.cs");
            Assert.Contains("DbHelper.PatronContiene(buscar)", codigo);
            Assert.DoesNotContain("\"%\" + buscar", codigo);
        }

        [Fact]
        public void DestinoDescarga_no_escribe_la_planta_anterior_sin_codificar()
        {
            string codigo = Fuente("WebSGV", "Views", "DestinoDescarga.aspx.cs");
            Assert.DoesNotContain("{f.PlantaAnterior}", codigo);
            Assert.Contains("H(f.PlantaAnterior)", codigo);
        }

        [Fact]
        public void Auditoria_filtro_Actualizacion_con_tilde_y_mismo_valor()
        {
            string marcado = Fuente("WebSGV", "Views", "Auditoria.aspx");
            Assert.Contains("<asp:ListItem Value=\"UPDATE\">Actualización</asp:ListItem>", marcado);
        }
    }
}
