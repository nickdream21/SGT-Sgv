using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Catalogos;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-19: el SQL de los catálogos (clientes, plantas, estaciones de peaje y las grillas de choferes, tractos y
    /// semirremolques) salió de las páginas a sus servicios. Contra la BD de pruebas dentro de
    /// <see cref="TransaccionDePrueba"/> (no deja datos), con nombres y RUC inventados ("ZZ E619 …"). Alta, auditoría,
    /// editar sin chocar consigo mismo, duplicado y renombrar a un duplicado, con el mismo mensaje que tenía la
    /// página; plantas: el mismo nombre una vez nacional y una internacional (DN-35, migración 64). El duplicado lo
    /// da el índice único (violación 2601/2627): esos casos van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class CatalogosServiciosBdTests
    {
        public CatalogosServiciosBdTests() => BaseDePruebas.Preparar();

        private static readonly Random Azar = new Random();

        /// <summary>Sufijo de 8 letras A-F/dígitos en mayúsculas: cumple el formato de nombre de los tres catálogos.</summary>
        private static string Sufijo() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        /// <summary>RUC de 11 dígitos que no está en la BD.</summary>
        private static string RucLibre()
        {
            for (int i = 0; i < 50; i++)
            {
                string ruc = "209" + Azar.Next(10000000, 99999999).ToString();
                if (Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Cliente WHERE ruc = @r", DbHelper.Param("@r", ruc))) == 0)
                    return ruc;
            }
            throw new InvalidOperationException("No se encontró un RUC libre para la prueba.");
        }

        private static DataTable Auditoria(string tabla, int id, string accion) =>
            DbHelper.ConsultarTabla(
                @"SELECT Descripcion FROM AuditoriaLog
                  WHERE TablaAfectada = @t AND IdRegistroAfectado = @id AND Accion = @a",
                DbHelper.Param("@t", tabla), DbHelper.Param("@id", id.ToString()), DbHelper.Param("@a", accion));

        private static ClienteService.DatosCliente Cliente(string ruc, string nombre, string contacto = "") =>
            new ClienteService.DatosCliente
            {
                Ruc = ruc, Nombre = nombre, Direccion = "  ", Contacto = contacto, Telefono = "", Correo = null,
                Moneda = "USD", EsExportador = true, Observaciones = ""
            };

        // ── Clientes ─────────────────────────────────────────────────────────────────────

        [FactBD]
        public void Cliente_alta_guarda_nulos_audita_y_sale_en_la_grilla()
        {
            using (new TransaccionDePrueba())
            {
                string ruc = RucLibre();
                string nombre = "ZZ E619 CLIENTE " + Sufijo();
                int id = ClienteService.Registrar(Cliente(" " + ruc + " ", "  " + nombre + " "), "PRUEBA E619");

                DataRow r = DbHelper.ConsultarTabla(
                    "SELECT ruc, nombre, direccion, telefono, correo, observaciones, monedaFacturacion, esExportador, usuarioRegistro, activo FROM Cliente WHERE idCliente = @id",
                    DbHelper.Param("@id", id)).Rows[0];
                Assert.Equal(ruc, (string)r["ruc"]);
                Assert.Equal(nombre, (string)r["nombre"]);
                Assert.Equal(DBNull.Value, r["direccion"]);
                Assert.Equal(DBNull.Value, r["telefono"]);
                Assert.Equal(DBNull.Value, r["correo"]);
                Assert.Equal(DBNull.Value, r["observaciones"]);
                Assert.Equal("USD", (string)r["monedaFacturacion"]);
                Assert.True(Convert.ToBoolean(r["esExportador"]));
                Assert.Equal("PRUEBA E619", (string)r["usuarioRegistro"]);
                Assert.True(Convert.ToBoolean(r["activo"]));

                DataTable aud = Auditoria("Cliente", id, "INSERT");
                Assert.Equal(1, aud.Rows.Count);
                Assert.Equal($"Cliente registrado - Nombre: {nombre}, RUC: {ruc}, Moneda: USD, Exportador: Si",
                    (string)aud.Rows[0]["Descripcion"]);

                DataRow fila = ClienteService.ObtenerGrilla().Rows.Cast<DataRow>().Single(x => Convert.ToInt32(x["idCliente"]) == id);
                Assert.Equal("", (string)fila["direccion"]);
                Assert.Equal(ruc, (string)fila["ruc"]);

                Assert.Equal(ClienteService.MensajeRucRegistrado(nombre), ClienteService.AvisoRucRegistrado(ruc));
                Assert.Null(ClienteService.AvisoRucRegistrado(RucLibre()));

                // Dos clientes sin RUC conviven (NULL, no "", en el índice filtrado).
                int sinRuc1 = ClienteService.Registrar(Cliente("", "ZZ E619 SIN RUC " + Sufijo()), "PRUEBA E619");
                int sinRuc2 = ClienteService.Registrar(Cliente("  ", "ZZ E619 SIN RUC " + Sufijo()), "PRUEBA E619");
                Assert.NotEqual(sinRuc1, sinRuc2);
                Assert.Equal("Cliente registrado - Nombre: " + DbHelper.EjecutarEscalar(
                        "SELECT nombre FROM Cliente WHERE idCliente = @id", DbHelper.Param("@id", sinRuc1)) +
                    ", RUC: Sin RUC, Moneda: USD, Exportador: Si",
                    (string)Auditoria("Cliente", sinRuc1, "INSERT").Rows[0]["Descripcion"]);
            }
        }

        [FactBD]
        public void Cliente_editar_sin_cambiar_el_ruc_no_choca_consigo_mismo_y_audita()
        {
            using (new TransaccionDePrueba())
            {
                string ruc = RucLibre();
                string nombre = "ZZ E619 EDITAR " + Sufijo();
                int id = ClienteService.Registrar(Cliente(ruc, nombre), "PRUEBA E619");

                var d = Cliente(ruc, nombre, contacto: "AREA LOGISTICA");
                d.Moneda = "PEN";
                d.EsExportador = false;
                ClienteService.Actualizar(id, d);

                DataRow r = DbHelper.ConsultarTabla("SELECT contacto, monedaFacturacion, esExportador FROM Cliente WHERE idCliente = @id",
                    DbHelper.Param("@id", id)).Rows[0];
                Assert.Equal("AREA LOGISTICA", (string)r["contacto"]);
                Assert.Equal("PEN", (string)r["monedaFacturacion"]);
                Assert.False(Convert.ToBoolean(r["esExportador"]));
                DataTable aud = Auditoria("Cliente", id, "UPDATE");
                Assert.Equal(1, aud.Rows.Count);
                Assert.Equal($"Cliente editado — Nombre:{nombre}, RUC:{ruc}, Moneda:PEN, Exportador:No", (string)aud.Rows[0]["Descripcion"]);

                // Un nombre antiguo fuera de formato se conserva si no cambia; uno nuevo fuera de formato no pasa.
                DbHelper.EjecutarNonQuery("UPDATE Cliente SET nombre = 'ZZ+E619' WHERE idCliente = @id", DbHelper.Param("@id", id));
                ClienteService.Actualizar(id, Cliente(ruc, "ZZ+E619"));
                var ex = Assert.Throws<ErrorNegocioException>(() => ClienteService.Actualizar(id, Cliente(ruc, "ZZ+E619 OTRO")));
                Assert.Equal(ClienteService.MensajeNombreInvalido, ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => ClienteService.Actualizar(id, Cliente("123", "ZZ+E619")));
                Assert.Equal(ClienteService.MensajeRucInvalido, ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => ClienteService.Actualizar(id, Cliente(ruc, "   ")));
                Assert.Equal(ClienteService.MensajeNombreObligatorio, ex.Message);
            }
        }

        [FactBD]
        public void Cliente_alta_con_un_ruc_registrado_da_el_mensaje_de_siempre()
        {
            using (new TransaccionDePrueba())
            {
                string ruc = RucLibre();
                ClienteService.Registrar(Cliente(ruc, "ZZ E619 PRIMERO " + Sufijo()), "PRUEBA E619");

                var ex = Assert.Throws<ErrorNegocioException>(
                    () => ClienteService.Validar("2012345678X", "ZZ E619 OTRO"));
                Assert.Equal(ClienteService.MensajeRucInvalido, ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => ClienteService.Registrar(Cliente(ruc, "Z"), "PRUEBA E619"));
                Assert.Equal(ClienteService.MensajeNombreInvalido, ex.Message);

                // Error al final: la violación de UQ_Cliente_Ruc
                ex = Assert.Throws<ErrorNegocioException>(
                    () => ClienteService.Registrar(Cliente(ruc, "ZZ E619 SEGUNDO " + Sufijo()), "PRUEBA E619"));
                Assert.Equal("Ya existe un cliente registrado con ese RUC.", ex.Message);
                Assert.IsAssignableFrom<SqlException>(ex.InnerException);
            }
        }

        [FactBD]
        public void Cliente_cambiar_al_ruc_de_otro_da_el_mensaje_de_siempre()
        {
            using (new TransaccionDePrueba())
            {
                string rucA = RucLibre();
                ClienteService.Registrar(Cliente(rucA, "ZZ E619 A " + Sufijo()), "PRUEBA E619");
                string nombreB = "ZZ E619 B " + Sufijo();
                int idB = ClienteService.Registrar(Cliente(RucLibre(), nombreB), "PRUEBA E619");

                // Error al final: la violación de UQ_Cliente_Ruc
                var ex = Assert.Throws<ErrorNegocioException>(() => ClienteService.Actualizar(idB, Cliente(rucA, nombreB)));
                Assert.Equal("Ya existe otro cliente registrado con ese RUC.", ex.Message);
            }
        }

        // ── Plantas ──────────────────────────────────────────────────────────────────────

        [FactBD]
        public void Planta_el_mismo_nombre_se_admite_una_vez_nacional_y_una_internacional()
        {
            using (new TransaccionDePrueba())
            {
                string nombre = "ZZ E619 PLANTA " + Sufijo();
                int nacional = PlantaService.Registrar(" " + nombre.ToLowerInvariant() + " ", false);
                int internacional = PlantaService.Registrar(nombre, true);
                Assert.NotEqual(nacional, internacional);

                DataRow r = DbHelper.ConsultarTabla("SELECT nombre, esInternacional, activo FROM Planta WHERE idPlanta = @id",
                    DbHelper.Param("@id", nacional)).Rows[0];
                Assert.Equal(nombre, (string)r["nombre"]);
                Assert.False(Convert.ToBoolean(r["esInternacional"]));
                Assert.True(Convert.ToBoolean(r["activo"]));

                Assert.Equal($"Planta registrada - Nombre: {nombre}, Ámbito: Nacional",
                    (string)Auditoria("Planta", nacional, "INSERT").Rows[0]["Descripcion"]);
                Assert.Equal($"Planta registrada - Nombre: {nombre}, Ámbito: Internacional",
                    (string)Auditoria("Planta", internacional, "INSERT").Rows[0]["Descripcion"]);

                var grilla = PlantaService.ObtenerGrilla().Rows.Cast<DataRow>().Where(x => (string)x["nombre"] == nombre).ToList();
                Assert.Equal(2, grilla.Count);

                var ex = Assert.Throws<ErrorNegocioException>(() => PlantaService.ValidarAlta("  "));
                Assert.Equal(PlantaService.MensajeNombreObligatorio, ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => PlantaService.Registrar("ZZ+E619", false));
                Assert.Equal(PlantaService.MensajeNombreInvalido, ex.Message);

                // Error al final: la violación de UX_Planta_Nombre_Ambito (mismo nombre, mismo ámbito).
                ex = Assert.Throws<ErrorNegocioException>(() => PlantaService.Registrar(nombre.ToLowerInvariant(), true));
                Assert.Equal("Ya existe una planta con ese nombre en el mismo ámbito.", ex.Message);
            }
        }

        [FactBD]
        public void Planta_editar_sin_cambiar_no_choca_consigo_misma_y_audita()
        {
            using (new TransaccionDePrueba())
            {
                string nombre = "ZZ E619 EDITAR " + Sufijo();
                int id = PlantaService.Registrar(nombre, false);

                PlantaService.Actualizar(id, nombre.ToLowerInvariant(), false);
                PlantaService.Actualizar(id, nombre, true);   // cambia de ámbito: no hay otra internacional con ese nombre

                DataRow r = DbHelper.ConsultarTabla("SELECT nombre, esInternacional FROM Planta WHERE idPlanta = @id",
                    DbHelper.Param("@id", id)).Rows[0];
                Assert.Equal(nombre, (string)r["nombre"]);
                Assert.True(Convert.ToBoolean(r["esInternacional"]));
                var aud = Auditoria("Planta", id, "UPDATE").Rows.Cast<DataRow>().Select(x => (string)x["Descripcion"]).ToList();
                Assert.Contains($"Planta editada — Nombre:{nombre}, Ámbito:Nacional", aud);
                Assert.Contains($"Planta editada — Nombre:{nombre}, Ámbito:Internacional", aud);

                var ex = Assert.Throws<ErrorNegocioException>(() => PlantaService.Actualizar(id, " ", false));
                Assert.Equal(PlantaService.MensajeNombreObligatorio, ex.Message);
            }
        }

        [FactBD]
        public void Planta_renombrar_a_una_del_mismo_ambito_da_el_mensaje_de_siempre()
        {
            using (new TransaccionDePrueba())
            {
                string nombreA = "ZZ E619 A " + Sufijo();
                PlantaService.Registrar(nombreA, false);
                int idB = PlantaService.Registrar("ZZ E619 B " + Sufijo(), false);

                // A la internacional del mismo nombre sí se puede (otro ámbito).
                PlantaService.Actualizar(idB, nombreA, true);

                // Error al final: vuelve al ámbito nacional, donde ya está A.
                var ex = Assert.Throws<ErrorNegocioException>(() => PlantaService.Actualizar(idB, nombreA, false));
                Assert.Equal("Ya existe otra planta con ese nombre en el mismo ámbito.", ex.Message);
            }
        }

        [FactBD]
        public void Planta_indice_unico_por_nombre_y_ambito_rechaza_un_insert_directo()
        {
            DataTable cols = DbHelper.ConsultarTabla(
                @"SELECT c.name FROM sys.indexes i
                  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE i.object_id = OBJECT_ID('dbo.Planta') AND i.name = @n AND i.is_unique = 1 AND i.has_filter = 0
                  ORDER BY ic.key_ordinal", DbHelper.Param("@n", PlantaService.IndiceNombreAmbito));
            Assert.Equal(new[] { "nombre", "esInternacional" }, cols.Rows.Cast<DataRow>().Select(x => (string)x["name"]).ToArray());

            using (new TransaccionDePrueba())
            {
                string nombre = "ZZ E619 DIRECTO " + Sufijo();
                PlantaService.Registrar(nombre, true);

                // Error al final: collation CI, "zz e619 …" es el mismo nombre.
                var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(
                    "INSERT INTO Planta (nombre, esInternacional, activo) VALUES (@n, 1, 0)",
                    DbHelper.Param("@n", nombre.ToLowerInvariant())));
                Assert.True(ex.Number == 2601 || ex.Number == 2627, "Número de error: " + ex.Number);
                Assert.Contains(PlantaService.IndiceNombreAmbito, ex.Message);

                // Solo la violación del índice esperado es "nombre repetido"; la de otro índice queda como técnica.
                Assert.True(PlantaService.EsClaveDuplicada(ex, PlantaService.IndiceNombreAmbito));
                Assert.False(ClienteService.EsClaveDuplicada(ex, ClienteService.IndiceRuc));
                Assert.False(EstacionPeajeService.EsClaveDuplicada(ex, EstacionPeajeService.IndiceNombre));
            }
        }

        // ── Estaciones de peaje ──────────────────────────────────────────────────────────

        [FactBD]
        public void Peaje_alta_audita_y_editar_sin_cambiar_no_choca_consigo_mismo()
        {
            using (new TransaccionDePrueba())
            {
                string nombre = "ZZ E619 PEAJE " + Sufijo();
                int id = EstacionPeajeService.Registrar(" " + nombre.ToLowerInvariant());
                Assert.Equal(nombre, (string)DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM EstacionesPeaje WHERE idEstacion = @id", DbHelper.Param("@id", id)));
                Assert.Equal($"Estacion de peaje registrada - Nombre: {nombre}",
                    (string)Auditoria("EstacionesPeaje", id, "INSERT").Rows[0]["Descripcion"]);
                Assert.Contains(EstacionPeajeService.ObtenerGrilla().Rows.Cast<DataRow>(), x => Convert.ToInt32(x["idEstacion"]) == id);

                EstacionPeajeService.Actualizar(id, nombre.ToLowerInvariant());
                Assert.Equal($"Estación de peaje editada — Nombre:{nombre}",
                    (string)Auditoria("EstacionesPeaje", id, "UPDATE").Rows[0]["Descripcion"]);

                // Un nombre antiguo con "+" se conserva si no cambia; uno nuevo con "+" no pasa.
                string antiguo = "ZZ+E619 " + Sufijo();
                DbHelper.EjecutarNonQuery("UPDATE EstacionesPeaje SET nombre = @n WHERE idEstacion = @id",
                    DbHelper.Param("@n", antiguo), DbHelper.Param("@id", id));
                EstacionPeajeService.Actualizar(id, antiguo);
                var ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Actualizar(id, antiguo + " X"));
                Assert.Equal(EstacionPeajeService.MensajeNombreInvalido, ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Actualizar(int.MaxValue, nombre));
                Assert.Equal("La estación ya no existe; recargue la página.", ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Actualizar(id, ""));
                Assert.Equal("Debe ingresar el nombre de la estación.", ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Registrar(""));
                Assert.Equal("Debe ingresar el nombre de la estación de peaje.", ex.Message);
                ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Registrar("ZZ+E619"));
                Assert.Equal(EstacionPeajeService.MensajeNombreInvalido, ex.Message);
            }
        }

        [FactBD]
        public void Peaje_alta_con_un_nombre_registrado_da_el_mensaje_de_siempre()
        {
            using (new TransaccionDePrueba())
            {
                string nombre = "ZZ E619 REPETIDO " + Sufijo();
                EstacionPeajeService.Registrar(nombre);

                // Error al final: la violación de UK_EstacionesPeaje_Nombre
                var ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Registrar(nombre.ToLowerInvariant()));
                Assert.Equal("Ya existe una estación de peaje con ese nombre.", ex.Message);
            }
        }

        [FactBD]
        public void Peaje_renombrar_a_otra_registrada_da_el_mensaje_de_siempre()
        {
            using (new TransaccionDePrueba())
            {
                string nombreA = "ZZ E619 A " + Sufijo();
                EstacionPeajeService.Registrar(nombreA);
                int idB = EstacionPeajeService.Registrar("ZZ E619 B " + Sufijo());

                // Error al final: la violación de UK_EstacionesPeaje_Nombre
                var ex = Assert.Throws<ErrorNegocioException>(() => EstacionPeajeService.Actualizar(idB, nombreA));
                Assert.Equal("Ya existe otra estación de peaje con ese nombre.", ex.Message);
            }
        }

        // ── Grillas de choferes, tractos y semirremolques ────────────────────────────────

        [FactBD]
        public void Grillas_de_choferes_tractos_y_semirremolques_traen_las_columnas_de_la_pagina()
        {
            using (new TransaccionDePrueba())
            {
                int tracto = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ9-919", "VOLVO", "FH");
                int carreta = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9-918", "RANDON", "SR");

                DataTable tractos = VehiculoService.ObtenerGrilla(TipoVehiculo.Tracto);
                foreach (string c in new[] { "idTracto", "placaTracto", "marca", "modelo", "activo", "conHistorial" })
                    Assert.True(tractos.Columns.Contains(c), "Falta la columna " + c);
                DataRow t = tractos.Rows.Cast<DataRow>().Single(x => Convert.ToInt32(x["idTracto"]) == tracto);
                Assert.Equal("ZZ9-919", (string)t["placaTracto"]);
                Assert.False((bool)t["conHistorial"]);

                DataTable carretas = VehiculoService.ObtenerGrilla(TipoVehiculo.Carreta);
                foreach (string c in new[] { "idCarreta", "placaCarreta", "marca", "modelo", "activo", "conHistorial" })
                    Assert.True(carretas.Columns.Contains(c), "Falta la columna " + c);
                Assert.Contains(carretas.Rows.Cast<DataRow>(), x => Convert.ToInt32(x["idCarreta"]) == carreta);
            }

            DataTable choferes = ListaChoferesService.ObtenerGrilla();
            foreach (string c in new[] { "idConductor", "documentoDisplay", "nombre", "apPaterno", "apMaterno", "nombreCompleto", "telefono", "activo" })
                Assert.True(choferes.Columns.Contains(c), "Falta la columna " + c);
            Assert.Equal(Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Conductor")), choferes.Rows.Count);
            Assert.DoesNotContain(choferes.Rows.Cast<DataRow>(), x => x["documentoDisplay"] == DBNull.Value || x["telefono"] == DBNull.Value);
        }
    }
}
