// GestionUsuarios.aspx (E4-12): si se cancela la contraseña del administrador pedida al guardar el formulario
// de usuario (alta o cambio de rol), vuelve a abrir ese formulario con lo que se había escrito (la contraseña
// de un usuario nuevo no se conserva: un campo password no se vuelve a mostrar).
(function () {
    if (!window.jQuery) return;
    $(function () {
        var enviando = false;
        // Solo un envío que sale de verdad: si la validación lo cancela (p. ej. contraseña vacía), Cancelar
        // todavía debe reabrir el formulario del usuario.
        $('form').on('submit', function (e) {
            if (!e.isDefaultPrevented() && window.Page_IsValid !== false) enviando = true;
        });
        $('#modalReautenticar').on('hidden.bs.modal', function () {
            var accion = document.getElementById(SGV.hfAccionReaut);
            if (enviando || !accion || accion.value !== 'GUARDAR') return;
            accion.value = '';
            $('#modalUsuario').modal('show');
        });
    });
})();

// Conductor asociado (E5-20b): obligatorio solo con el rol Conductor (o Chofer), como RolesHelper.EsRolConductor;
// el servidor lo repite en btnGuardarUsuario_Click.
window.SGV = window.SGV || {};

SGV.guEsRolConductor = function () {
    var rol = document.getElementById(SGV.ddlRol);
    var r = rol ? String(rol.value || '').trim().toUpperCase() : '';
    return r === 'CONDUCTOR' || r === 'CHOFER';
};

function guValidarConductor(val, args) {
    args.IsValid = !SGV.guEsRolConductor() || String(args.Value || '') !== '';
}

(function () {
    var rol = document.getElementById(SGV.ddlRol);
    if (!rol) return;

    function actualizar() {
        var exige = SGV.guEsRolConductor();
        var asterisco = document.getElementById('guObligConductor');
        var campo = document.getElementById(SGV.ddlConductorUsuario);
        if (asterisco) asterisco.classList.toggle('d-none', !exige);
        if (campo) {
            if (exige) campo.setAttribute('aria-required', 'true');
            else campo.removeAttribute('aria-required');
        }
        // Ya marcado: al cambiar el rol, el mensaje se va (o vuelve) sin esperar a Guardar.
        var v = document.getElementById(SGV.cvConductor);
        if (v && campo && campo.hasAttribute('data-sgv-marcado') && typeof window.ValidatorValidate === 'function') {
            window.ValidatorValidate(v);
            if (typeof window.ValidatorUpdateIsValid === 'function') window.ValidatorUpdateIsValid();
        }
    }

    rol.addEventListener('change', actualizar);
    actualizar();
})();
