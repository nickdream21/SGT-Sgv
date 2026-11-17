using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Transactions;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// "Finalizar lote" de RegistroDespacho todo o nada contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>. Los archivos van a una carpeta Uploads temporal.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class FinalizarLoteBdTests
    {
        public FinalizarLoteBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void Lote_nacional_con_factura_y_dos_conductores_guarda_todo()
        {
            string raiz = NuevaRaiz();
            try
            {
                using (new TransaccionDePrueba())
                {
                    var lote = LoteNacional(FechaHelper.Hoy().AddDays(15), 2);
                    string temp = ArchivoTemp(raiz, "Factura", ".pdf");
                    lote.Documentacion.FacturaArchivoRutaTemp = temp;
                    lote.Documentacion.FacturaArchivoNombreOriginal = "factura.pdf";

                    var r = FinalizarLoteService.Finalizar(lote, raiz);

                    Assert.True(r.Ok, string.Join(" ", r.Errores));
                    Assert.NotNull(r.IdFactura);
                    Assert.Equal(2, r.IdsCreados.Count);
                    Assert.Empty(r.IdsCompletados);
                    Assert.Equal(r.IdsCreados, r.IdsPorConductor);
                    foreach (int id in r.IdsCreados)
                        Assert.Equal(r.IdFactura.Value, Convert.ToInt32(DbHelper.EjecutarEscalar(
                            "SELECT idFactura FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", id))));

                    DataTable docs = DbHelper.ConsultarTabla("SELECT rutaArchivo, nombreOriginal FROM DocumentosFactura WHERE idFactura = @f",
                        DbHelper.Param("@f", r.IdFactura.Value));
                    Assert.Single(docs.Rows);
                    Assert.Equal("factura.pdf", docs.Rows[0]["nombreOriginal"]);
                    string ruta = Convert.ToString(docs.Rows[0]["rutaArchivo"]);
                    Assert.StartsWith($"~/Uploads/Factura/", ruta);
                    Assert.True(File.Exists(Fisica(raiz, ruta)));
                    Assert.False(File.Exists(temp));   // el temporal se borra tras el commit
                }
            }
            finally { Borrar(raiz); }
        }

        [FactBD]
        public void Lote_internacional_con_cpic_y_manifiestos()
        {
            string raiz = NuevaRaiz();
            try
            {
                using (new TransaccionDePrueba())
                {
                    var lote = LoteNacional(FechaHelper.Hoy().AddDays(16), 1);
                    lote.EsInternacional = true;
                    lote.IdPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 1 ORDER BY idPlanta"));
                    lote.Documentacion.NumeroCPIC = "9" + new Random().Next(10000000, 99999999);
                    lote.Documentacion.FechaEmisionCPIC = FechaHelper.Hoy();
                    lote.Documentacion.ValorFlete = 1841.4m;
                    lote.Documentacion.CPICArchivoRutaTemp = ArchivoTemp(raiz, "CPIC", ".pdf");
                    lote.Documentacion.CPICArchivoNombreOriginal = "cpic.pdf";
                    var c = lote.Conductores[0];
                    c.ManifiestoCruceRutaTemp = ArchivoTemp(raiz, "Manifiesto", ".pdf");
                    c.ManifiestoCruceNombreOriginal = "cruce.pdf";
                    c.ManifiestoRegresoRutaTemp = ArchivoTemp(raiz, "Manifiesto", ".pdf");
                    c.ManifiestoRegresoNombreOriginal = "retorno.pdf";

                    var r = FinalizarLoteService.Finalizar(lote, raiz);

                    Assert.True(r.Ok, string.Join(" ", r.Errores));
                    Assert.NotNull(r.IdCpic);
                    int idDespacho = Assert.Single(r.IdsCreados);
                    Assert.Equal(r.IdCpic.Value, Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT idCPIC FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", idDespacho))));
                    Assert.Equal(r.IdFactura.Value, Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT idFactura FROM CPIC WHERE idCPIC = @c", DbHelper.Param("@c", r.IdCpic.Value))));
                    Assert.Equal(1, Contar("SELECT COUNT(*) FROM DocumentosCPIC WHERE idCPIC = @id AND activo = 1", r.IdCpic.Value));

                    DataTable man = DbHelper.ConsultarTabla("SELECT tipoManifiesto, rutaArchivo FROM DocumentosManifiesto WHERE idDespacho = @d ORDER BY tipoManifiesto",
                        DbHelper.Param("@d", idDespacho));
                    Assert.Equal(new[] { "CRUCE", "RETORNO" }, man.Rows.Cast<DataRow>().Select(x => x[0].ToString()).ToArray());
                    foreach (DataRow m in man.Rows)
                        Assert.True(File.Exists(Fisica(raiz, m["rutaArchivo"].ToString())));
                    Assert.False(File.Exists(c.ManifiestoCruceRutaTemp));
                    Assert.False(File.Exists(lote.Documentacion.CPICArchivoRutaTemp));
                }
            }
            finally { Borrar(raiz); }
        }

        [FactBD]
        public void Completa_el_despacho_programado_con_sus_documentos()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fecha = FechaHelper.Hoy().AddDays(17);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho BETWEEN @f AND DATEADD(day, 1, @f)", DbHelper.Param("@f", fecha));
                var lote = LoteNacional(fecha, 1);
                var c = lote.Conductores[0];

                var prog = ProgramacionDespachoService.Programar(new List<ProgramacionDespachoService.FilaElegida>
                {
                    new ProgramacionDespachoService.FilaElegida { IdConductor = c.IdConductor, IdTracto = c.IdTracto, IdCarreta = c.IdCarreta, Hora = new TimeSpan(5, 30, 0), Etiqueta = "1" }
                }, lote.IdCliente, lote.IdPlanta, fecha, esExportacion: false, usuario: "test");
                int idProgramado = Assert.Single(prog.Creados);
                c.IdDespachoProgramado = idProgramado;

                var r = FinalizarLoteService.Finalizar(lote, null);

                Assert.True(r.Ok, string.Join(" ", r.Errores));
                Assert.Empty(r.IdsCreados);
                Assert.Equal(new[] { idProgramado }, r.IdsCompletados);
                DataRow d = DbHelper.ConsultarTabla("SELECT idFactura, guiaRemitente, numeroPedido FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", idProgramado)).Rows[0];
                Assert.Equal(r.IdFactura.Value, Convert.ToInt32(d["idFactura"]));
                Assert.Equal(c.GuiaRemitente, d["guiaRemitente"]);
                Assert.Equal(lote.NumeroPedido, d["numeroPedido"]);

                // Otra vez el mismo lote (con otra factura): el programado ya no espera documentos
                lote.Documentacion.NumeroFactura += "B";
                var otra = FinalizarLoteService.Finalizar(lote, null);
                Assert.False(otra.Ok);
                Assert.Contains(otra.Errores, e => e.Contains("ya no espera documentos"));
            }
        }

        [FactBD]
        public void Factura_ya_registrada_no_guarda_nada()
        {
            using (new TransaccionDePrueba())
            {
                var lote = LoteNacional(FechaHelper.Hoy().AddDays(18), 2);
                RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote);   // otro la registró mientras tanto

                var r = FinalizarLoteService.Finalizar(lote, null);

                Assert.False(r.Ok);
                Assert.Contains(r.Errores, e => e.Contains($"'{lote.Documentacion.NumeroFactura}' ya está registrado"));
                Assert.Empty(r.IdsPorConductor);
                Assert.Equal(0, ContarDespachosConGuias(lote));
            }
        }

        [FactBD]
        public void Guia_de_remitente_repetida_no_guarda_nada()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fecha = FechaHelper.Hoy().AddDays(19);
                var primero = LoteNacional(fecha, 1);
                Assert.True(FinalizarLoteService.Finalizar(primero, null).Ok);

                var segundo = LoteNacional(fecha, 2);
                segundo.Conductores[1].GuiaRemitente = primero.Conductores[0].GuiaRemitente;

                var r = FinalizarLoteService.Finalizar(segundo, null);

                Assert.False(r.Ok);
                Assert.Contains(r.Errores, e => e.Contains(primero.Conductores[0].GuiaRemitente) && e.Contains("ya está registrada"));
                Assert.Equal(0, Contar("SELECT COUNT(*) FROM Despachos WHERE guiaRemitente = @id AND activo = 1", segundo.Conductores[0].GuiaRemitente));
                Assert.Equal(0, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", segundo.Documentacion.NumeroFactura));
            }
        }

        /// <summary>
        /// La factura es única por número y cliente (migración 49): el mismo número de otro cliente no bloquea el lote;
        /// el del mismo cliente sí (<see cref="Factura_ya_registrada_no_guarda_nada"/>).
        /// </summary>
        [FactBD]
        public void Factura_con_el_mismo_numero_de_otro_cliente_no_bloquea()
        {
            using (new TransaccionDePrueba())
            {
                var lote = LoteNacional(FechaHelper.Hoy().AddDays(22), 1);
                var deOtro = new LoteDespachos
                {
                    IdCliente = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 AND idCliente <> @c ORDER BY idCliente", DbHelper.Param("@c", lote.IdCliente)))
                };
                deOtro.Documentacion.NumeroFactura = lote.Documentacion.NumeroFactura;
                RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", deOtro);

                var r = FinalizarLoteService.Finalizar(lote, null);

                Assert.True(r.Ok, string.Join(" ", r.Errores));
                Assert.Equal(2, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", lote.Documentacion.NumeroFactura));
            }
        }

        /// <summary>Al final (el error aborta la transacción): falla el 2.º conductor y no queda nada, ni en BD ni en disco.</summary>
        [FactBD]
        public void Error_en_el_segundo_conductor_no_guarda_nada()
        {
            string raiz = NuevaRaiz();
            try
            {
                using (new TransaccionDePrueba())
                {
                    var lote = LoteNacional(FechaHelper.Hoy().AddDays(20), 2);
                    string temp = ArchivoTemp(raiz, "Factura", ".pdf");
                    lote.Documentacion.FacturaArchivoRutaTemp = temp;
                    lote.Documentacion.FacturaArchivoNombreOriginal = "factura.pdf";
                    lote.Conductores[1].IdPlanta = -1;   // sp_CrearDespacho: "La planta indicada no existe"

                    var r = FinalizarLoteService.Finalizar(lote, raiz);

                    Assert.False(r.Ok);
                    string error = Assert.Single(r.Errores);
                    Assert.StartsWith("No se guardó nada. " + lote.Conductores[1].NombreConductor, error);
                    Assert.Null(r.IdFactura);
                    Assert.Empty(r.IdsCreados);
                    Assert.Empty(r.IdsPorConductor);
                    Assert.True(File.Exists(temp));   // se puede reintentar
                    Assert.DoesNotContain(Directory.GetFiles(Path.Combine(raiz, "Factura"), "*", SearchOption.AllDirectories),
                        f => !f.StartsWith(Path.GetDirectoryName(temp), StringComparison.OrdinalIgnoreCase));   // destino vacío
                    Assert.Equal(TransactionStatus.Aborted, Transaction.Current.TransactionInformation.Status);
                }
            }
            finally { Borrar(raiz); }
        }

        /// <summary>
        /// Al final: el error sale dentro del TRY de sp_CrearDespacho (FK de tracto, después de su BEGIN TRAN, con
        /// ROLLBACK en el CATCH). Tampoco queda nada y el mensaje no muestra texto técnico. La transacción de la prueba
        /// ya está abortada: solo se mira el resultado y el disco.
        /// </summary>
        [FactBD]
        public void Error_dentro_del_sp_en_el_segundo_conductor_no_guarda_nada()
        {
            string raiz = NuevaRaiz();
            try
            {
                using (new TransaccionDePrueba())
                {
                    var lote = LoteNacional(FechaHelper.Hoy().AddDays(21), 2);
                    string temp = ArchivoTemp(raiz, "Factura", ".pdf");
                    lote.Documentacion.FacturaArchivoRutaTemp = temp;
                    lote.Documentacion.FacturaArchivoNombreOriginal = "factura.pdf";
                    lote.Conductores[1].IdTracto = -1;   // FK_Despachos_Tracto, no se valida antes del TRY

                    var r = FinalizarLoteService.Finalizar(lote, raiz);

                    Assert.False(r.Ok);
                    string error = Assert.Single(r.Errores);
                    Assert.StartsWith("No se guardó nada. " + lote.Conductores[1].NombreConductor + ": ", error);
                    foreach (string tecnico in new[] { "FOREIGN KEY", "FK_", "REFERENCE", "conflict", "Despachos", "Tracto" })
                        Assert.DoesNotContain(tecnico, error, StringComparison.OrdinalIgnoreCase);
                    Assert.Null(r.IdFactura);
                    Assert.Empty(r.IdsCreados);
                    Assert.Empty(r.IdsCompletados);
                    Assert.Empty(r.IdsPorConductor);
                    Assert.True(File.Exists(temp));
                    Assert.DoesNotContain(Directory.GetFiles(Path.Combine(raiz, "Factura"), "*", SearchOption.AllDirectories),
                        f => !f.StartsWith(Path.GetDirectoryName(temp), StringComparison.OrdinalIgnoreCase));   // destino vacío
                }
            }
            finally { Borrar(raiz); }
        }

        // ── Ayudantes ────────────────────────────────────────────────────────

        private static LoteDespachos LoteNacional(DateTime fecha, int conductores)
        {
            string sufijo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, nombre FROM Cliente WHERE activo = 1 ORDER BY idCliente").Rows[0];
            int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
            var choferes = DbHelper.ConsultarTabla($"SELECT TOP {conductores} idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC").Rows;
            var tractos = DbHelper.ConsultarTabla($"SELECT TOP {conductores} idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows;
            var carretas = DbHelper.ConsultarTabla($"SELECT TOP {conductores} idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows;

            var lote = new LoteDespachos
            {
                FechaProgramacion = fecha.ToString("yyyy-MM-dd"), IdCliente = Convert.ToInt32(cliente["idCliente"]),
                NombreCliente = cliente["nombre"].ToString(), NumeroPedido = "4490" + new Random().Next(100000, 999999),
                TipoOperacion = "CARGA", EsInternacional = false, IdPlanta = idPlanta, PlantaOperacion = "Prueba", UsuarioCreacion = "test"
            };
            lote.Documentacion.NumeroFactura = "F999-" + sufijo;
            lote.Documentacion.FechaEmisionFactura = fecha;
            lote.Documentacion.ValorTotalFactura = 1500m;
            for (int i = 0; i < conductores; i++)
                lote.Conductores.Add(new ConductorLote
                {
                    IdConductor = Convert.ToInt32(choferes[i][0]), NombreConductor = "CONDUCTOR " + (i + 1),
                    IdTracto = Convert.ToInt32(tractos[i][0]), IdCarreta = Convert.ToInt32(carretas[i][0]),
                    GuiaRemitente = $"T999-{sufijo}{i}", GuiaTransportista = $"V999-{sufijo}{i}"
                });
            return lote;
        }

        private static int ContarDespachosConGuias(LoteDespachos lote) =>
            lote.Conductores.Sum(c => Contar("SELECT COUNT(*) FROM Despachos WHERE guiaRemitente = @id", c.GuiaRemitente));

        private static int Contar(string sql, object id) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, DbHelper.Param("@id", id)));

        private static string NuevaRaiz() =>
            Path.Combine(Path.GetTempPath(), "sgv_lote_" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;

        private static string ArchivoTemp(string raiz, string carpeta, string extension)
        {
            string dir = Path.Combine(raiz, carpeta, "_temp");
            Directory.CreateDirectory(dir);
            string ruta = Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
            File.WriteAllBytes(ruta, new byte[] { 37, 80, 68, 70 });
            return ruta;
        }

        private static string Fisica(string raiz, string rutaVirtual) =>
            Path.Combine(raiz, rutaVirtual.Substring("~/Uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

        private static void Borrar(string raiz)
        {
            if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
        }
    }
}
