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
            // Al cargar este script SiteMaster.js todavía no definió SGV.avisar: se espera a que la página termine.
            $(function () {
                SGV.avisar('La última consulta al GPS tiene más de 15 minutos: se actualiza sola ahora.');
                setTimeout(function () { boton.click(); }, 600);
            });
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

    // Las unidades de un mismo pedido van juntas bajo una cabecera (pedido, programación y cuántas son),
    // y lo que se escribe en "Buscar" filtra la tabla al instante, sin esperar al servidor.
    (function () {
        var filas = Array.prototype.slice.call(document.querySelectorAll('tr.sx-fila'));
        if (!filas.length) return;
        var columnas = filas[0].children.length, grupos = [], actual = null;
        filas.forEach(function (tr) {
            var clave = tr.dataset.pedido + '|' + tr.dataset.prog;
            if (!actual || actual.clave !== clave) {
                var cab = document.createElement('tr');
                cab.className = 'sx-grupo';
                var td = document.createElement('td');
                td.colSpan = columnas;
                cab.appendChild(td);
                tr.parentNode.insertBefore(cab, tr);
                actual = { clave: clave, cab: cab, td: td, filas: [], pedido: tr.dataset.pedido, prog: tr.dataset.prog };
                grupos.push(actual);
            }
            actual.filas.push(tr);
            tr.classList.add('sx-fila-agrupada');
        });

        function plano(t) { return t.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, ''); }
        filas.forEach(function (tr) { tr._texto = plano(tr.textContent); });

        var caja = document.getElementById(SGV.txtBuscar), cuenta = document.getElementById('sxFiltroCuenta');
        function aplicar() {
            var palabras = caja ? plano(caja.value).split(/\s+/).filter(Boolean) : [], visibles = 0;
            grupos.forEach(function (g) {
                var n = 0;
                g.filas.forEach(function (tr) {
                    var ok = palabras.every(function (p) { return tr._texto.indexOf(p) >= 0; });
                    tr.hidden = !ok;
                    if (ok) n++;
                });
                g.cab.hidden = n === 0;
                g.td.innerHTML = '';
                var b = document.createElement('strong');
                b.className = 'sx-mono'; b.textContent = g.pedido || 'Sin pedido';
                g.td.appendChild(b);
                g.td.appendChild(document.createTextNode('  ·  Prog. ' + g.prog + '  ·  ' +
                    (n === g.filas.length ? n : n + ' de ' + g.filas.length) + (g.filas.length === 1 ? ' unidad' : ' unidades')));
                visibles += n;
            });
            if (cuenta) cuenta.textContent = palabras.length ? visibles + ' de ' + filas.length + ' viajes en esta pestaña' : '';
        }
        aplicar();
        if (caja) caja.addEventListener('input', aplicar);
    })();

    document.addEventListener('click', function (e) {
        var uno = e.target.closest('[data-copiar]');
        if (uno) {
            var celda = uno.closest('.sx-status');
            var texto = celda.querySelector('.sx-status-texto').textContent.trim();
            copiar(texto, 'STATUS copiado: ' + texto);
            return;
        }
        if (e.target.closest('#btnCopiarStatus')) {
            // Solo las filas a la vista (el filtro al escribir oculta las demás).
            var filas = Array.prototype.map.call(document.querySelectorAll('tr.sx-fila:not([hidden]) .sx-status-texto'), function (s) { return s.textContent.trim(); });
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
