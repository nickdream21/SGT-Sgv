// Compara la huella SHA-256 del PDF elegido (calculada en el navegador, sin enviarlo) con la
// registrada para el código de verificación.
(function () {
    'use strict';
    var input = document.getElementById('archivoPdf');
    var salida = document.getElementById('resultadoArchivo');
    var hidden = document.getElementById('hfHash');
    if (!input || !salida || !hidden) return;

    function mostrar(clase, texto) {
        salida.className = 'vd-estado mt-3 ' + clase;
        salida.textContent = texto;
        salida.style.display = '';
    }

    input.addEventListener('change', function () {
        var archivo = input.files && input.files[0];
        if (!archivo) return;
        if (!window.crypto || !window.crypto.subtle) {
            mostrar('vd-estado-alerta', 'Este navegador no permite calcular la huella (se necesita una conexión segura https).');
            return;
        }
        var lector = new FileReader();
        lector.onload = function () {
            window.crypto.subtle.digest('SHA-256', lector.result).then(function (buf) {
                var hex = Array.prototype.map.call(new Uint8Array(buf), function (b) {
                    return ('0' + b.toString(16)).slice(-2);
                }).join('');
                if (hex === (hidden.value || '').toLowerCase()) {
                    mostrar('vd-estado-ok', '✔ El archivo coincide: es exactamente el documento firmado (huella ' + hex + ').');
                } else {
                    mostrar('vd-estado-alerta', '✖ El archivo NO coincide con el documento firmado: fue modificado o es otro documento (huella ' + hex + ').');
                }
            });
        };
        lector.readAsArrayBuffer(archivo);
    });
})();
