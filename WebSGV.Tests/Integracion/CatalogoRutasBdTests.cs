using System;
using WebSGV.Helpers;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Bloque 5, B5-5, contra la BD de pruebas dentro de una <see cref="TransaccionDePrueba"/> (no deja datos):
    /// <c>Ruta.nombre</c> es UNIQUE en toda la tabla, así que el nombre repetido de una ruta se busca en todos los
    /// clientes (antes filtraba por cliente y el UPDATE/INSERT acababa en "Ocurrió un error inesperado"); y la
    /// violación del índice único se convierte en <see cref="ErrorNegocioException"/> con un mensaje claro.
    /// Productos y plantas siguen comparándose dentro del cliente. Los casos de error van al final.
    /// Además, solo lectura: el patrón de <see cref="DbHelper.PatronContiene"/> busca literal en SQL Server.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class CatalogoRutasBdTests
    {
        private const string RutaA = "ZZ RUTA PRUEBA B5-5 A";
        private const string RutaB = "ZZ RUTA PRUEBA B5-5 B";

        public CatalogoRutasBdTests() => BaseDePruebas.Preparar();

        private static int InsertarCliente(string nombre) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Cliente (nombre, usuarioRegistro, activo) VALUES (@n, 'PRUEBA', 1);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                DbHelper.Param("@n", nombre)));

        [FactBD]
        public void Nombre_de_ruta_repetido_se_busca_en_todos_los_clientes()
        {
            using (new TransaccionDePrueba())
            {
                int cliente1 = InsertarCliente("ZZ CLIENTE B5-5 UNO");
                int cliente2 = InsertarCliente("ZZ CLIENTE B5-5 DOS");
                int idA = CatalogoClienteService.Crear(TipoCatalogo.Ruta, cliente1, RutaA, "");

                // Desde otro cliente la ruta no se ve en su lista, pero el nombre ya está tomado
                Assert.True(CatalogoClienteService.ExisteNombre(TipoCatalogo.Ruta, cliente2, " zz ruta prueba b5-5 a "));
                // La propia fila no cuenta (editar sin cambiar el nombre)
                Assert.False(CatalogoClienteService.ExisteNombre(TipoCatalogo.Ruta, cliente1, RutaA, idA));
                Assert.False(CatalogoClienteService.ExisteNombre(TipoCatalogo.Ruta, cliente2, RutaB));

                // Una ruta general (idCliente NULL) también ocupa el nombre para cualquier cliente
                object general = DbHelper.EjecutarEscalar("SELECT TOP 1 nombre FROM Ruta WHERE idCliente IS NULL ORDER BY idRuta");
                Assert.True(general != null, "La BD de pruebas no tiene rutas generales con qué comparar.");
                Assert.True(CatalogoClienteService.ExisteNombre(TipoCatalogo.Ruta, cliente2, (string)general));
            }
        }

        [FactBD]
        public void Nombre_de_producto_repetido_sigue_siendo_por_cliente()
        {
            using (new TransaccionDePrueba())
            {
                int cliente1 = InsertarCliente("ZZ CLIENTE B5-5 UNO");
                int cliente2 = InsertarCliente("ZZ CLIENTE B5-5 DOS");
                CatalogoClienteService.Crear(TipoCatalogo.Producto, cliente1, "ZZ PRODUCTO B5-5", "");

                Assert.True(CatalogoClienteService.ExisteNombre(TipoCatalogo.Producto, cliente1, "zz producto b5-5"));
                Assert.False(CatalogoClienteService.ExisteNombre(TipoCatalogo.Producto, cliente2, "ZZ PRODUCTO B5-5"));
            }
        }

        [FactBD]
        public void Patron_contiene_busca_literal_en_sql_server()
        {
            int Coincide(string valor, string buscado) => Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT CASE WHEN @v LIKE @p THEN 1 ELSE 0 END",
                DbHelper.Param("@v", valor), DbHelper.Param("@p", DbHelper.PatronContiene(buscado))));

            Assert.Equal(1, Coincide("juan_perez", "n_p"));
            Assert.Equal(0, Coincide("juanXperez", "n_p"));
            Assert.Equal(0, Coincide("admin", "%"));
            Assert.Equal(1, Coincide("al 100% listo", "100%"));
            Assert.Equal(0, Coincide("admin", "[a-z]"));
            Assert.Equal(1, Coincide("x[a-z]y", "[a-z]"));
        }

        // ── Casos de error (al final) ─────────────────────────────────────────

        [FactBD]
        public void Renombrar_una_ruta_con_el_nombre_de_otra_da_mensaje_claro()
        {
            using (new TransaccionDePrueba())
            {
                int cliente1 = InsertarCliente("ZZ CLIENTE B5-5 UNO");
                int cliente2 = InsertarCliente("ZZ CLIENTE B5-5 DOS");
                CatalogoClienteService.Crear(TipoCatalogo.Ruta, cliente1, RutaA, "");
                int idB = CatalogoClienteService.Crear(TipoCatalogo.Ruta, cliente2, RutaB, "");

                var ex = Assert.Throws<ErrorNegocioException>(
                    () => CatalogoClienteService.Actualizar(TipoCatalogo.Ruta, idB, RutaA, "otra descripción"));
                Assert.Equal("Ya existe una ruta con ese nombre.", ex.Message);
                Assert.Equal("Ya existe una ruta con ese nombre.", MensajeErrorHelper.ParaUsuario(ex));

                Assert.Equal(RutaB, (string)DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM Ruta WHERE idRuta = @id", DbHelper.Param("@id", idB)));
            }
        }

        [FactBD]
        public void Crear_una_ruta_con_el_nombre_de_otra_da_mensaje_claro()
        {
            using (new TransaccionDePrueba())
            {
                int cliente1 = InsertarCliente("ZZ CLIENTE B5-5 UNO");
                int cliente2 = InsertarCliente("ZZ CLIENTE B5-5 DOS");
                CatalogoClienteService.Crear(TipoCatalogo.Ruta, cliente1, RutaA, "");

                var ex = Assert.Throws<ErrorNegocioException>(
                    () => CatalogoClienteService.Crear(TipoCatalogo.Ruta, cliente2, RutaA.ToLowerInvariant(), ""));
                Assert.Equal("Ya existe una ruta con ese nombre.", ex.Message);
            }
        }
    }
}
