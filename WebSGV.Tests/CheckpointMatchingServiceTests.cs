using System;
using System.Collections.Generic;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    public class CheckpointMatchingServiceTests
    {
        private static OnwayHistoryPoint Punto(DateTime hora, double lat, double lng, string alertEn = null, double speed = 0) =>
            new OnwayHistoryPoint
            {
                MessageTime = hora,
                Lat = lat,
                Lng = lng,
                Speed = speed,
                AlertDescription = alertEn == null ? null : new OnwayTextoMultiIdioma { En = alertEn }
            };

        // ---- CalcularDistanciaMetros ----

        [Fact]
        public void CalcularDistanciaMetros_PuntosIguales_RetornaCero()
        {
            double d = CheckpointMatchingService.CalcularDistanciaMetros(-4.956195, -80.699310, -4.956195, -80.699310);
            Assert.Equal(0, d, 3);
        }

        [Fact]
        public void CalcularDistanciaMetros_UnGradoDeLatitud_RetornaAproximadamente111Km()
        {
            // 1 grado de latitud equivale a ~111.32 km en cualquier punto de la Tierra.
            double d = CheckpointMatchingService.CalcularDistanciaMetros(0, 0, 1, 0);
            Assert.InRange(d, 110500, 111500);
        }

        // ---- DetectarLlegada ----

        [Fact]
        public void DetectarLlegada_HistorialNull_RetornaNull()
        {
            var resultado = CheckpointMatchingService.DetectarLlegada(null, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_HistorialVacio_RetornaNull()
        {
            var resultado = CheckpointMatchingService.DetectarLlegada(new List<OnwayHistoryPoint>(), 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_NingunPuntoDentroDelRadio_RetornaNull()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 10, 0, 0), 10, 10) // muy lejos del checkpoint (0,0)
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_SinEventoApagado_RetornaPrimerPuntoDentroDelRadioPorHora()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 11, 0, 0), 10, 10),      // fuera de radio
                Punto(new DateTime(2026, 8, 25, 12, 30, 0), 0, 0),       // dentro del radio - más tarde
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0.0005, 0.0005) // dentro del radio - más temprano
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 0, 0), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_ConEventoIgnitionOffDentroDelRadio_PriorizaEseEventoSobreUnPuntoCrudoAnterior()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                // Punto crudo dentro del radio, más temprano que el evento de apagado.
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0, 0),
                // Evento "Ignition Off" dentro del radio, más tarde.
                Punto(new DateTime(2026, 8, 25, 12, 30, 33), 0, 0, "Ignition Off, Vehicle AVM-877")
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 30, 33), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_EventoIgnitionOffFueraDelRadio_NoLoPrioriza()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0, 0),                         // dentro del radio
                Punto(new DateTime(2026, 8, 25, 12, 30, 0), 10, 10, "Ignition Off, X")     // apagado, pero lejos
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 0, 0), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_HistorialDesordenado_LoOrdenaPorHoraAntesDeMatchear()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 13, 0, 0), 0, 0),
                Punto(new DateTime(2026, 8, 25, 11, 0, 0), 0, 0)
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 11, 0, 0), resultado.MessageTime);
        }

        // ---- DetectarLlegada (tier 2: parada sostenida por velocidad) ----
        //
        // Caso real: camión CBV-829 (Joel Ramírez), despacho 26/08/2026 Trujillo-Vittapro,
        // documentado punto por punto por la administradora de transporte a partir del reporte
        // "Historial" de Onway/Entel. Las coordenadas/horas/velocidades marcadas como "real"
        // vienen literalmente de ese reporte; los puntos marcados "sintético" son relleno
        // necesario para darle al algoritmo el contexto que un solo pantallazo no capta.

        [Fact]
        public void DetectarLlegada_TrujilloIngreso_CBV829_ParadaSostenidaCercaDeSeq239()
        {
            // Checkpoint TRUJILLO_INGRESO (PuntoControlGps): -8.135358, -79.013090, radio 300m.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 5, 44, 52), -8.135676, -79.013430, speed: 0),    // real: seq238
                Punto(new DateTime(2026, 8, 27, 5, 45, 34), -8.135545, -79.013330, speed: 6.6),  // real: seq239 (aún entrando)
                Punto(new DateTime(2026, 8, 27, 5, 45, 43), -8.135527, -79.013214, speed: 5.9),  // real: seq240 (aún entrando)
                Punto(new DateTime(2026, 8, 27, 5, 45, 53), -8.135510, -79.013160, speed: 0),    // real: seq241 ("Vehículo apagado" en Onway, texto en español que el tier 1 no reconoce)
                Punto(new DateTime(2026, 8, 27, 5, 46, 23), -8.135510, -79.013160, speed: 0),    // sintético: confirma que la parada de seq241 fue sostenida
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, -8.135358, -79.013090, 300);

            Assert.NotNull(resultado);
            // El tier 1 (texto "ignition off") no aplica porque la alerta real vino en español
            // ("Vehículo apagado"), así que esto ejercita puntualmente el tier 2 nuevo.
            Assert.Equal(new DateTime(2026, 8, 27, 5, 45, 53), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_BaseLlegada2_CBV829_ParadaSostenidaTrasTramoDeAltaVelocidad()
        {
            // Checkpoint BASE_LLEGADA_2 (PuntoControlGps): -4.956411, -80.697300, radio 500m.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 28, 1, 33, 13), -4.958425, -80.697365, speed: 46.8), // real: seq187, aún en la Panamericana
                Punto(new DateTime(2026, 8, 28, 1, 33, 38), -4.956505, -80.697290, speed: 11.4), // real: seq188, desacelerando
                Punto(new DateTime(2026, 8, 28, 1, 33, 43), -4.956395, -80.697340, speed: 7.0),  // real: seq189, desacelerando
                Punto(new DateTime(2026, 8, 28, 1, 34, 43), -4.956380, -80.697360, speed: 0),    // real: seq190, se detiene
                Punto(new DateTime(2026, 8, 28, 1, 34, 53), -4.956380, -80.697360, speed: 0),    // sintético: confirma que la parada de seq190 fue sostenida
                Punto(new DateTime(2026, 8, 28, 1, 35, 3),  -4.956380, -80.697360, speed: 0),    // sintético: ídem
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, -4.956411, -80.697300, 500);

            Assert.NotNull(resultado);
            // 59s antes del punto que la administradora eligió a simple vista (seq191, 01:35:42)
            // — razonablemente cerca, consistente con que ella misma aclaró que no hace falta
            // que el punto sea exacto.
            Assert.Equal(new DateTime(2026, 8, 28, 1, 34, 43), resultado.MessageTime);
        }

        // ---- DetectarSalida ----

        [Fact]
        public void DetectarSalida_HistorialNull_RetornaNull()
        {
            var resultado = CheckpointMatchingService.DetectarSalida(null, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarSalida_SinPuntosEnRadio_RetornaNull()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 10, 0, 0), 10, 10, speed: 20) // muy lejos del checkpoint (0,0)
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarSalida_ArranqueQueVuelveAPararSostenido_NoLoConfundeConSalidaReal()
        {
            // Sintético, aislando el patrón: arranca, pero vuelve a quedarse parado por varios
            // minutos y no hay ninguna salida real después — no debe reportarse ninguna salida.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 26, 10, 0, 0), 0, 0, speed: 0),
                Punto(new DateTime(2026, 8, 26, 10, 0, 30), 0.001, 0, speed: 8),   // arranque
                Punto(new DateTime(2026, 8, 26, 10, 1, 0), 0.0015, 0, speed: 0),   // vuelve a pararse...
                Punto(new DateTime(2026, 8, 26, 10, 5, 0), 0.0015, 0, speed: 0),   // ...por 4 minutos: regreso sostenido
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarSalida_ParadaBreveTipoPortonNoInterrumpeLaDeteccion()
        {
            // Sintético: tras el punto de salida real, hay una parada de 30s (tipo portón) que
            // no debe hacer que el algoritmo la confunda con un "regreso sostenido".
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 26, 10, 0, 0), 0, 0, speed: 0),
                Punto(new DateTime(2026, 8, 26, 10, 0, 30), 0.001, 0, speed: 6),   // salida real
                Punto(new DateTime(2026, 8, 26, 10, 0, 45), 0.0012, 0, speed: 0),  // parada breve (portón)...
                Punto(new DateTime(2026, 8, 26, 10, 1, 15), 0.0012, 0, speed: 0),  // ...30s, no sostenida
                Punto(new DateTime(2026, 8, 26, 10, 1, 45), 0.002, 0, speed: 10),  // reanuda el alejamiento
                Punto(new DateTime(2026, 8, 26, 10, 3, 0), 0.01, 0, speed: 40),    // ya fuera del radio, sin retorno
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 26, 10, 0, 30), resultado.MessageTime);
        }

        [Fact]
        public void DetectarSalida_BaseSalida1_CBV829_IgnoraArranquesFalsosYDetectaSeq40()
        {
            // Checkpoint BASE_SALIDA_1 (PuntoControlGps): -4.956480, -80.697510, radio 500m.
            // Todos los puntos 31-41 son reales (reporte Onway del 26/08/2026); 42-43 son
            // relleno sintético para confirmar que el alejamiento tras seq40 es sostenido.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 26, 16, 28, 9),  -4.956340, -80.698326, speed: 0),    // real: seq31, parado
                Punto(new DateTime(2026, 8, 26, 16, 29, 9),  -4.956340, -80.698326, speed: 0),    // real: seq32, parado
                Punto(new DateTime(2026, 8, 26, 16, 29, 19), -4.956340, -80.698320, speed: 6.6),  // real: seq33, arranque falso
                Punto(new DateTime(2026, 8, 26, 16, 29, 22), -4.956400, -80.698290, speed: 10.7), // real: seq34, arranque falso
                Punto(new DateTime(2026, 8, 26, 16, 29, 27), -4.956420, -80.698150, speed: 12),   // real: seq35, arranque falso
                Punto(new DateTime(2026, 8, 26, 16, 30, 27), -4.956388, -80.698010, speed: 0),    // real: seq36, vuelve a pararse...
                Punto(new DateTime(2026, 8, 26, 16, 31, 27), -4.956346, -80.697670, speed: 0),    // real: seq37, ...
                Punto(new DateTime(2026, 8, 26, 16, 32, 27), -4.956336, -80.697525, speed: 0),    // real: seq38, ...
                Punto(new DateTime(2026, 8, 26, 16, 33, 27), -4.956336, -80.697525, speed: 0),    // real: seq39, ...3 minutos parado: regreso sostenido, descarta 33-35
                Punto(new DateTime(2026, 8, 26, 16, 33, 46), -4.956425, -80.697420, speed: 6.2),  // real: seq40, salida real (identificada por la administradora)
                Punto(new DateTime(2026, 8, 26, 16, 33, 51), -4.956508, -80.697440, speed: 6.6),  // real: seq41, sigue alejándose
                Punto(new DateTime(2026, 8, 26, 16, 34, 21), -4.957500, -80.696800, speed: 15),   // sintético: continúa el alejamiento
                Punto(new DateTime(2026, 8, 26, 16, 35, 21), -4.960500, -80.695500, speed: 35),   // sintético: ya en la Panamericana, sin retorno
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -4.956480, -80.697510, 500);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 26, 16, 33, 46), resultado.MessageTime);
        }

        [Fact]
        public void DetectarSalida_TrujilloSalida_CBV829_DetectaSeq292TrasCarga()
        {
            // Checkpoint TRUJILLO_SALIDA (PuntoControlGps): -8.134321, -79.013880, radio 500m.
            // Todos los puntos son reales (reporte Onway del 27/08/2026, tras horas de carga en
            // planta). El ciclo "Vehículo apagado"/"Vehículo encendido" en seq288-291 es un
            // arranque falso (el camión se reacomoda y se vuelve a apagar); la salida real y
            // sostenida empieza en seq292.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 8, 31, 15), -8.135510, -79.013145, speed: 5.5),                          // seq286
                Punto(new DateTime(2026, 8, 27, 8, 31, 20), -8.135510, -79.013220, speed: 5.5),                          // seq287
                Punto(new DateTime(2026, 8, 27, 8, 31, 36), -8.135535, -79.013330, speed: 0, alertEn: "Vehicle powered off"), // seq288 (alerta real en español "Vehículo apagado"; se usa un texto neutro aquí porque el tier 1 solo reconoce "ignition off" en inglés y este caso debe resolverse por el tier 2/velocidad, no por el tier 1)
                Punto(new DateTime(2026, 8, 27, 8, 31, 36), -8.135535, -79.013330, speed: 0),                            // seq289 (mismo instante que seq288 en el reporte real)
                Punto(new DateTime(2026, 8, 27, 8, 33, 30), -8.135535, -79.013330, speed: 0),                            // seq290
                Punto(new DateTime(2026, 8, 27, 8, 33, 43), -8.135535, -79.013330, speed: 0),                            // seq291, 2:07 parado desde seq288: regreso sostenido, descarta 286-287
                Punto(new DateTime(2026, 8, 27, 8, 33, 52), -8.135623, -79.013350, speed: 12),                           // seq292, salida real
                Punto(new DateTime(2026, 8, 27, 8, 33, 57), -8.135726, -79.013320, speed: 7.2),                          // seq293
                Punto(new DateTime(2026, 8, 27, 8, 34, 25), -8.135985, -79.013280, speed: 9.2),                          // seq294 (punto que la administradora eligió a simple vista)
                Punto(new DateTime(2026, 8, 27, 8, 35, 1),  -8.137005, -79.012930, speed: 10.7),                         // seq295
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -8.134321, -79.013880, 500);

            Assert.NotNull(resultado);
            // 33s antes del punto que la administradora eligió a simple vista (seq294) — dentro
            // de la tolerancia que ella misma describió ("no tiene que ser exacto").
            Assert.Equal(new DateTime(2026, 8, 27, 8, 33, 52), resultado.MessageTime);
        }

        // ---- DetectarSalida: ventana de confirmación acotada (caso real "F.H. Salida Planta") ----
        //
        // Caso real reportado por la administradora sobre el mismo viaje CBV-829/27-08-2026: el
        // algoritmo devolvía 27/08 15:41 para "F.H. Salida Planta" en vez de la salida real a las
        // ~09:17 (seq327, -8.135675/-79.01342, 7.2 km/h — visualmente identificada por ella tras
        // una parada de 1 minuto en seq324-325). La causa: el camión volvió a pasar cerca del
        // mismo checkpoint (Av. Gonzales Prada, radio 500m) horas más tarde por otro motivo, y el
        // chequeo de "regreso sostenido" no tenía límite de tiempo, así que ese paso posterior sin
        // relación invalidaba la salida real de las 09:17.

        [Fact]
        public void DetectarSalida_RegresoMuchoDespuesFueraDeLaVentana_NoInvalidaLaSalida()
        {
            // Checkpoint TRUJILLO_SALIDA (PuntoControlGps): -8.134321, -79.013880, radio 500m.
            var historial = new List<OnwayHistoryPoint>
            {
                // Parada breve de 1 minuto (real: seq324-325) justo antes de la salida — no debe
                // interpretarse como que "ya salió antes" porque el candidato real viene después.
                Punto(new DateTime(2026, 8, 27, 9, 15, 24), -8.135541, -79.013330, speed: 0),
                Punto(new DateTime(2026, 8, 27, 9, 16, 24), -8.135541, -79.013330, speed: 0),
                Punto(new DateTime(2026, 8, 27, 9, 17, 13), -8.135675, -79.013420, speed: 7.2), // real: seq327, salida real identificada por la administradora
                Punto(new DateTime(2026, 8, 27, 9, 19, 0),  -8.140000, -79.020000, speed: 30),  // sintético: alejándose, ya fuera del radio
                // Sintético: mucho más tarde ese mismo día, el camión vuelve a pasar cerca del
                // checkpoint por otro motivo (no relacionado con la carga) y se detiene un rato.
                Punto(new DateTime(2026, 8, 27, 15, 40, 0), -8.135600, -79.013300, speed: 0),
                Punto(new DateTime(2026, 8, 27, 15, 45, 0), -8.135600, -79.013300, speed: 0),
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -8.134321, -79.013880, 500);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 27, 9, 17, 13), resultado.MessageTime);
        }

        [Fact]
        public void DetectarSalida_RegresoDentroDeLaVentana_SiInvalidaLaSalida()
        {
            // Contraparte del test anterior: si el "regreso sostenido" ocurre DENTRO de la
            // ventana de confirmación (15 min), sigue descartando el candidato como antes.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 9, 17, 13), -8.135675, -79.013420, speed: 7.2), // candidato
                Punto(new DateTime(2026, 8, 27, 9, 25, 0),  -8.135600, -79.013300, speed: 0),   // regreso a los 8 min...
                Punto(new DateTime(2026, 8, 27, 9, 30, 0),  -8.135600, -79.013300, speed: 0),   // ...sostenido 5 min: regreso real
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -8.134321, -79.013880, 500);

            Assert.Null(resultado);
        }

        // ---- DetectarLlegada: rechazo de un "Ignition Off" que no se sostiene (caso real "F.H. Ingreso Planta") ----

        [Fact]
        public void DetectarLlegada_IgnitionOffQueSaleDelRadioEnfoques_PrefiereElSiguienteEventoSostenido()
        {
            // Un primer "Ignition Off" dentro del radio, pero el vehículo sale del radio poco
            // después (blip: apagó/prendió mientras aún maniobraba) — no debe tomarse como la
            // llegada real. El segundo "Ignition Off", que sí se sostiene, es el correcto.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 5, 35, 0), 0, 0, alertEn: "Ignition Off, blip"),
                Punto(new DateTime(2026, 8, 27, 5, 36, 0), 0.005, 0), // sale del radio (~550m) poco después: confirma que fue un blip
                Punto(new DateTime(2026, 8, 27, 5, 45, 0), 0, 0),     // vuelve a entrar al radio
                Punto(new DateTime(2026, 8, 27, 5, 49, 0), 0, 0, alertEn: "Ignition Off, real"), // esta sí se sostiene
                Punto(new DateTime(2026, 8, 27, 6, 5, 0),  0, 0),     // sigue dentro del radio 16 min después
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 27, 5, 49, 0), resultado.MessageTime);
        }

        [Fact]
        public void DetectarSalida_TrujilloSalida_ASU862_IgnoraCicloDeApagadoYDetectaSeq266()
        {
            // Checkpoint TRUJILLO_SALIDA (PuntoControlGps): -8.134321, -79.013880, radio 500m.
            // Caso real (tracto ASU-862, 27/08/2026): tras el ciclo "Vehículo apagado"
            // (seq261-263, ~10:20:59-10:22:13) el camión se queda quieto (speed 0) hasta
            // seq265 (10:23:21) — un total de 2:22 parado, por encima de la tolerancia de
            // parada breve (2 min) — así que la aproximación previa (seq256-260, en movimiento
            // mientras se acerca a estacionar) se descarta correctamente por el regreso
            // sostenido. La salida real y sostenida es seq266 (10:24:21, 12.6 km/h).
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 10, 20, 29), -8.135272, -79.013145, speed: 8.7),                          // seq256
                Punto(new DateTime(2026, 8, 27, 10, 20, 35), -8.135320, -79.013100, speed: 9.1),                          // seq257
                Punto(new DateTime(2026, 8, 27, 10, 20, 35), -8.135389, -79.013070, speed: 9.6),                          // seq258 (mismo instante que seq257 en el reporte real)
                Punto(new DateTime(2026, 8, 27, 10, 20, 39), -8.135456, -79.013100, speed: 9.2),                          // seq259
                Punto(new DateTime(2026, 8, 27, 10, 20, 47), -8.135529, -79.013275, speed: 8.3),                          // seq260
                Punto(new DateTime(2026, 8, 27, 10, 20, 59), -8.135543, -79.013340, speed: 0,   alertEn: "Vehicle powered off"), // seq261 ("Vehículo apagado" real, en español)
                Punto(new DateTime(2026, 8, 27, 10, 21, 2),  -8.135543, -79.013340, speed: 0),                            // seq262
                Punto(new DateTime(2026, 8, 27, 10, 22, 13), -8.135543, -79.013340, speed: 0,   alertEn: "Vehicle powered on"),  // seq263 ("Vehículo encendido" real, en español)
                Punto(new DateTime(2026, 8, 27, 10, 22, 19), -8.135524, -79.013380, speed: 0.2),                          // seq264
                Punto(new DateTime(2026, 8, 27, 10, 23, 21), -8.135539, -79.013380, speed: 0),                            // seq265, 2:22 parado desde seq261: regreso sostenido, descarta 256-260
                Punto(new DateTime(2026, 8, 27, 10, 24, 21), -8.136571, -79.013110, speed: 12.6),                         // seq266, salida real
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -8.134321, -79.013880, 500);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 27, 10, 24, 21), resultado.MessageTime);
        }

        [Fact]
        public void DetectarSalida_TrujilloSalida_SegundoVehiculo_IgnoraCicloDeApagadoYDetectaSeq232()
        {
            // Checkpoint TRUJILLO_SALIDA (PuntoControlGps): -8.134321, -79.013880, radio 500m.
            // Mismo patrón que el caso ASU-862, con otro camión de la misma flota el mismo día
            // (27/08/2026): aproximación (seq226-227), ciclo "Vehículo apagado" (09:56:06) →
            // "Vehículo encendido" (09:58:26) con 2:25 min parado en total — por encima de la
            // tolerancia de parada breve (2 min) — así que la aproximación previa se descarta
            // por regreso sostenido. La salida real es seq232 (09:59:32, 7.9 km/h), la fila que
            // la administradora resaltó como la correcta.
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 27, 9, 55, 33), -8.135452, -79.013070, speed: 4.6),                          // seq226
                Punto(new DateTime(2026, 8, 27, 9, 55, 39), -8.135505, -79.013120, speed: 4.7),                          // seq227
                Punto(new DateTime(2026, 8, 27, 9, 56, 6),  -8.135566, -79.013306, speed: 0, alertEn: "Vehicle powered off"), // seq228 ("Vehículo apagado" real, en español)
                Punto(new DateTime(2026, 8, 27, 9, 56, 8),  -8.135566, -79.013306, speed: 0),                            // seq229
                Punto(new DateTime(2026, 8, 27, 9, 58, 26), -8.135566, -79.013306, speed: 0, alertEn: "Vehicle powered on"),  // seq230 ("Vehículo encendido" real, en español)
                Punto(new DateTime(2026, 8, 27, 9, 58, 31), -8.135572, -79.013320, speed: 0.1),                          // seq231, 2:25 parado desde seq228: regreso sostenido, descarta 226-227
                Punto(new DateTime(2026, 8, 27, 9, 59, 32), -8.135933, -79.013330, speed: 7.9),                          // seq232, salida real
            };

            var resultado = CheckpointMatchingService.DetectarSalida(historial, -8.134321, -79.013880, 500);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 27, 9, 59, 32), resultado.MessageTime);
        }
    }
}
