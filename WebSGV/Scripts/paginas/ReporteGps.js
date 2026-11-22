// Reporte GPS por placas: aviso mientras se consulta el GPS y botones de copiar para el correo.
(function () {
    'use strict';

    $(function () {
        $('#' + SGV.btnGenerar).on('click', function () {
            // El doble envío lo frena SiteMaster.js (E5-2; data-espera en el botón: la consulta tarda).
            $('#avisoGenerando').removeClass('d-none');
        });

        $('[data-copiar]').on('click', function () {
            var campo = document.getElementById($(this).data('copiar'));
            campo.select();
            var copiar = navigator.clipboard
                ? navigator.clipboard.writeText(campo.value)
                : Promise.resolve(document.execCommand('copy'));
            copiar.then(function () { SGV.avisar('Copiado.', { tipo: 'success' }); });
        });
    });
})();
