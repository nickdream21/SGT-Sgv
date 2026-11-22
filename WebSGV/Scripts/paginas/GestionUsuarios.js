// GestionUsuarios.aspx (E4-12): si se cancela la contraseña del administrador pedida al guardar el formulario
// de usuario (alta o cambio de rol), vuelve a abrir ese formulario con lo que se había escrito (la contraseña
// de un usuario nuevo no se conserva: un campo password no se vuelve a mostrar).
(function () {
    if (!window.jQuery) return;
    $(function () {
        var enviando = false;
        $('form').on('submit', function () { enviando = true; });
        $('#modalReautenticar').on('hidden.bs.modal', function () {
            var accion = document.getElementById(SGV.hfAccionReaut);
            if (enviando || !accion || accion.value !== 'GUARDAR') return;
            accion.value = '';
            $('#modalUsuario').modal('show');
        });
    });
})();
