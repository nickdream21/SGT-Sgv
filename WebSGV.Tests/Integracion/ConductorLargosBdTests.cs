using System;
using WebSGV.Helpers;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Bloque 5, B5-2, de SOLO LECTURA: los largos que validan las pantallas son los de las columnas
    /// (si alguien cambia una columna, esta prueba avisa).
    /// </summary>
    public class LargoColumnasBdTests
    {
        public LargoColumnasBdTests() => BaseDePruebas.Preparar();

        private static int Largo(string tabla, string columna)
        {
            object v = DbHelper.EjecutarEscalar(
                @"SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @t AND COLUMN_NAME = @c",
                DbHelper.Param("@t", tabla), DbHelper.Param("@c", columna));
            Assert.True(v != null && v != DBNull.Value, $"No existe la columna {tabla}.{columna}");
            return Convert.ToInt32(v);
        }

        [FactBD]
        public void Largos_del_conductor_son_los_de_la_tabla()
        {
            Assert.Equal(RegistroConductorService.LargoNombre, Largo("Conductor", "nombre"));
            Assert.Equal(RegistroConductorService.LargoApellido, Largo("Conductor", "apPaterno"));
            Assert.Equal(RegistroConductorService.LargoApellido, Largo("Conductor", "apMaterno"));
            Assert.Equal(RegistroConductorService.LargoTelefono, Largo("Conductor", "telefono"));
            Assert.Equal(DocumentoConductor.LargoDni, Largo("Conductor", "DNI"));
            Assert.Equal(DocumentoConductor.LargoCarnet, Largo("Conductor", "carnetExtranjeria"));
        }

        [FactBD]
        public void Largos_de_cliente_y_peaje_son_los_de_la_tabla()
        {
            Assert.Equal(LargoColumnas.ClienteNombre, Largo("Cliente", "nombre"));
            Assert.Equal(LargoColumnas.EstacionPeajeNombre, Largo("EstacionesPeaje", "nombre"));
        }

        [FactBD]
        public void Largos_de_los_catalogos_son_los_de_la_tabla()
        {
            foreach (TipoCatalogo tipo in Enum.GetValues(typeof(TipoCatalogo)))
            {
                string tabla = CatalogoClienteService.NombreTabla(tipo);
                Assert.Equal(Largo(tabla, "nombre"), CatalogoClienteService.LargoNombre(tipo));
                Assert.Equal(Largo(tabla, CatalogoClienteService.ColumnaDetalle(tipo)), CatalogoClienteService.LargoDetalle(tipo));
            }
        }
    }

    /// <summary>
    /// Alta manual de conductores contra la BD de pruebas (bloque 5, B5-2), dentro de una
    /// <see cref="TransaccionDePrueba"/>: no deja datos. Carnets inventados (99900xx) que no existen.
    /// Los casos de error van al final.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class RegistroConductorBdTests
    {
        public RegistroConductorBdTests() => BaseDePruebas.Preparar();

        private static string CarnetDe(int id) =>
            ((string)DbHelper.EjecutarEscalar("SELECT carnetExtranjeria FROM Conductor WHERE idConductor = @id",
                DbHelper.Param("@id", id))).Trim();

        [FactBD]
        public void Carnet_sin_ceros_se_rechaza_si_ya_existe_con_ceros()
        {
            using (new TransaccionDePrueba())
            {
                // Carnet ficticio guardado con ceros, como los que deja la Lista de choferes
                int id = RegistroConductorService.Registrar(RegistroConductorService.TipoCarnet, "009990001",
                    "PRUEBA", "CARNET", "", "");
                Assert.Equal("009990001", CarnetDe(id));

                var ex = Assert.Throws<ErrorNegocioException>(() => RegistroConductorService.Registrar(
                    RegistroConductorService.TipoCarnet, "9990001", "PRUEBA", "OTRO", "", ""));
                Assert.Contains("carnet de extranjería 009990001", ex.Message);
            }
        }

        [FactBD]
        public void Carnet_se_guarda_con_ceros_y_choca_con_uno_antiguo_guardado_sin_ceros()
        {
            using (new TransaccionDePrueba())
            {
                int id = RegistroConductorService.Registrar(RegistroConductorService.TipoCarnet, "9990002",
                    "PRUEBA", "CEROS", "", "987654321");
                Assert.Equal("009990002", CarnetDe(id));

                // Un alta manual de antes de B5-2 guardaba el carnet tal cual
                DbHelper.EjecutarNonQuery(
                    "INSERT INTO Conductor (nombre, apPaterno, carnetExtranjeria, activo) VALUES ('PRUEBA', 'ANTIGUO', '9990003', 0)");

                var ex = Assert.Throws<ErrorNegocioException>(() => RegistroConductorService.Registrar(
                    RegistroConductorService.TipoCarnet, "009990003", "PRUEBA", "NUEVO", "", ""));
                Assert.Contains("(registrado como 9990003)", ex.Message);
            }
        }

        [FactBD]
        public void Edicion_valida_los_largos_y_conserva_lo_que_no_cambia()
        {
            using (new TransaccionDePrueba())
            {
                int id = RegistroConductorService.Registrar(RegistroConductorService.TipoCarnet, "9990004",
                    "PRUEBA", "EDICION", "", "");
                // Teléfono antiguo con guion (17 conductores lo tienen de otra forma)
                DbHelper.EjecutarNonQuery("UPDATE Conductor SET telefono = '044-123456' WHERE idConductor = @id",
                    DbHelper.Param("@id", id));

                RegistroConductorService.Actualizar(id, "PRUEBA DOS", "EDICION", "", "044-123456");
                Assert.Equal("PRUEBA DOS", (string)DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM Conductor WHERE idConductor = @id", DbHelper.Param("@id", id)));

                Assert.Throws<ErrorNegocioException>(
                    () => RegistroConductorService.Actualizar(id, "PRUEBA", new string('E', 21), "", "044-123456"));
                Assert.Throws<ErrorNegocioException>(
                    () => RegistroConductorService.Actualizar(int.MaxValue, "PRUEBA", "EDICION", "", ""));
            }
        }
    }

    /// <summary>
    /// M-078, de SOLO LECTURA: el nombre completo del conductor (RegistroConductorService.SqlNombreCompleto, el de
    /// los mensajes de duplicado, del cambio de estado y de la grilla de RegistroChoferes) no deja dobles espacios
    /// ni espacios en los bordes con columnas CHAR con relleno o sin apellido materno (antes 54 de 120 filas).
    /// </summary>
    public class NombreCompletoConductorBdTests
    {
        public NombreCompletoConductorBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void ObtenerDocumentos_no_deja_dobles_espacios_en_el_nombre()
        {
            foreach (var d in RegistroConductorService.ObtenerDocumentos())
            {
                string n = d.NombreCompleto ?? "";
                Assert.False(n.Contains("  "), $"Conductor #{d.IdConductor}: nombre con doble espacio");
                Assert.Equal(n.Trim(), n);
            }
        }

        [FactBD]
        public void El_nombre_de_la_grilla_no_deja_dobles_espacios()
        {
            object malos = DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM Conductor WHERE CHARINDEX('  ', " + RegistroConductorService.SqlNombreCompleto + ") > 0" +
                " OR DATALENGTH(" + RegistroConductorService.SqlNombreCompleto + ") <> DATALENGTH(LTRIM(RTRIM(" +
                RegistroConductorService.SqlNombreCompleto + ")))");
            Assert.Equal(0, Convert.ToInt32(malos));
        }
    }
}
