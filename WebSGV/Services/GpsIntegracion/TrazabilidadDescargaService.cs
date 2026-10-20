using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;
using static WebSGV.Services.GpsIntegracion.TrazabilidadCargaService;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Tramo de descarga de un tracto: sale de base, pasa por la bodega nacional (Complex), el CEBAF,
    /// cruza a Ecuador, pasa por TCI y descarga en Jave o Inbalnor. Cada hito es null si el GPS no lo muestra.
    /// </summary>
    public class ResultadoTramoDescarga
    {
        public HitoGps SalidaBase { get; set; }
        public HitoGps LlegadaComplex { get; set; }
        public HitoGps IngresoComplex { get; set; }
        public HitoGps SalidaComplex { get; set; }
        public HitoGps LlegadaCebaf { get; set; }
        public HitoGps Cruce { get; set; }
        public HitoGps LlegadaTci { get; set; }
        public HitoGps SalidaTci { get; set; }

        /// <summary>No entró a TCI: llegada y salida son las del control aduanero cercano.</summary>
        public bool PasoDirectoTci { get; set; }

        /// <summary>"JAVE" o "INBALNOR"; null si no se detectó la descarga.</summary>
        public string PlantaDescarga { get; set; }

        /// <summary>Cerca o afuera de la planta: va en F.H.LL.Planta y en F.H.LL.Almacén.</summary>
        public HitoGps LlegadaPlanta { get; set; }
        public HitoGps IngresoPlanta { get; set; }
        public HitoGps SalidaPlanta { get; set; }
        public HitoGps LlegadaBase { get; set; }
    }

    /// <summary>
    /// Detección del tramo de descarga con las mismas reglas que el de carga
    /// (<see cref="TrazabilidadCargaService"/>): visitas a geocercas en polígono, cada parada buscada
    /// después de la anterior, y la salida de base como la última salida antes de la primera parada.
    /// Si una parada no aparece (sin GPS ese rato), las siguientes se buscan desde la última encontrada.
    /// </summary>
    public static class TrazabilidadDescargaService
    {
        /// <summary>Estadía mínima en el acceso de una bodega (Complex, Jave, Inbalnor): menos es pasar de largo.</summary>
        private const int MinutosMinimosEnBodega = 10;

        /// <summary>Estadía mínima en TCI (el trámite dura pocos minutos).</summary>
        private const int MinutosMinimosEnTci = 3;

        public static ResultadoTramoDescarga DetectarTramoDescarga(IEnumerable<OnwayHistoryPoint> historial, DateTime desdeUtc)
        {
            var puntos = Ordenar(historial);
            var r = new ResultadoTramoDescarga();
            int cursor = puntos.FindIndex(p => Hora(p) >= desdeUtc);
            if (cursor < 0) return r;
            int primerHito = -1;
            void Marcar(int i) { if (primerHito < 0) primerHito = i; }

            // 1. Bodega nacional (Complex): llegada a la cola, ingreso y salida del patio.
            var complex = PrimeraVisita(puntos, GeocercasTrazabilidad.ComplexAcceso, cursor, MinutosMinimosEnBodega);
            if (complex != null)
            {
                r.LlegadaComplex = Hito(puntos[complex.Primero], "Primer punto en la cola del Complex (Av. Juan Velasco Alvarado)");
                Marcar(complex.Primero);
                var patio = Visitas(puntos, GeocercasTrazabilidad.ComplexPatio)
                    .FirstOrDefault(v => v.Primero >= complex.Primero && v.Primero <= complex.Ultimo);
                if (patio != null)
                {
                    r.IngresoComplex = Hito(puntos[patio.Primero], "Primer punto dentro del patio del Complex");
                    if (patio.Cerrada) r.SalidaComplex = Hito(puntos[patio.Ultimo + 1], "Primer punto fuera del patio del Complex");
                }
                cursor = complex.Cerrada ? complex.Ultimo + 1 : complex.Ultimo;
            }

            // 2. CEBAF y 3. cruce: primer punto dentro, en ese orden.
            int idx = PrimerPuntoDentro(puntos, GeocercasTrazabilidad.Cebaf, cursor);
            if (idx >= 0)
            {
                r.LlegadaCebaf = Hito(puntos[idx], "Primer punto dentro del CEBAF");
                Marcar(idx);
                cursor = idx;
            }
            idx = PrimerPuntoDentro(puntos, GeocercasTrazabilidad.Cruce, cursor);
            if (idx >= 0)
            {
                r.Cruce = Hito(puntos[idx], "Primer punto en la E50 después de la aduana de Ecuador");
                Marcar(idx);
                cursor = idx;
            }

            // 4. TCI. Si se coordinó para que pase directo, no entra y se toma el control aduanero
            // cercano; si entró, ese control viene después y no cuenta.
            var tci = PrimeraVisita(puntos, GeocercasTrazabilidad.Tci, cursor, MinutosMinimosEnTci);
            var control = PrimeraVisita(puntos, GeocercasTrazabilidad.TciControlAduana, cursor, 1);
            r.PasoDirectoTci = tci == null || (control != null && control.Primero < tci.Primero);
            var visitaTci = r.PasoDirectoTci ? control : tci;
            if (visitaTci != null)
            {
                string lugar = r.PasoDirectoTci ? "el control aduanero junto a TCI (paso directo)" : "TCI";
                r.LlegadaTci = Hito(puntos[visitaTci.Primero], "Primer punto en " + lugar);
                if (visitaTci.Cerrada)
                    r.SalidaTci = Hito(puntos[visitaTci.Ultimo + 1], "Primer punto fuera " + (r.PasoDirectoTci ? "del control aduanero junto a TCI (paso directo)" : "de TCI"));
                Marcar(visitaTci.Primero);
                cursor = visitaTci.Cerrada ? visitaTci.Ultimo + 1 : visitaTci.Ultimo;
            }
            else r.PasoDirectoTci = false;

            // 5. Planta de descarga: la primera de Jave o Inbalnor donde la unidad se queda y entra.
            var jave = BuscarDescarga(puntos, GeocercasTrazabilidad.JaveAcceso, GeocercasTrazabilidad.JavePlanta, cursor);
            var inbalnor = BuscarDescarga(puntos, GeocercasTrazabilidad.InbalnorAcceso, GeocercasTrazabilidad.InbalnorPlanta, cursor);
            var descarga = jave != null && (inbalnor == null || jave.Item1.Primero <= inbalnor.Item1.Primero) ? jave : inbalnor;
            if (descarga != null)
            {
                string nombre = descarga == jave ? "Jave" : "Inbalnor";
                r.PlantaDescarga = nombre.ToUpperInvariant();
                r.LlegadaPlanta = Hito(puntos[descarga.Item1.Primero], "Primer punto en el acceso a " + nombre);
                r.IngresoPlanta = Hito(puntos[descarga.Item2.Primero], "Primer punto dentro de la bodega " + nombre);
                Marcar(descarga.Item1.Primero);
                if (descarga.Item2.Cerrada)
                {
                    r.SalidaPlanta = Hito(puntos[descarga.Item2.Ultimo + 1], "Primer punto fuera de la bodega " + nombre);
                    cursor = descarga.Item2.Ultimo + 1;
                }
                else cursor = -1; // sigue descargando: todavía no hay regreso
            }

            // 6. Salida de base antes de la primera parada y regreso a la base al final.
            if (primerHito >= 0)
                r.SalidaBase = BuscarSalidaBaseAntesDe(puntos, primerHito);
            if (r.SalidaPlanta != null && cursor >= 0)
            {
                BuscarRegresoBase(puntos, cursor, out var llegada, out _, out _);
                r.LlegadaBase = llegada;
            }
            return r;
        }

        private static Visita PrimeraVisita(List<OnwayHistoryPoint> puntos, GeocercaGps zona, int desde, int minutosMinimos) =>
            Visitas(puntos, zona).FirstOrDefault(v => v.Primero >= desde &&
                (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= minutosMinimos);

        private static int PrimerPuntoDentro(List<OnwayHistoryPoint> puntos, GeocercaGps zona, int desde)
        {
            for (int i = desde; i < puntos.Count; i++)
                if (zona.Contiene(puntos[i].Lat, puntos[i].Lng)) return i;
            return -1;
        }

        /// <summary>Visita al acceso de la planta (10+ min) con una estadía dentro de la bodega en la que se movió.</summary>
        private static Tuple<Visita, Visita> BuscarDescarga(List<OnwayHistoryPoint> puntos, GeocercaGps acceso, GeocercaGps planta, int desde)
        {
            var visitasPlanta = Visitas(puntos, planta);
            foreach (var va in Visitas(puntos, acceso).Where(v => v.Primero >= desde &&
                         (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= MinutosMinimosEnBodega))
            {
                var vp = visitasPlanta.FirstOrDefault(v => v.Primero >= va.Primero && v.Primero <= va.Ultimo && SeMovioAdentro(puntos, v));
                if (vp != null) return Tuple.Create(va, vp);
            }
            return null;
        }
    }
}
