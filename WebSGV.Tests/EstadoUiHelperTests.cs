using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-14: EstadoUiHelper devuelve el modificador del sello común (<c>sgv-estado-*</c>) para los estados de
    /// liquidación, despacho y seguimiento; lo desconocido queda gris (""), nunca texto libre en la clase.
    /// </summary>
    public class EstadoUiHelperTests
    {
        [Theory]
        [InlineData("APROBADO", "sgv-estado-ok")]
        [InlineData("COMPLETADO", "sgv-estado-ok")]
        [InlineData("PENDIENTE", "sgv-estado-info")]
        [InlineData("POR_CONFORMAR", "sgv-estado-alerta")]
        [InlineData("REABIERTO", "sgv-estado-peligro")]
        [InlineData("RECHAZADO", "sgv-estado-peligro")]
        [InlineData(" por_conformar ", "sgv-estado-alerta")]
        [InlineData("aprobado", "sgv-estado-ok")]
        [InlineData("OTRO", "")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Liquidacion(string estado, string esperado) =>
            Assert.Equal(esperado, EstadoUiHelper.ClaseEstadoLiquidacion(estado));

        [Theory]
        [InlineData("COMPLETADO", "sgv-estado-ok")]
        [InlineData("FINALIZADO", "sgv-estado-ok")]
        [InlineData("PROGRAMADO", "sgv-estado-info")]
        [InlineData("EN_PROGRESO", "sgv-estado-alerta")]
        [InlineData("ENPROGRESO", "sgv-estado-alerta")]
        [InlineData("ANULADO", "sgv-estado-peligro")]
        [InlineData("cancelado", "sgv-estado-peligro")]
        [InlineData("<script>", "")]
        [InlineData(null, "")]
        public void Despacho(string estado, string esperado) =>
            Assert.Equal(esperado, EstadoUiHelper.ClaseEstadoDespacho(estado));

        [Theory]
        [InlineData("FINALIZADO", "sgv-estado-ok")]
        [InlineData("EN_CURSO", "sgv-estado-info")]
        [InlineData("CANCELADO", "sgv-estado-peligro")]
        [InlineData("COMPLETADO", "")]
        [InlineData(null, "")]
        public void Seguimiento(string estado, string esperado) =>
            Assert.Equal(esperado, EstadoUiHelper.ClaseEstadoSeguimiento(estado));

        [Fact]
        public void DbNullEsGris()
        {
            Assert.Equal("", EstadoUiHelper.ClaseEstadoLiquidacion(DBNull.Value));
            Assert.Equal("", EstadoUiHelper.ClaseEstadoDespacho(DBNull.Value));
            Assert.Equal("", EstadoUiHelper.ClaseEstadoSeguimiento(DBNull.Value));
        }

        [Fact]
        public void UsaLasConstantesDeLosServicios()
        {
            Assert.Equal(EstadoUiHelper.ClaseOk, EstadoUiHelper.ClaseEstadoLiquidacion(Services.Liquidaciones.EstadosOrden.Aprobado));
            Assert.Equal(EstadoUiHelper.ClaseAlerta, EstadoUiHelper.ClaseEstadoLiquidacion(Services.Liquidaciones.EstadosOrden.PorConformar));
            Assert.Equal(EstadoUiHelper.ClaseInfo, EstadoUiHelper.ClaseEstadoSeguimiento(Services.Exportacion.EstadoSeguimiento.EnCurso));
        }

        [Fact]
        public void LasClasesExistenEnSgvBase()
        {
            var css = System.IO.File.ReadAllText(System.IO.Path.Combine(AvisoHelperTests.RaizWeb(), "Content", "sgv-base.css"));
            foreach (var clase in new[] { EstadoUiHelper.ClaseOk, EstadoUiHelper.ClaseInfo, EstadoUiHelper.ClaseAlerta, EstadoUiHelper.ClasePeligro })
                Assert.Contains("." + clase + " {", css);
        }

        // E5-16: pregunta de data-confirmar del botón Activar/Desactivar de los catálogos.
        [Theory]
        [InlineData(true, "el tracto", "ABC-123", "¿Desactivar el tracto ABC-123?")]
        [InlineData(false, "el semirremolque", " XYZ-789 ", "¿Activar el semirremolque XYZ-789?")]
        [InlineData(true, "la planta", "", "¿Desactivar la planta?")]
        [InlineData(false, "el peaje", null, "¿Activar el peaje?")]
        public void PreguntaCambioEstado(bool activo, string sustantivo, string nombre, string esperado) =>
            Assert.Equal(esperado, EstadoUiHelper.PreguntaCambioEstado(activo, sustantivo, nombre));

        [Fact]
        public void PreguntaCambioEstado_ConValoresDeLaFila()
        {
            Assert.Equal("¿Desactivar el tracto ABC-123?", EstadoUiHelper.PreguntaCambioEstado((object)true, "el tracto", (object)"ABC-123"));
            Assert.Equal("¿Activar el tracto?", EstadoUiHelper.PreguntaCambioEstado(System.DBNull.Value, "el tracto", System.DBNull.Value));
        }

        // ── E5-17: sello con tono ─────────────────────────────────────────────────────────────

        [Fact]
        public void Sello_CodificaElTextoYPoneLaClaseDelTono()
        {
            Assert.Equal("<span class=\"sgv-estado sgv-estado-ok\">Activo</span>", EstadoUiHelper.Sello("Activo", TonoEstado.Ok));
            Assert.Equal("<span class=\"sgv-estado\">CONDUCTOR</span>", EstadoUiHelper.Sello(" CONDUCTOR ", TonoEstado.Neutro));
            Assert.Equal("<span class=\"sgv-estado sgv-estado-peligro\">&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;</span>",
                EstadoUiHelper.Sello("<script>alert('x')</script>", TonoEstado.Peligro));
            Assert.Equal("<span class=\"sgv-estado\"></span>", EstadoUiHelper.Sello(null, TonoEstado.Neutro));
        }

        [Fact]
        public void ClaseTono_UsaLasClasesDelSello()
        {
            Assert.Equal("", EstadoUiHelper.ClaseTono(TonoEstado.Neutro));
            Assert.Equal(EstadoUiHelper.ClaseOk, EstadoUiHelper.ClaseTono(TonoEstado.Ok));
            Assert.Equal(EstadoUiHelper.ClaseAlerta, EstadoUiHelper.ClaseTono(TonoEstado.Alerta));
            Assert.Equal(EstadoUiHelper.ClasePeligro, EstadoUiHelper.ClaseTono(TonoEstado.Peligro));
            Assert.Equal(EstadoUiHelper.ClaseInfo, EstadoUiHelper.ClaseTono(TonoEstado.Info));
            Assert.Equal("", EstadoUiHelper.ClaseTono((TonoEstado)99));
        }

        [Theory]
        [InlineData("DELETE", TonoEstado.Peligro)]
        [InlineData("RECHAZAR", TonoEstado.Peligro)]
        [InlineData("LOGIN_FALLIDO", TonoEstado.Peligro)]
        [InlineData("LOGIN_BLOQUEADO", TonoEstado.Peligro)]
        [InlineData("FIRMA_CONTRASENA_INCORRECTA", TonoEstado.Peligro)]
        [InlineData("SEGUNDO_FACTOR_FALLIDO", TonoEstado.Peligro)]
        [InlineData("SEGUNDO_FACTOR_BLOQUEADO", TonoEstado.Peligro)]
        [InlineData("INTEGRIDAD_PDF", TonoEstado.Peligro)]
        [InlineData("RETIRAR", TonoEstado.Alerta)]
        [InlineData("RETIRAR_LIQUIDACION_PRUEBA", TonoEstado.Alerta)]
        [InlineData("REVERTIR", TonoEstado.Alerta)]
        [InlineData("OBSERVAR_LIQUIDACION", TonoEstado.Alerta)]
        [InlineData("SEGUNDO_FACTOR_QUITADO", TonoEstado.Alerta)]
        [InlineData("INSERT", TonoEstado.Ok)]
        [InlineData("APROBAR", TonoEstado.Ok)]
        [InlineData("FIRMAR", TonoEstado.Ok)]
        [InlineData("CONFORMIDAD", TonoEstado.Ok)]
        [InlineData("UPDATE", TonoEstado.Info)]
        [InlineData("login", TonoEstado.Info)]
        [InlineData(" SEGUNDO_FACTOR_OK ", TonoEstado.Info)]
        [InlineData("LOGOUT", TonoEstado.Neutro)]
        [InlineData("CONSULTAR_HORA_GPS", TonoEstado.Neutro)]
        [InlineData("DESCARGAR_PDF", TonoEstado.Neutro)]
        [InlineData("OTRA_COSA", TonoEstado.Neutro)]
        [InlineData("", TonoEstado.Neutro)]
        [InlineData(null, TonoEstado.Neutro)]
        public void TonoAccionAuditoria(string accion, TonoEstado esperado) =>
            Assert.Equal(esperado, EstadoUiHelper.TonoAccionAuditoria(accion));

        [Fact]
        public void TonoAccionAuditoria_DbNullEsGris() =>
            Assert.Equal(TonoEstado.Neutro, EstadoUiHelper.TonoAccionAuditoria(DBNull.Value));
    }
}
