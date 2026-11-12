// Dinero y pagos del viaje: muestra los campos que corresponden al tipo (entrega / pago directo).
(function () {
    'use strict';

    function tipoActual() {
        var marcado = document.querySelector('#rblTipo input:checked');
        return marcado ? marcado.value : 'ENTREGA';
    }

    function mostrar(selector, visible) {
        document.querySelectorAll(selector).forEach(function (el) { el.style.display = visible ? '' : 'none'; });
    }

    function actualizar() {
        var esPago = tipoActual() === 'PAGO_DIRECTO';
        var medio = document.getElementById(SGV.ddlMedio);
        mostrar('.mv-solo-entrega', !esPago);
        mostrar('.mv-solo-pago', esPago);
        mostrar('.mv-ayuda-entrega', !esPago);
        mostrar('.mv-ayuda-pago', esPago);
        // Un pago directo al proveedor no va en sobre.
        var opcionSobre = medio.querySelector('option[value="SOBRE"]');
        if (opcionSobre) {
            opcionSobre.disabled = esPago;
            if (esPago && medio.value === 'SOBRE') medio.value = 'YAPE';
        }
    }

    document.querySelectorAll('#rblTipo input').forEach(function (r) { r.addEventListener('change', actualizar); });
    actualizar();

    window.abrirAnular = function (id) {
        document.getElementById('hfIdAnular').value = id;
        $('#modalAnular').modal('show');
    };
})();
