// MiSeguridad.aspx (E4-14): copiar o imprimir los códigos de recuperación recién creados.
(function ($) {
    'use strict';

    function textoCodigos() {
        return $('#listaCodigos li').map(function () { return $(this).text().trim(); }).get().join('\n');
    }

    $(document).on('click', '.js-copiar-codigos', function () {
        var texto = textoCodigos();
        var avisar = function (m, t) { if (window.SGV && SGV.avisar) SGV.avisar(m, { tipo: t }); };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(texto).then(
                function () { avisar('Códigos copiados. Péguelos en un lugar seguro.', 'success'); },
                function () { avisar('No se pudieron copiar: selecciónelos y cópielos a mano.', 'warning'); });
        } else {
            avisar('Este navegador no permite copiar: selecciónelos y cópielos a mano.', 'warning');
        }
    });

    $(document).on('click', '.js-imprimir-codigos', function () {
        window.print();
    });

    $(function () {
        var campo = document.querySelector('.ms-codigo');
        if (campo && $(campo).is(':visible')) {
            try { campo.focus(); } catch (e) { /* sin foco */ }
        }
    });
})(jQuery);
