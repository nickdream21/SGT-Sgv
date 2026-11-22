// SegundoFactor.aspx (E4-14): el cursor va al campo del código al abrir la página.
// Enter envía el código por data-enter (SiteMaster.js); aquí no se envía nada.
(function () {
    'use strict';
    function enfocar() {
        var campo = document.querySelector('.sf-codigo');
        if (campo && !campo.disabled) {
            try { campo.focus(); } catch (e) { /* sin foco en algunos navegadores */ }
        }
    }
    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', enfocar);
    else
        enfocar();
})();
