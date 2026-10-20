// Reporte GPS por placas: aviso mientras se consulta el GPS y botones de copiar para el correo.
(function () {
    'use strict';

    $(function () {
        $('#' + SGV.btnGenerar).on('click', function () {
            $('#avisoGenerando').removeClass('d-none');
            // Se deshabilita después del envío para no cortar el postback.
            var boton = this;
            setTimeout(function () { boton.disabled = true; }, 0);
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
