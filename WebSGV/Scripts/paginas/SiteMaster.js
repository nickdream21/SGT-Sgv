// ── Escudo anti-autocompletado ──────────────────────────────────────
// El formulario único de WebForms envuelve TODA la página, incluido el
// modal "Cambiar Contraseña" (con campos type=password reales). Eso hace
// que Chrome/Edge traten la página entera como un formulario de login y
// autocompleten campos de negocio (N° Pedido, Conductor, etc.) con
// usuario/contraseña guardados, sin que el usuario haga clic.
//
// Truco: los campos de texto quedan [readonly] hasta el primer focus, con
// lo que el navegador no tiene nada editable que autocompletar. Al hacer
// clic o tabular hasta el campo, se libera al instante (ver CSS .af-shield
// en <head> para que no se vea "deshabilitado" mientras tanto).
// No se aplica en Login.aspx: ahí SÍ queremos el autocompletado normal.
(function () {
    if (/Login\.aspx/i.test(window.location.pathname)) return;

    function protegerCampo(input) {
        if (input.dataset.afShielded === '1') return;
        input.dataset.afShielded = '1';
        input.classList.add('af-shield');
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('readonly', 'readonly');
        input.addEventListener('focus', function () {
            input.removeAttribute('readonly');
        }, { once: true });
    }

    function protegerFormulario() {
        var form = document.forms[0];
        if (!form) return;
        var campos = form.querySelectorAll('input[type="text"]:not([readonly]), input:not([type]):not([readonly])');
        for (var i = 0; i < campos.length; i++) protegerCampo(campos[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', protegerFormulario);
    } else {
        protegerFormulario();
    }

    // Reaplicar sobre los campos nuevos que trae cada postback parcial de UpdatePanel.
    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        Sys.WebForms.PageRequestManager.getInstance().add_endRequest(protegerFormulario);
    }
})();
