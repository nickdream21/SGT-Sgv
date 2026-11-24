// Dinero y pagos del viaje: muestra los campos que corresponden al tipo (entrega / pago directo).
window.SGV = window.SGV || {};

SGV.mvEsPagoDirecto = function () {
    var marcado = document.querySelector('#rblTipo input:checked');
    return !!marcado && marcado.value === 'PAGO_DIRECTO';
};

SGV.mvExigeOperacion = function () {
    var medio = document.getElementById(SGV.ddlMedio);
    return !!medio && (medio.value === 'YAPE' || medio.value === 'TRANSFERENCIA');
};

// Validadores del formulario (ClientValidationFunction: deben ser globales). El servidor repite cada regla
// (MovimientosViaje.aspx.cs y MovimientosViajeService.Validar).
function mvValidarProveedor(val, args) {
    args.IsValid = !SGV.mvEsPagoDirecto() || String(args.Value || '').trim() !== '';
}

function mvValidarOperacion(val, args) {
    args.IsValid = !SGV.mvExigeOperacion() || String(args.Value || '').trim() !== '';
}

// Soles o dólares distinto de 0. Un monto ilegible no cuenta aquí: lo marca SiteMaster.js (data-monto).
function mvValidarMonto(val, args) {
    var soles = SGV.leerMonto(args.Value);
    var dolares = SGV.leerMonto(document.getElementById(SGV.txtDolares).value);
    if (soles === null || dolares === null) { args.IsValid = true; return; }
    args.IsValid = soles !== 0 || dolares !== 0;
}

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

    // Un validador cuyo campo ya se marcó se vuelve a evaluar cuando cambia el otro dato del que depende.
    function revalidarSiMarcado(idValidador) {
        var v = document.getElementById(idValidador);
        var campo = v && document.getElementById(v.controltovalidate);
        if (!campo || !campo.hasAttribute('data-sgv-marcado') || typeof window.ValidatorValidate !== 'function') return;
        window.ValidatorValidate(v);
        if (typeof window.ValidatorUpdateIsValid === 'function') window.ValidatorUpdateIsValid();
    }

    // N.° de operación: obligatorio solo con Yape o transferencia (asterisco y aria-required a la par).
    function actualizarOperacion() {
        var exige = SGV.mvExigeOperacion();
        var asterisco = document.getElementById('mvObligOperacion');
        var campo = document.getElementById(SGV.txtOperacion);
        if (asterisco) asterisco.classList.toggle('d-none', !exige);
        if (campo) {
            if (exige) campo.setAttribute('aria-required', 'true');
            else campo.removeAttribute('aria-required');
        }
        revalidarSiMarcado(SGV.cvOperacion);
    }

    document.querySelectorAll('#rblTipo input').forEach(function (r) {
        r.addEventListener('change', function () { actualizar(); actualizarOperacion(); });
    });
    document.getElementById(SGV.ddlMedio).addEventListener('change', actualizarOperacion);
    // Escribir los dólares corrige el "indique el monto" que quedó marcado en soles.
    document.getElementById(SGV.txtDolares).addEventListener('input', function () { revalidarSiMarcado(SGV.cvMonto); });
    actualizar();
    actualizarOperacion();

    window.abrirAnular = function (id) {
        document.getElementById('hfIdAnular').value = id;
        $('#modalAnular').modal('show');
    };
})();

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    abrirAnular: function (el) { window.abrirAnular(Number(el.getAttribute('data-id'))); }
});
