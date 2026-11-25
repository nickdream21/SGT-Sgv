using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Views;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-13: los errores que antes se tapaban. RegistroDespacho muestra el mensaje de negocio del SP en lugar del texto
    /// fijo de la pantalla; <see cref="MensajeErrorHelper.ParaUsuarioOTexto"/> elige el texto; <see cref="LecturaSegura"/>
    /// (el GetSafeValue de ListaDespachos) avisa una vez por columna inexistente sin lanzar.
    /// </summary>
    public class ErroresQueNoSeTapanTests
    {
        /// <summary>
        /// SqlException como la de un RAISERROR/THROW de un SP (número ≥ 50000), armada por reflexión: sus constructores
        /// son internos.
        /// </summary>
        internal static SqlException ErrorDeSp(string mensaje, int numero = 50000)
        {
            const BindingFlags interno = BindingFlags.NonPublic | BindingFlags.Instance;
            ConstructorInfo ctorError = typeof(SqlError).GetConstructors(interno)
                .OrderByDescending(c => c.GetParameters().Length).First();
            object[] args = ctorError.GetParameters().Select(p =>
            {
                if (p.Name == "infoNumber") return (object)numero;
                if (p.Name == "errorMessage") return mensaje;
                if (p.Name == "errorClass") return (byte)16;
                if (p.ParameterType == typeof(string)) return "prueba";
                if (p.ParameterType == typeof(Exception)) return null;
                return Activator.CreateInstance(p.ParameterType);
            }).ToArray();
            var error = (SqlError)ctorError.Invoke(args);

            var errores = (SqlErrorCollection)typeof(SqlErrorCollection).GetConstructors(interno)[0].Invoke(null);
            typeof(SqlErrorCollection).GetMethod("Add", interno).Invoke(errores, new object[] { error });

            MethodInfo crear = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "CreateException")
                .OrderBy(m => m.GetParameters().Length).First();
            object[] argsCrear = crear.GetParameters().Select(p =>
                p.ParameterType == typeof(SqlErrorCollection) ? errores
                : p.ParameterType == typeof(string) ? (object)"16.0"
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            return (SqlException)crear.Invoke(null, argsCrear);
        }

        [Fact]
        public void ErrorDeSp_ArmadoParaLaPrueba_EsDeNegocio()
        {
            SqlException ex = ErrorDeSp("El conductor ya tiene un viaje en progreso.");
            Assert.Equal(50000, ex.Number);
            Assert.Equal("El conductor ya tiene un viaje en progreso.", MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void RegistroDespacho_ErrorDeNegocioDelSp_MuestraElTextoDelSp()
        {
            string texto = RegistroDespacho.TextoErrorOperacion("Error al crear nuevo viaje.", "btnCrearNuevoViaje_Click",
                ErrorDeSp("El conductor ya tiene un viaje en progreso."));
            Assert.Equal("El conductor ya tiene un viaje en progreso.", texto);
        }

        [Fact]
        public void RegistroDespacho_ErrorDeNegocioDeUnServicio_MuestraSuTexto()
        {
            string texto = RegistroDespacho.TextoErrorOperacion("Error al iniciar el lote. Intente nuevamente.", "btnIniciarLote_Click",
                new ErrorNegocioException("Ya tiene un lote activo."));
            Assert.Equal("Ya tiene un lote activo.", texto);
        }

        [Fact]
        public void RegistroDespacho_ErrorTecnico_MuestraElTextoDeLaPantalla()
        {
            string texto = RegistroDespacho.TextoErrorOperacion("Error al crear nuevo viaje.", "btnCrearNuevoViaje_Click",
                new InvalidOperationException("Invalid column name 'idPlantaX'."));
            Assert.Equal("Error al crear nuevo viaje.", texto);
            // Un error de SQL que no es de negocio (número < 50000) tampoco se muestra.
            Assert.Equal("Error al crear nuevo viaje.", RegistroDespacho.TextoErrorOperacion("Error al crear nuevo viaje.", "x",
                ErrorDeSp("Violation of PRIMARY KEY constraint 'PK_X'.", 2627)));
        }

        [Fact]
        public void ParaUsuarioOTexto_SinTextoPropio_UsaElGenerico()
        {
            Assert.Equal(MensajeError.Generico, MensajeErrorHelper.ParaUsuarioOTexto(new Exception("x"), null, "Error en {Contexto}", "Prueba"));
            Assert.Equal(MensajeError.Generico, MensajeErrorHelper.ParaUsuarioOTexto(new Exception("x"), " ", "Error en {Contexto}", "Prueba"));
            Assert.Equal("Envuelto", MensajeErrorHelper.ParaUsuarioOTexto(
                new Exception("técnico", new ErrorNegocioException("Envuelto")), "Texto propio", "Error en {Contexto}", "Prueba"));
        }

        // ── Aprobar con MovimientosViaje apagado (ronda 1 de E6-13) ──────────────────────────────

        [Fact]
        public void MovimientosApagados_ElServidorRespondeExitoSinMovimientos()
        {
            string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(FuentesWeb.RaizWeb(), "Views", "LiquidacionesPendientes.aspx.cs"));
            int i = cs.IndexOf("public static object ObtenerMovimientosParaRevision", StringComparison.Ordinal);
            Assert.True(i > 0);
            string metodo = cs.Substring(i, cs.IndexOf("MovimientosViajeService.ObtenerParaRevision", i, StringComparison.Ordinal) - i);
            // Antes respondía success = false ("Funcionalidad desactivada.") y Aprobar quedaba esperando para siempre.
            Assert.Contains("if (!MovimientosViajeService.Habilitado)", metodo);
            Assert.Contains("success = true, desactivada = true", metodo);
            Assert.DoesNotContain("success = false, message = \"Funcionalidad desactivada", metodo);
        }

        [Fact]
        public void MovimientosApagados_AprobarNoBloqueaYNoEnviaIds()
        {
            string js = System.IO.File.ReadAllText(System.IO.Path.Combine(FuentesWeb.RaizWeb(), "Scripts", "paginas", "LiquidacionesPendientes.js"));
            // La respuesta "desactivada" marca "no aplica" sin llenar _movimientosRevision (sigue null).
            Assert.Matches(@"if \(r && r\.desactivada\) \{ _movimientosRevisionNoAplica = true; return; \}", js);
            // null = no tocar los ya unidos: idsMovimientosMarcados devuelve null mientras no se cargaron.
            Assert.Matches(@"function idsMovimientosMarcados\(\) \{\s*if \(_movimientosRevision === null\) return null;", js);
            // Aprobar solo espera la carga si la función está encendida.
            Assert.Contains("if (_movimientosRevision === null && !_movimientosRevisionNoAplica) {", js);
            // Un success = false del servidor (BD caída) marca el fallo y avisa.
            Assert.Matches(@"if \(!r \|\| !r\.success\) \{\s*_movimientosRevisionFallo = true;", js);
        }

        [Fact]
        public void IdsMovimientosNulos_NoTocanLosYaUnidos()
        {
            // Lado servidor de aprobar: sin ids (null) no llama a UnirAOrden.
            string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(FuentesWeb.RaizWeb(), "Services", "Liquidaciones", "LiquidacionesPendientesService.cs"));
            Assert.Matches(@"if \(idsMovimientos != null\)\s*MovimientosViajeService\.UnirAOrden\(", cs);
        }

        // ── LecturaSegura (GetSafeValue) ──────────────────────────────────────────────────────

        private static DataRow Fila()
        {
            var t = new DataTable();
            t.Columns.Add("flete", typeof(decimal));
            t.Columns.Add("nombre", typeof(string));
            t.Columns.Add("vacio", typeof(int));
            t.Columns.Add("texto", typeof(string));
            DataRow r = t.NewRow();
            r["flete"] = 1250.5m;
            r["nombre"] = "ABC-123";
            r["vacio"] = DBNull.Value;
            r["texto"] = "no es número";
            t.Rows.Add(r);
            return r;
        }

        [Fact]
        public void LecturaSegura_LeeYConvierteComoElGetSafeValueDeAntes()
        {
            DataRow r = Fila();
            Assert.Equal(1250.5m, LecturaSegura.Leer<decimal?>(c => r[c], "flete", "PruebaLee"));
            Assert.Equal("ABC-123", LecturaSegura.Leer<string>(c => r[c], "nombre", "PruebaLee"));
            Assert.Equal(7, LecturaSegura.Leer(c => r[c], "vacio", "PruebaLee", 7));   // DBNull → por defecto, sin aviso
            Assert.Equal(1250.5m, LecturaSegura.Leer<decimal>(c => r[c], "flete", "PruebaLee"));
        }

        [Fact]
        public void LecturaSegura_ColumnaInexistente_DevuelveElDefectoSinLanzarYAvisaUnaVez()
        {
            DataRow r = Fila();
            string origen = "PruebaColumna" + Guid.NewGuid().ToString("N");
            for (int i = 0; i < 3; i++)   // tres "filas": el resultado es el mismo y no lanza
                Assert.Equal(-1, LecturaSegura.Leer(c => r[c], "fleteMalEscrito", origen, -1));
            Assert.Null(LecturaSegura.Leer<string>(c => r[c], "fleteMalEscrito", origen));
            // Ya avisada: el siguiente intento para la misma columna no deja otra advertencia.
            Assert.False(LecturaSegura.AvisarUnaVez(new ArgumentException("x"), origen, "fleteMalEscrito", "no está en el resultado"));
            // Otra columna del mismo origen sí avisa (una vez).
            Assert.True(LecturaSegura.AvisarUnaVez(new ArgumentException("x"), origen, "otraColumna", "no está en el resultado"));
            Assert.False(LecturaSegura.AvisarUnaVez(new ArgumentException("x"), origen, "otraColumna", "no está en el resultado"));
        }

        [Fact]
        public void LecturaSegura_ValorQueNoSeConvierte_DevuelveElDefecto()
        {
            DataRow r = Fila();
            string origen = "PruebaConversion" + Guid.NewGuid().ToString("N");
            Assert.Equal(0, LecturaSegura.Leer<int>(c => r[c], "texto", origen));
            Assert.False(LecturaSegura.AvisarUnaVez(new FormatException("x"), origen, "texto", "no se pudo convertir"));
        }
    }
}
