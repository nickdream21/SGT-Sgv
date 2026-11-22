using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E3-4: reglas de fuente sobre las transacciones de los SP del repo (sin BD, corre en CI).
    /// - Todo SP con BEGIN TRAN tiene SET XACT_ABORT ON antes de abrirla: un error que no salta
    ///   al CATCH (timeout del cliente, error de compilación diferida) deshace la transacción en
    ///   vez de dejarla abierta en la conexión del pool.
    /// - Ninguno tiene SET XACT_ABORT OFF.
    /// - Todo BEGIN CATCH (también uno anidado) relanza el error: THROW, o RAISERROR con severidad
    ///   11 o más (o no literal); una severidad de 0 a 10 es solo un aviso. Los textos entre
    ///   comillas no cuentan (un 'THROW' dentro de un mensaje no relanza nada).
    /// Solo Database/StoredProcedures: las migraciones aplicadas (Schema) no se editan y Scripts
    /// son ad hoc. Los comentarios (-- y /* */) no cuentan.
    /// </summary>
    public class TransaccionesEnSpTests
    {
        private static readonly string CarpetaSp =
            Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Database", "StoredProcedures");

        private static readonly Regex BeginTran =
            new Regex(@"\bBEGIN\s+TRAN(SACTION)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex XactAbortOn =
            new Regex(@"\bSET\s+XACT_ABORT\s+ON\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex XactAbortOff =
            new Regex(@"\bSET\s+XACT_ABORT\s+OFF\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MarcaCatch =
            new Regex(@"\b(BEGIN|END)\s+CATCH\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TextoEntreComillas =
            new Regex(@"N?'(?:[^']|'')*'", RegexOptions.Compiled);
        private static readonly Regex Throw =
            new Regex(@"\bTHROW\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // RAISERROR (mensaje, severidad, ...), ya sin textos: el mensaje es '', @variable o un número.
        private static readonly Regex Raiserror =
            new Regex(@"\bRAISERROR\s*\(\s*[^,()]*,\s*([^,()]+?)\s*,", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string SinTextos(string codigo) => TextoEntreComillas.Replace(codigo, "''");

        private static IEnumerable<(string Nombre, string Codigo)> Procedimientos() =>
            Directory.GetFiles(CarpetaSp, "*.sql")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => (Path.GetFileName(f), ErroresYEstadosEnSpTests.SinComentarios(File.ReadAllText(f))));

        /// <summary>Falta XACT_ABORT ON, o aparece después del primer BEGIN TRAN.</summary>
        internal static bool FaltaXactAbort(string codigoSinComentarios)
        {
            codigoSinComentarios = SinTextos(codigoSinComentarios);
            Match tran = BeginTran.Match(codigoSinComentarios);
            if (!tran.Success) return false;
            Match xact = XactAbortOn.Match(codigoSinComentarios);
            return !xact.Success || xact.Index > tran.Index;
        }

        internal static bool TieneXactAbortOff(string codigoSinComentarios) =>
            XactAbortOff.IsMatch(SinTextos(codigoSinComentarios));

        /// <summary>
        /// Cuerpos de los bloques CATCH (de BEGIN CATCH a su END CATCH), anidados incluidos: cada uno
        /// con todo lo que tenga dentro, también sus bloques interiores.
        /// </summary>
        internal static List<string> CuerposCatch(string codigoSinComentarios)
        {
            var cuerpos = new List<string>();
            var abiertos = new Stack<int>();
            foreach (Match m in MarcaCatch.Matches(codigoSinComentarios))
            {
                if (m.Groups[1].Value.Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
                    abiertos.Push(m.Index + m.Length);
                else if (abiertos.Count > 0)
                {
                    int inicio = abiertos.Pop();
                    cuerpos.Add(codigoSinComentarios.Substring(inicio, m.Index - inicio));
                }
            }
            return cuerpos;
        }

        /// <summary>THROW, o RAISERROR con severidad no literal o de 11 o más (fuera de los textos).</summary>
        internal static bool Relanza(string cuerpo)
        {
            string codigo = SinTextos(cuerpo);
            if (Throw.IsMatch(codigo)) return true;
            return Raiserror.Matches(codigo).Cast<Match>().Any(r =>
                !int.TryParse(r.Groups[1].Value, out int severidad) || severidad >= 11);
        }

        /// <summary>Número de bloques CATCH que no relanzan el error.</summary>
        internal static int CatchesQueNoRelanzan(string codigoSinComentarios) =>
            CuerposCatch(codigoSinComentarios).Count(c => !Relanza(c));

        [Fact]
        public void Hay_procedimientos_con_transaccion_que_revisar()
        {
            Assert.True(Procedimientos().Count(p => BeginTran.IsMatch(p.Codigo)) >= 25);
        }

        [Fact]
        public void Todo_sp_con_transaccion_tiene_XACT_ABORT_ON_antes_de_abrirla()
        {
            Assert.Empty(Procedimientos().Where(p => FaltaXactAbort(p.Codigo)).Select(p => p.Nombre));
        }

        [Fact]
        public void Ningun_sp_apaga_XACT_ABORT()
        {
            Assert.Empty(Procedimientos().Where(p => TieneXactAbortOff(p.Codigo)).Select(p => p.Nombre));
        }

        /// <summary>SP con un CATCH que a propósito no relanza, con su motivo. Cambiar esta lista es una decisión.</summary>
        private static readonly Dictionary<string, string> CatchSinRelanzarPermitido = new Dictionary<string, string>
        {
            // La limpieza de vencidos corre tras el COMMIT de la reserva: si fallara (p. ej. un interbloqueo con otra
            // reserva), el C# reintentaría una reserva ya hecha y contaría el fallo dos veces (E4-4).
            ["sp_US_ReservarIntentoAcceso.sql"] = "limpieza de vencidos tras el COMMIT, sin transacción abierta",
        };

        [Fact]
        public void Todo_CATCH_de_un_sp_relanza_el_error()
        {
            Assert.Empty(Procedimientos()
                .Where(p => CatchesQueNoRelanzan(p.Codigo) > (CatchSinRelanzarPermitido.ContainsKey(p.Nombre) ? 1 : 0))
                .Select(p => p.Nombre));
        }

        [Fact]
        public void Los_CATCH_sin_relanzar_permitidos_existen_y_no_tienen_transaccion_abierta()
        {
            foreach (string nombre in CatchSinRelanzarPermitido.Keys)
            {
                var sp = Procedimientos().SingleOrDefault(p => p.Nombre == nombre);
                Assert.True(sp.Nombre != null, nombre + " está en CatchSinRelanzarPermitido pero no existe.");
                Assert.Equal(1, CatchesQueNoRelanzan(sp.Codigo));
            }
        }

        [Theory]
        [InlineData("SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION; COMMIT;", false)]
        [InlineData("SET NOCOUNT ON; BEGIN TRAN; COMMIT;", true)]
        [InlineData("SET NOCOUNT ON; BEGIN TRAN; SET XACT_ABORT ON; COMMIT;", true)]
        [InlineData("SET NOCOUNT ON; -- SET XACT_ABORT ON;\n BEGIN TRAN; COMMIT;", true)]
        [InlineData("SET NOCOUNT ON; /* SET XACT_ABORT ON; */ BEGIN TRAN; COMMIT;", true)]
        [InlineData("SET NOCOUNT ON; SELECT 1; -- BEGIN TRAN\n", false)]
        public void Detecta_XACT_ABORT_faltante_ignorando_comentarios(string sql, bool falta)
        {
            Assert.Equal(falta, FaltaXactAbort(ErroresYEstadosEnSpTests.SinComentarios(sql)));
        }

        [Theory]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH IF @@TRANCOUNT > 0 ROLLBACK; THROW; END CATCH", 0)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR('x', 16, 1); END CATCH", 0)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH IF @@TRANCOUNT > 0 ROLLBACK; SET @resultado = 0; END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH ROLLBACK; -- THROW;\n END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH ROLLBACK; PRINT 'THROW'; END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH ROLLBACK; PRINT N'RAISERROR(''x'', 16, 1)'; END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR('aviso', 10, 1); END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR('aviso, con coma', 0, 1); END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR(N'error', 11, 1); END CATCH", 0)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR(@msg, @sev, 1); END CATCH", 0)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH RAISERROR (50001, 16, 1); END CATCH", 0)]
        [InlineData("BEGIN TRY BEGIN TRY SELECT 1; END TRY BEGIN CATCH SET @x = 1; END CATCH END TRY BEGIN CATCH THROW; END CATCH", 1)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH BEGIN TRY ROLLBACK; END TRY BEGIN CATCH THROW; END CATCH; THROW; END CATCH", 0)]
        [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH BEGIN TRY ROLLBACK; END TRY BEGIN CATCH SET @x = 1; END CATCH; THROW; END CATCH", 1)]
        public void Detecta_CATCH_que_se_traga_el_error(string sql, int esperados)
        {
            Assert.Equal(esperados, CatchesQueNoRelanzan(ErroresYEstadosEnSpTests.SinComentarios(sql)));
        }

        [Theory]
        [InlineData("SET NOCOUNT ON; SET XACT_ABORT OFF; BEGIN TRAN; COMMIT;", true)]
        [InlineData("SET NOCOUNT ON; SET   xact_abort   off;", true)]
        [InlineData("SET NOCOUNT ON; SET XACT_ABORT ON; -- SET XACT_ABORT OFF\n", false)]
        [InlineData("SET NOCOUNT ON; SET XACT_ABORT ON; PRINT 'SET XACT_ABORT OFF';", false)]
        public void Detecta_XACT_ABORT_OFF(string sql, bool apagado)
        {
            Assert.Equal(apagado, TieneXactAbortOff(ErroresYEstadosEnSpTests.SinComentarios(sql)));
        }
    }
}
