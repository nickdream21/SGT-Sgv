using System.Configuration;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Mientras los conductores no liquidan por la web (siguen con la hoja de papel), sus viajes de liquidación
    /// (<c>ViajesEnProgreso</c>) se cierran solos, para que el día que empiecen cada uno arranque con su vuelta
    /// actual y no con semanas de despachos acumulados (decisión del usuario 2026-11-13).
    /// <para>
    /// Interruptor: appSetting <c>Liquidaciones.ConductoresEnSistema</c>. En <c>false</c> (o si falta) cada corrida del
    /// GPS automático cierra los viajes <c>ABIERTO</c>; en <c>true</c> no se cierra nada. Solo toca el viaje de
    /// liquidación: los despachos, su estado y el seguimiento GPS (otras tablas) quedan igual.
    /// Los viajes en revisión, por conformar o aprobados no están en <c>ABIERTO</c>, así que no se tocan.
    /// </para>
    /// </summary>
    public static class CierreViajesSinLiquidacion
    {
        public const string Motivo = "Liquidado en papel: cierre automático mientras los conductores no liquidan en el sistema.";

        public static bool ConductoresEnSistema =>
            bool.TryParse(ConfigurationManager.AppSettings["Liquidaciones.ConductoresEnSistema"], out bool s) && s;

        /// <summary>Cierra los viajes abiertos si los conductores aún no usan el sistema. Devuelve cuántos cerró.</summary>
        public static int CerrarSiCorresponde()
        {
            if (ConductoresEnSistema) return 0;
            int cerrados = CerrarViajesAbiertos();
            if (cerrados > 0)
                AuditoriaHelper.Registrar("UPDATE", "ViajesEnProgreso", null,
                    $"Cierre automático de {cerrados} viaje(s) abierto(s) sin liquidación web (Liquidaciones.ConductoresEnSistema = false)");
            return cerrados;
        }

        /// <summary>Cierra todos los viajes ABIERTO con el motivo de cierre sin liquidación. Público para los tests.</summary>
        public static int CerrarViajesAbiertos() =>
            DbHelper.EjecutarNonQuery(@"
                UPDATE ViajesEnProgreso
                SET estadoViaje   = 'CERRADO',
                    fechaCierre   = dbo.fn_AhoraPeru(),
                    usuarioCierre = 'SISTEMA',
                    motivoCierre  = @motivo
                WHERE estadoViaje = 'ABIERTO'",
                DbHelper.Param("@motivo", Motivo));
    }
}
