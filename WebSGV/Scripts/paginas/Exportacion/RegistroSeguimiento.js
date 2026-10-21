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
        }
    });
})();
