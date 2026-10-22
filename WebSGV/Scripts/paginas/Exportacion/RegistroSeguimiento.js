// Tablero de seguimiento: copiar el STATUS de la unidad (fila por fila o toda la tabla) para
// pegarlo en el Excel STATUS GENERAL o enviarlo.
(function () {
    'use strict';

    function copiar(texto, mensaje) {
        function listo() { SGV.avisar(mensaje); }
        function respaldo() {
            var area = document.createElement('textarea');
            area.value = texto;
            area.setAttribute('readonly', '');
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();
            var ok = false;
            try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
            document.body.removeChild(area);
            if (ok) listo(); else SGV.avisar('No se pudo copiar: selecciona el texto y usa Ctrl+C.');
        }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(texto).then(listo, respaldo);
        } else {
            respaldo();
        }
    }

    // La última consulta al GPS tiene más de 15 minutos: se consulta sola al abrir (el servidor decide;
    // tras el postback ya no se repite). Con ?auto=0 en la dirección no se lanza.
    if (SGV.autoActualizarGps) {
        var boton = document.getElementById(SGV.btnActualizarGps);
        if (boton) {
            SGV.avisar('La última consulta al GPS tiene más de 15 minutos: se actualiza sola ahora.');
            setTimeout(function () { boton.click(); }, 600);
        }
    }

    // La actualización automática del GPS corre en el servidor cada 10–15 min: con la página abierta se
    // pregunta cada 2 min si hay una consulta más nueva que la mostrada y se avisa (sin recargar sola:
    // "Ver datos nuevos" vuelve a buscar con los mismos filtros).
    var aviso = document.getElementById('avisoGpsNuevo');
    if (aviso && SGV.urlEstadoGps) {
        var revisar = function () {
            if (document.hidden) return;
            fetch(SGV.urlEstadoGps, { credentials: 'same-origin', cache: 'no-store' })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (d) {
                    if (!d || !d.ultima || (SGV.ultimaConsultaGps && d.ultima <= SGV.ultimaConsultaGps)) return;
                    document.getElementById('avisoGpsHora').textContent = d.ultima.substr(11, 5);
                    aviso.hidden = false;
                })
                .catch(function () { /* sin red: se reintenta en la próxima vuelta */ });
        };
        setInterval(revisar, 120000);
        document.addEventListener('visibilitychange', function () { if (!document.hidden) revisar(); });
        document.getElementById('btnVerDatosNuevos').addEventListener('click', function () {
            var filtrar = document.getElementById(SGV.btnFiltrar);
            if (filtrar) filtrar.click(); else location.reload();
        });
    }

    document.addEventListener('click', function (e) {
        var uno = e.target.closest('[data-copiar]');
        if (uno) {
            var celda = uno.closest('.sx-status');
            var texto = celda.querySelector('.sx-status-texto').textContent.trim();
            copiar(texto, 'STATUS copiado: ' + texto);
            return;
        }
        if (e.target.closest('#btnCopiarStatus')) {
            var filas = Array.prototype.map.call(document.querySelectorAll('.sx-status-texto'), function (s) { return s.textContent.trim(); });
            if (!filas.length) { SGV.avisar('No hay filas para copiar.'); return; }
            // Una fila por línea, en el orden de la tabla: se pega directo en la columna STATUS del Excel.
            copiar(filas.join('\r\n'), 'STATUS de ' + filas.length + ' unidad(es) copiado, en el orden de la tabla.');
            return;
        }
        if (e.target.closest('#btnReporteLlegadas')) {
            // El servidor arma el reporte con todas las unidades en camino a planta (no solo la pestaña elegida).
            var reporte = document.getElementById('txtReporteLlegadas').value.trim();
            if (!reporte) { SGV.avisar('No hay unidades en camino a cargar a Trujillo ni a descargar a Jave/Inbalnor con posición del GPS.'); return; }
            copiar(reporte, 'Reporte de llegadas copiado: pégalo en el mensaje.');
            return;
        }
        if (e.target.closest('#btnReporteGrifo')) {
            var grifo = document.getElementById('txtReporteGrifo').value.trim();
            if (!grifo) { SGV.avisar('No hay carros de vuelta de Ecuador camino al grifo Santa Rosa (ni descargando).'); return; }
            copiar(grifo, 'Reporte del grifo Santa Rosa copiado: pégalo en el mensaje al grifo.');
        }
    });
})();
