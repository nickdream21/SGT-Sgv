using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-20: la verificación pública (VerificarDocumento.aspx, sin sesión) lee con
    /// <see cref="VerificacionDocumentoService.Consultar"/> lo mismo que la página leía de sp_LD_VerificarDocumento:
    /// cada campo que se muestra, fijado contra el SP con una declaración vigente y con la orden ya retirada
    /// (firma anulada, sin número de orden, dos firmas). Un código que no existe da null. Todo dentro de una
    /// <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        private static void IgualAlSp(string codigo, DocumentoVerificado d)
        {
            DataSet ds = new DataSet();
            using (var cn = new System.Data.SqlClient.SqlConnection(DbHelper.ConnectionString))
            using (var cmd = new System.Data.SqlClient.SqlCommand("sp_LD_VerificarDocumento", cn) { CommandType = CommandType.StoredProcedure })
            using (var da = new System.Data.SqlClient.SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add(DbHelper.Param("@codigoVerificacion", codigo));
                da.Fill(ds);
            }
            Assert.Equal(2, ds.Tables.Count);
            DataRow r = ds.Tables[0].Rows.Cast<DataRow>().Single();
            Assert.NotNull(d);
            Assert.Equal(r["numeroOrdenViaje"] == DBNull.Value ? null : Convert.ToString(r["numeroOrdenViaje"]), d.NumeroOrdenViaje);
            Assert.Equal(r["etapa"] as string, d.Etapa);
            Assert.Equal(Convert.ToString(r["nombreFirmante"]), d.NombreFirmante);
            Assert.Equal(Convert.ToString(r["rolFirmante"]) == "ADMIN", d.FirmoAdministracion);
            Assert.Equal(Convert.ToDateTime(r["fechaHoraFirma"]), d.FechaHoraFirmaUtc);
            Assert.Equal(Convert.ToString(r["hashDocumento"]), d.HashDocumento);
            Assert.Equal(Convert.ToBoolean(r["anulada"]), d.Anulada);
            Assert.Equal(Convert.ToBoolean(r["reemplazado"]), d.Reemplazado);

            var filas = ds.Tables[1].Rows.Cast<DataRow>().ToList();
            Assert.Equal(filas.Count, d.Firmas.Count);
            for (int i = 0; i < filas.Count; i++)
            {
                DataRow f = filas[i];
                FirmaVerificada v = d.Firmas[i];
                Assert.Equal(f["etapa"] as string, v.Etapa);
                Assert.Equal(Convert.ToString(f["nombreFirmante"]), v.NombreFirmante);
                Assert.Equal(Convert.ToDateTime(f["fechaHoraFirma"]), v.FechaHoraFirmaUtc);
                Assert.Equal(Convert.ToString(f["estadoFirma"]) == "A", v.EsAnulacion);
                Assert.Equal(Convert.ToString(f["motivoAnulacion"]), v.MotivoAnulacion);
                Assert.Equal(Convert.ToBoolean(f["anulada"]), v.Anulada);
                Assert.Equal(f["codigoVerificacion"] == DBNull.Value ? "" : Convert.ToString(f["codigoVerificacion"]), v.CodigoVerificacion);
            }
        }

        [FactBD]
        public void VerificacionPublica_LeeLoMismoQueElSp_VigenteYRetirada()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                var input = NuevaLiquidacion(datos, idViaje);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);
                string codigo = CodigoVerificacion.Generar();
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor, codigo);

                // Declaración vigente: la orden con su número, firmada por el conductor, una firma.
                DocumentoVerificado vigente = VerificacionDocumentoService.Consultar(codigo);
                IgualAlSp(codigo, vigente);
                Assert.Equal(input.NumeroOrdenViaje, vigente.NumeroOrdenViaje);
                Assert.Equal("DECLARACION", vigente.Etapa);
                Assert.False(vigente.FirmoAdministracion);
                Assert.False(vigente.Anulada);
                Assert.False(vigente.Reemplazado);
                Assert.Single(vigente.Firmas);
                Assert.Equal(codigo, vigente.Firmas[0].CodigoVerificacion);
                Assert.Equal(64, vigente.HashDocumento.Trim().Length);

                // Retirada por el conductor: la firma queda anulada, la orden sin número y su anulación en la lista.
                Assert.Equal(1, DashboardConductorService.RetirarLiquidacion(idOrden, datos.IdConductor).Resultado);
                DocumentoVerificado retirada = VerificacionDocumentoService.Consultar(codigo);
                IgualAlSp(codigo, retirada);
                Assert.Null(retirada.NumeroOrdenViaje);
                Assert.True(retirada.Anulada);
                Assert.Equal(2, retirada.Firmas.Count);
                Assert.True(retirada.Firmas[1].EsAnulacion);
                Assert.StartsWith("RETIRO_CONDUCTOR", retirada.Firmas[1].MotivoAnulacion);

                // Un código bien formado que no existe.
                string otro;
                do { otro = CodigoVerificacion.Generar(); } while (otro == codigo);
                Assert.Null(VerificacionDocumentoService.Consultar(otro));
            }
        }
    }
}
